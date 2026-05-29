using System.Security.Claims;
using System.Text;
using Dapper;
using DoctorAppointments.Api.Contracts;
using DoctorAppointments.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=doctorappointments.db";
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
var rateLimitPermit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 10);
var rateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60);

builder.Services.AddSingleton(new AppDb(connectionString));
builder.Services.AddSingleton<IPasswordHasher<object>, PasswordHasher<object>>();
builder.Services.AddSingleton<DbInitializer>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "global";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimitPermit,
            Window = TimeSpan.FromSeconds(rateLimitWindowSeconds),
            QueueLimit = 0,
        });
    });
});

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Doctor Appointment Application API",
        Version = "v1",
        Description = "JWT auth, refresh token, role based access, Dapper + SQLite demo API for admin/doctor/patient dashboards.",
    });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme,
        },
    };

    options.AddSecurityDefinition(jwtSecurityScheme.Reference.Id, jwtSecurityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [jwtSecurityScheme] = Array.Empty<string>(),
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
    await initializer.InitializeAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

// English + Hindi: middleware order important hai, warna CORS/auth/rate-limit sahi kaam nahi karega.
app.UseRateLimiter();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    message = "Doctor Appointment API is running.",
    swagger = "/swagger",
    seededUsers = new[]
    {
        "admin@doctorapp.com / Admin@123",
        "sonia@doctorapp.com / Doctor@123",
        "amit@doctorapp.com / Doctor@123",
        "rahul@doctorapp.com / Patient@123",
    },
})).AllowAnonymous();

app.MapPost("/api/auth/login", async (LoginRequest request, AppDb appDb, IPasswordHasher<object> passwordHasher, JwtTokenService tokenService) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Email and password are required." });
    }

    using var connection = appDb.CreateConnection();
    var user = await connection.QuerySingleOrDefaultAsync<DbUser>(
        "SELECT Id, Name, Email, PasswordHash, Role, Specialization FROM Users WHERE lower(Email) = lower(@Email);",
        new { request.Email });

    if (user is null)
    {
        return Results.Unauthorized();
    }

    var verification = passwordHasher.VerifyHashedPassword(new object(), user.PasswordHash, request.Password);
    if (verification == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    var response = tokenService.CreateAuthResponse(user);
    await PersistRefreshTokenAsync(connection, user.Id, response.RefreshToken, tokenService.GetRefreshTokenDays());

    return Results.Ok(response);
})
.AllowAnonymous()
.RequireRateLimiting("auth")
.WithSummary("Login with JWT")
.WithDescription("Validates hashed password and returns access token + refresh token.");

app.MapPost("/api/auth/refresh", async (RefreshRequest request, AppDb appDb, JwtTokenService tokenService) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return Results.BadRequest(new { message = "Refresh token is required." });
    }

    using var connection = appDb.CreateConnection();
    var tokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken);
    var refreshToken = await connection.QuerySingleOrDefaultAsync<RefreshTokenRecord>(
        "SELECT Id, UserId, TokenHash, ExpiresAtUtc, RevokedAtUtc FROM RefreshTokens WHERE TokenHash = @TokenHash;",
        new { TokenHash = tokenHash });

    if (refreshToken is null || refreshToken.RevokedAtUtc is not null || refreshToken.ExpiresAtUtc <= DateTime.UtcNow)
    {
        return Results.Unauthorized();
    }

    var user = await connection.QuerySingleAsync<DbUser>(
        "SELECT Id, Name, Email, PasswordHash, Role, Specialization FROM Users WHERE Id = @Id;",
        new { Id = refreshToken.UserId });

    await connection.ExecuteAsync(
        "UPDATE RefreshTokens SET RevokedAtUtc = @RevokedAtUtc WHERE Id = @Id;",
        new { RevokedAtUtc = DateTime.UtcNow, refreshToken.Id });

    var response = tokenService.CreateAuthResponse(user);
    await PersistRefreshTokenAsync(connection, user.Id, response.RefreshToken, tokenService.GetRefreshTokenDays());

    return Results.Ok(response);
})
.AllowAnonymous()
.RequireRateLimiting("auth")
.WithSummary("Refresh JWT")
.WithDescription("Refreshes JWT using a stored refresh token hash.");

