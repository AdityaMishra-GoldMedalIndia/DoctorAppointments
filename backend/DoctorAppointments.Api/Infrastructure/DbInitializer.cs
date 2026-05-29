using Dapper;
using DoctorAppointments.Api.Contracts;
using Microsoft.AspNetCore.Identity;

namespace DoctorAppointments.Api.Infrastructure;

public sealed class DbInitializer
{
    private readonly AppDb _appDb;
    private readonly IPasswordHasher<object> _passwordHasher;

    public DbInitializer(AppDb appDb, IPasswordHasher<object> passwordHasher)
    {
        _appDb = appDb;
        _passwordHasher = passwordHasher;
    }

    public async Task InitializeAsync()
    {
        using var connection = _appDb.CreateConnection();

        // English + Hindi: yahan par lightweight SQLite schema ban raha hai taaki demo/project jaldi run ho sake.
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL,
                Specialization TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS RefreshTokens (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                TokenHash TEXT NOT NULL UNIQUE,
                ExpiresAtUtc TEXT NOT NULL,
                RevokedAtUtc TEXT NULL,
                FOREIGN KEY(UserId) REFERENCES Users(Id)
            );

            CREATE TABLE IF NOT EXISTS Appointments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoctorId INTEGER NOT NULL,
                PatientId INTEGER NOT NULL,
                AppointmentDate TEXT NOT NULL,
                Status TEXT NOT NULL,
                Notes TEXT NULL,
                FOREIGN KEY(DoctorId) REFERENCES Users(Id),
                FOREIGN KEY(PatientId) REFERENCES Users(Id)
            );
            """);

        var totalUsers = await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM Users;");
        if (totalUsers > 0)
        {
            return;
        }

        var seedUsers = new List<SeedUser>
        {
            new() { Name = "Admin User", Email = "admin@doctorapp.com", Password = "Admin@123", Role = Roles.Admin },
            new() { Name = "Dr. Sonia Kapoor", Email = "sonia@doctorapp.com", Password = "Doctor@123", Role = Roles.Doctor, Specialization = "Cardiology" },
            new() { Name = "Dr. Amit Verma", Email = "amit@doctorapp.com", Password = "Doctor@123", Role = Roles.Doctor, Specialization = "Dermatology" },
            new() { Name = "Rahul Sharma", Email = "rahul@doctorapp.com", Password = "Patient@123", Role = Roles.Patient },
            new() { Name = "Pooja Singh", Email = "pooja@doctorapp.com", Password = "Patient@123", Role = Roles.Patient },
            new() { Name = "Neha Joshi", Email = "neha@doctorapp.com", Password = "Patient@123", Role = Roles.Patient },
            new() { Name = "Vikram Mehta", Email = "vikram@doctorapp.com", Password = "Patient@123", Role = Roles.Patient },
        };

        foreach (var user in seedUsers)
        {
            await connection.ExecuteAsync(
                "INSERT INTO Users (Name, Email, PasswordHash, Role, Specialization) VALUES (@Name, @Email, @PasswordHash, @Role, @Specialization);",
                new
                {
                    user.Name,
                    user.Email,
                    PasswordHash = _passwordHasher.HashPassword(new object(), user.Password),
                    user.Role,
                    user.Specialization,
                });
        }

        var doctorIds = (await connection.QueryAsync<(long Id, string Email)>("SELECT Id, Email FROM Users WHERE Role = @Role;", new { Role = Roles.Doctor }))
            .ToDictionary(x => x.Email, x => x.Id);
        var patientIds = (await connection.QueryAsync<(long Id, string Email)>("SELECT Id, Email FROM Users WHERE Role = @Role;", new { Role = Roles.Patient }))
            .ToDictionary(x => x.Email, x => x.Id);

        var appointments = new[]
        {
            new { DoctorId = doctorIds["sonia@doctorapp.com"], PatientId = patientIds["rahul@doctorapp.com"], AppointmentDate = DateTime.UtcNow.AddDays(1), Status = "Confirmed", Notes = "Heart follow-up / Dil checkup" },
            new { DoctorId = doctorIds["sonia@doctorapp.com"], PatientId = patientIds["pooja@doctorapp.com"], AppointmentDate = DateTime.UtcNow.AddDays(2), Status = "Pending", Notes = "ECG review" },
            new { DoctorId = doctorIds["amit@doctorapp.com"], PatientId = patientIds["neha@doctorapp.com"], AppointmentDate = DateTime.UtcNow.AddDays(3), Status = "Confirmed", Notes = "Skin allergy consultation" },
            new { DoctorId = doctorIds["amit@doctorapp.com"], PatientId = patientIds["vikram@doctorapp.com"], AppointmentDate = DateTime.UtcNow.AddDays(4), Status = "Scheduled", Notes = "Routine follow-up" },
            new { DoctorId = doctorIds["sonia@doctorapp.com"], PatientId = patientIds["vikram@doctorapp.com"], AppointmentDate = DateTime.UtcNow.AddDays(5), Status = "Scheduled", Notes = "Second opinion" },
        };

        foreach (var appointment in appointments)
        {
            await connection.ExecuteAsync(
                "INSERT INTO Appointments (DoctorId, PatientId, AppointmentDate, Status, Notes) VALUES (@DoctorId, @PatientId, @AppointmentDate, @Status, @Notes);",
                appointment);
        }
    }
}
