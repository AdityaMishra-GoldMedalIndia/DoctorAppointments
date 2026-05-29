namespace DoctorAppointments.Api.Infrastructure;

/// <summary>
/// English + Hindi: SQL Server schema aur stored procedures yahan rakhe gaye hain.
/// Har stored procedure apne alag batch me run hota hai (CREATE OR ALTER PROCEDURE
/// kisi batch ka pehla statement hona chahiye).
/// </summary>
public static class DatabaseScripts
{
    public const string Schema = """
        IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Users (
                Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                Name NVARCHAR(200) NOT NULL,
                Email NVARCHAR(256) NOT NULL UNIQUE,
                PasswordHash NVARCHAR(MAX) NOT NULL,
                Role NVARCHAR(50) NOT NULL,
                Specialization NVARCHAR(200) NULL
            );
        END;

        IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.RefreshTokens (
                Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                UserId BIGINT NOT NULL,
                TokenHash NVARCHAR(256) NOT NULL UNIQUE,
                ExpiresAtUtc DATETIME2 NOT NULL,
                RevokedAtUtc DATETIME2 NULL,
                CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id)
            );
        END;

        IF OBJECT_ID(N'dbo.Appointments', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Appointments (
                Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                DoctorId BIGINT NOT NULL,
                PatientId BIGINT NOT NULL,
                AppointmentDate DATETIME2 NOT NULL,
                Status NVARCHAR(50) NOT NULL,
                Notes NVARCHAR(MAX) NULL,
                CONSTRAINT FK_Appointments_Doctor FOREIGN KEY (DoctorId) REFERENCES dbo.Users(Id),
                CONSTRAINT FK_Appointments_Patient FOREIGN KEY (PatientId) REFERENCES dbo.Users(Id)
            );
        END;
        """;

