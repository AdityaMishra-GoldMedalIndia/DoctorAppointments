using Dapper;
using DoctorAppointments.Api.Contracts;
using Microsoft.AspNetCore.Identity;

namespace DoctorAppointments.Api.Infrastructure;

public sealed class DbInitializer
{
    private readonly AppDb _appDb;
    private readonly DataAccess _dataAccess;
    private readonly IPasswordHasher<object> _passwordHasher;

    public DbInitializer(AppDb appDb, DataAccess dataAccess, IPasswordHasher<object> passwordHasher)
    {
        _appDb = appDb;
        _dataAccess = dataAccess;
        _passwordHasher = passwordHasher;
    }

    public async Task InitializeAsync()
    {
        using (var connection = _appDb.CreateConnection())
        {
            // English + Hindi: pehle SQL Server schema banta hai (agar pehle se nahi hai).
            await connection.ExecuteAsync(DatabaseScripts.Schema);

            // English + Hindi: har stored procedure apne alag batch me create hota hai,
            // kyunki CREATE OR ALTER PROCEDURE batch ka pehla statement hona chahiye.
            foreach (var procedure in DatabaseScripts.StoredProcedures)
            {
                await connection.ExecuteAsync(procedure);
            }
        }

        var totalUsers = await _dataAccess.ExecuteScalarAsync<long>("usp_Users_Count");
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
            await _dataAccess.ExecuteAsync(
                "usp_Users_Insert",
                new
                {
                    user.Name,
                    user.Email,
                    PasswordHash = _passwordHasher.HashPassword(new object(), user.Password),
                    user.Role,
                    user.Specialization,
                });
        }

        var doctorIds = (await _dataAccess.QueryAsync<(long Id, string Email)>("usp_Users_GetByRole", new { Role = Roles.Doctor }))
            .ToDictionary(x => x.Email, x => x.Id);
        var patientIds = (await _dataAccess.QueryAsync<(long Id, string Email)>("usp_Users_GetByRole", new { Role = Roles.Patient }))
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
            await _dataAccess.ExecuteAsync(
                "usp_Appointments_Insert",
                appointment);
        }
    }
}
