using System.Data;
using Microsoft.Data.SqlClient;

namespace DoctorAppointments.Api.Infrastructure;

public sealed class AppDb
{
    private readonly string _connectionString;

    public AppDb(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