    public static readonly string[] StoredProcedures =
    [
        """
        CREATE OR ALTER PROCEDURE dbo.usp_Users_GetByEmail
            @Email NVARCHAR(256)
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT Id, Name, Email, PasswordHash, Role, Specialization
            FROM dbo.Users
            WHERE LOWER(Email) = LOWER(@Email);
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Users_GetById
            @Id BIGINT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT Id, Name, Email, PasswordHash, Role, Specialization
            FROM dbo.Users
            WHERE Id = @Id;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Users_GetByRole
            @Role NVARCHAR(50)
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT Id, Email
            FROM dbo.Users
            WHERE Role = @Role;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Users_Count
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(1) AS BIGINT) FROM dbo.Users;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Users_CountByRole
            @Role NVARCHAR(50)
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(1) AS BIGINT) FROM dbo.Users WHERE Role = @Role;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Users_Insert
            @Name NVARCHAR(200),
            @Email NVARCHAR(256),
            @PasswordHash NVARCHAR(MAX),
            @Role NVARCHAR(50),
            @Specialization NVARCHAR(200) = NULL
        AS
        BEGIN
            SET NOCOUNT ON;
            INSERT INTO dbo.Users (Name, Email, PasswordHash, Role, Specialization)
            VALUES (@Name, @Email, @PasswordHash, @Role, @Specialization);
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Doctors_GetAll
            @Role NVARCHAR(50)
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT Id, Name, Email, COALESCE(Specialization, '') AS Specialization
            FROM dbo.Users
            WHERE Role = @Role
            ORDER BY Name;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_RefreshTokens_Insert
            @UserId BIGINT,
            @TokenHash NVARCHAR(256),
            @ExpiresAtUtc DATETIME2
        AS
        BEGIN
            SET NOCOUNT ON;
            INSERT INTO dbo.RefreshTokens (UserId, TokenHash, ExpiresAtUtc)
            VALUES (@UserId, @TokenHash, @ExpiresAtUtc);
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_RefreshTokens_GetByHash
            @TokenHash NVARCHAR(256)
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT Id, UserId, TokenHash, ExpiresAtUtc, RevokedAtUtc
            FROM dbo.RefreshTokens
            WHERE TokenHash = @TokenHash;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_RefreshTokens_RevokeById
            @Id BIGINT,
            @RevokedAtUtc DATETIME2
        AS
        BEGIN
            SET NOCOUNT ON;
            UPDATE dbo.RefreshTokens
            SET RevokedAtUtc = @RevokedAtUtc
            WHERE Id = @Id;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_RefreshTokens_RevokeByUserAndHash
            @UserId BIGINT,
            @TokenHash NVARCHAR(256),
            @RevokedAtUtc DATETIME2
        AS
        BEGIN
            SET NOCOUNT ON;
            UPDATE dbo.RefreshTokens
            SET RevokedAtUtc = @RevokedAtUtc
            WHERE UserId = @UserId AND TokenHash = @TokenHash;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_Insert
            @DoctorId BIGINT,
            @PatientId BIGINT,
            @AppointmentDate DATETIME2,
            @Status NVARCHAR(50),
            @Notes NVARCHAR(MAX) = NULL
        AS
        BEGIN
            SET NOCOUNT ON;
            INSERT INTO dbo.Appointments (DoctorId, PatientId, AppointmentDate, Status, Notes)
            VALUES (@DoctorId, @PatientId, @AppointmentDate, @Status, @Notes);
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_Count
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(1) AS BIGINT) FROM dbo.Appointments;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_CountDistinctPatientsByDoctor
            @DoctorId BIGINT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(DISTINCT PatientId) AS BIGINT)
            FROM dbo.Appointments
            WHERE DoctorId = @DoctorId;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_CountByDoctor
            @DoctorId BIGINT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(1) AS BIGINT)
            FROM dbo.Appointments
            WHERE DoctorId = @DoctorId;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_CountDistinctDoctorsByPatient
            @PatientId BIGINT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(DISTINCT DoctorId) AS BIGINT)
            FROM dbo.Appointments
            WHERE PatientId = @PatientId;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_CountByPatient
            @PatientId BIGINT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(1) AS BIGINT)
            FROM dbo.Appointments
            WHERE PatientId = @PatientId;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Patients_CountForAdmin
            @PatientRole NVARCHAR(50)
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(1) AS INT) FROM dbo.Users WHERE Role = @PatientRole;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Patients_CountForDoctor
            @DoctorId BIGINT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(DISTINCT PatientId) AS INT)
            FROM dbo.Appointments
            WHERE DoctorId = @DoctorId;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Patients_GetPagedForAdmin
            @PatientRole NVARCHAR(50),
            @PageSize INT,
            @Offset INT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT
                p.Id,
                p.Name,
                p.Email,
                COALESCE((
                    SELECT STRING_AGG(d.Name, ', ')
                    FROM dbo.Appointments a
                    JOIN dbo.Users d ON d.Id = a.DoctorId
                    WHERE a.PatientId = p.Id
                ), '') AS DoctorNames,
                COALESCE((SELECT COUNT(1) FROM dbo.Appointments a WHERE a.PatientId = p.Id), 0) AS AppointmentCount
            FROM dbo.Users p
            WHERE p.Role = @PatientRole
            ORDER BY p.Name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Patients_GetPagedForDoctor
            @DoctorId BIGINT,
            @DoctorName NVARCHAR(200),
            @PageSize INT,
            @Offset INT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT
                p.Id,
                p.Name,
                p.Email,
                @DoctorName AS DoctorNames,
                COALESCE((SELECT COUNT(1) FROM dbo.Appointments a WHERE a.PatientId = p.Id AND a.DoctorId = @DoctorId), 0) AS AppointmentCount
            FROM dbo.Users p
            WHERE p.Id IN (
                SELECT DISTINCT PatientId FROM dbo.Appointments WHERE DoctorId = @DoctorId
            )
            ORDER BY p.Name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_CountByRole
            @Role NVARCHAR(50),
            @UserId BIGINT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT CAST(COUNT(1) AS INT)
            FROM dbo.Appointments a
            WHERE (@Role = 'Admin')
                OR (@Role = 'Doctor' AND a.DoctorId = @UserId)
                OR (@Role = 'Patient' AND a.PatientId = @UserId);
        END;
        """,

        """
        CREATE OR ALTER PROCEDURE dbo.usp_Appointments_GetPaged
            @Role NVARCHAR(50),
            @UserId BIGINT,
            @PageSize INT,
            @Offset INT
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT
                a.Id,
                d.Name AS DoctorName,
                p.Name AS PatientName,
                FORMAT(a.AppointmentDate, 'yyyy-MM-dd HH:mm') AS AppointmentDate,
                a.Status,
                COALESCE(a.Notes, '') AS Notes
            FROM dbo.Appointments a
            JOIN dbo.Users d ON d.Id = a.DoctorId
            JOIN dbo.Users p ON p.Id = a.PatientId
            WHERE (@Role = 'Admin')
                OR (@Role = 'Doctor' AND a.DoctorId = @UserId)
                OR (@Role = 'Patient' AND a.PatientId = @UserId)
            ORDER BY a.AppointmentDate
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        END;
        """,
    ];
}