app.MapPost("/api/auth/logout", async (LogoutRequest request, ClaimsPrincipal principal, AppDb appDb) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return Results.BadRequest(new { message = "Refresh token is required." });
    }

    var currentUser = principal.ToCurrentUser();
    using var connection = appDb.CreateConnection();
    await connection.ExecuteAsync(
        "UPDATE RefreshTokens SET RevokedAtUtc = @RevokedAtUtc WHERE UserId = @UserId AND TokenHash = @TokenHash;",
        new
        {
            RevokedAtUtc = DateTime.UtcNow,
            UserId = currentUser.Id,
            TokenHash = JwtTokenService.HashRefreshToken(request.RefreshToken),
        });

    return Results.Ok(new { message = "Logged out successfully." });
})
.RequireAuthorization()
.WithSummary("Logout")
.WithDescription("Revokes a refresh token for the currently logged-in user.");

app.MapGet("/api/users/me", (ClaimsPrincipal principal) =>
{
    var currentUser = principal.ToCurrentUser();
    return Results.Ok(currentUser);
})
.RequireAuthorization()
.WithSummary("Current user")
.WithDescription("Returns the current authenticated user's claims summary.");

app.MapGet("/api/dashboard", async (ClaimsPrincipal principal, AppDb appDb) =>
{
    var currentUser = principal.ToCurrentUser();
    using var connection = appDb.CreateConnection();

    var response = new DashboardResponse
    {
        Title = $"{currentUser.Role} Dashboard",
        Message = currentUser.Role switch
        {
            Roles.Admin => "Admin can monitor doctors, patients, and all appointments.",
            Roles.Doctor => "Doctor can manage only linked patients and appointments.",
            _ => "Patient can review personal appointments and connected doctors.",
        },
    };

    if (currentUser.Role == Roles.Admin)
    {
        response.Cards =
        [
            new() { Label = "Doctors", Value = (await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM Users WHERE Role = @Role;", new { Role = Roles.Doctor })).ToString() },
            new() { Label = "Patients", Value = (await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM Users WHERE Role = @Role;", new { Role = Roles.Patient })).ToString() },
            new() { Label = "Appointments", Value = (await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM Appointments;")).ToString() },
        ];
    }
    else if (currentUser.Role == Roles.Doctor)
    {
        response.Cards =
        [
            new() { Label = "My Patients", Value = (await connection.ExecuteScalarAsync<long>("SELECT COUNT(DISTINCT PatientId) FROM Appointments WHERE DoctorId = @DoctorId;", new { DoctorId = currentUser.Id })).ToString() },
            new() { Label = "My Appointments", Value = (await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM Appointments WHERE DoctorId = @DoctorId;", new { DoctorId = currentUser.Id })).ToString() },
            new() { Label = "Role", Value = currentUser.Role },
        ];
    }
    else
    {
        response.Cards =
        [
            new() { Label = "My Doctors", Value = (await connection.ExecuteScalarAsync<long>("SELECT COUNT(DISTINCT DoctorId) FROM Appointments WHERE PatientId = @PatientId;", new { PatientId = currentUser.Id })).ToString() },
            new() { Label = "My Appointments", Value = (await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM Appointments WHERE PatientId = @PatientId;", new { PatientId = currentUser.Id })).ToString() },
            new() { Label = "Role", Value = currentUser.Role },
        ];
    }

    return Results.Ok(response);
})
.RequireAuthorization()
.WithSummary("Role-wise dashboard")
.WithDescription("Returns different summary cards for admin, doctor, and patient.");

app.MapGet("/api/doctors", async (AppDb appDb) =>
{
    using var connection = appDb.CreateConnection();
    var doctors = await connection.QueryAsync<DoctorListItem>(
        "SELECT Id, Name, Email, COALESCE(Specialization, '') AS Specialization FROM Users WHERE Role = @Role ORDER BY Name;",
        new { Role = Roles.Doctor });

    return Results.Ok(doctors);
})
.RequireAuthorization(policy => policy.RequireRole(Roles.Admin, Roles.Patient, Roles.Doctor))
.WithSummary("Doctors list")
.WithDescription("Returns doctors for dropdown/list use on the frontend.");

app.MapGet("/api/patients", async (ClaimsPrincipal principal, AppDb appDb, int? pageNumber, int? pageSize) =>
{
    var currentUser = principal.ToCurrentUser();
    var paging = NormalizePaging(pageNumber, pageSize);

    using var connection = appDb.CreateConnection();
    IEnumerable<PatientListItem> items;
    int totalCount;

    if (currentUser.Role == Roles.Admin)
    {
        totalCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Users WHERE Role = @Role;", new { Role = Roles.Patient });
        items = await connection.QueryAsync<PatientListItem>(
            """
            SELECT
                p.Id,
                p.Name,
                p.Email,
                COALESCE((
                    SELECT GROUP_CONCAT(d.Name, ', ')
                    FROM Appointments a
                    JOIN Users d ON d.Id = a.DoctorId
                    WHERE a.PatientId = p.Id
                ), '') AS DoctorNames,
                COALESCE((SELECT COUNT(1) FROM Appointments a WHERE a.PatientId = p.Id), 0) AS AppointmentCount
            FROM Users p
            WHERE p.Role = @PatientRole
            ORDER BY p.Name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { PatientRole = Roles.Patient, paging.PageSize, paging.Offset });
    }
    else
    {
        totalCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(DISTINCT PatientId) FROM Appointments WHERE DoctorId = @DoctorId;",
            new { DoctorId = currentUser.Id });
        items = await connection.QueryAsync<PatientListItem>(
            """
            SELECT
                p.Id,
                p.Name,
                p.Email,
                @DoctorName AS DoctorNames,
                COALESCE((SELECT COUNT(1) FROM Appointments a WHERE a.PatientId = p.Id AND a.DoctorId = @DoctorId), 0) AS AppointmentCount
            FROM Users p
            WHERE p.Id IN (
                SELECT DISTINCT PatientId FROM Appointments WHERE DoctorId = @DoctorId
            )
            ORDER BY p.Name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { DoctorId = currentUser.Id, DoctorName = currentUser.Name, paging.PageSize, paging.Offset });
    }

    return Results.Ok(ToPagedResponse(items, paging.PageNumber, paging.PageSize, totalCount));
})
.RequireAuthorization(policy => policy.RequireRole(Roles.Admin, Roles.Doctor))
.WithSummary("Patients with pagination")
.WithDescription("Admin sees all patients; doctor sees only linked patients. Supports pageNumber and pageSize.");

app.MapGet("/api/appointments", async (ClaimsPrincipal principal, AppDb appDb, int? pageNumber, int? pageSize) =>
{
    var currentUser = principal.ToCurrentUser();
    var paging = NormalizePaging(pageNumber, pageSize);

    using var connection = appDb.CreateConnection();
    var baseWhere = currentUser.Role switch
    {
        Roles.Admin => "1 = 1",
        Roles.Doctor => "a.DoctorId = @UserId",
        _ => "a.PatientId = @UserId",
    };

    var totalCount = await connection.ExecuteScalarAsync<int>(
        $"SELECT COUNT(1) FROM Appointments a WHERE {baseWhere};",
        new { UserId = currentUser.Id });

    var items = await connection.QueryAsync<AppointmentListItem>(
        $"""
        SELECT
            a.Id,
            d.Name AS DoctorName,
            p.Name AS PatientName,
            strftime('%Y-%m-%d %H:%M', a.AppointmentDate) AS AppointmentDate,
            a.Status,
            COALESCE(a.Notes, '') AS Notes
        FROM Appointments a
        JOIN Users d ON d.Id = a.DoctorId
        JOIN Users p ON p.Id = a.PatientId
        WHERE {baseWhere}
        ORDER BY a.AppointmentDate
        LIMIT @PageSize OFFSET @Offset;
        """,
        new { UserId = currentUser.Id, paging.PageSize, paging.Offset });

    return Results.Ok(ToPagedResponse(items, paging.PageNumber, paging.PageSize, totalCount));
})
.RequireAuthorization(policy => policy.RequireRole(Roles.Admin, Roles.Doctor, Roles.Patient))
.WithSummary("Appointments with pagination")
.WithDescription("Admin sees all appointments, doctor sees own, patient sees personal appointments.");

app.Run();

static async Task PersistRefreshTokenAsync(System.Data.IDbConnection connection, long userId, string refreshToken, int refreshTokenDays)
{
    await connection.ExecuteAsync(
        "INSERT INTO RefreshTokens (UserId, TokenHash, ExpiresAtUtc) VALUES (@UserId, @TokenHash, @ExpiresAtUtc);",
        new
        {
            UserId = userId,
            TokenHash = JwtTokenService.HashRefreshToken(refreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(refreshTokenDays),
        });
}

static (int PageNumber, int PageSize, int Offset) NormalizePaging(int? pageNumber, int? pageSize)
{
    var safePageNumber = Math.Max(pageNumber ?? 1, 1);
    var safePageSize = Math.Clamp(pageSize ?? 5, 1, 20);
    return (safePageNumber, safePageSize, (safePageNumber - 1) * safePageSize);
}

static PagedResponse<T> ToPagedResponse<T>(IEnumerable<T> items, int pageNumber, int pageSize, int totalCount)
{
    return new PagedResponse<T>
    {
        Items = items.ToList(),
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalCount = totalCount,
        TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize),
    };
}
