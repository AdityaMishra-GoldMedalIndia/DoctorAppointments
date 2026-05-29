using System.Data;
using Dapper;

namespace DoctorAppointments.Api.Infrastructure;

/// <summary>
/// English + Hindi: ye ek generic data access helper hai jo sirf SQL Server stored procedures
/// ko call karta hai. Saare endpoints inhi generic methods ke through DB se baat karte hain,
/// taaki inline SQL kahin na likha jaye.
/// </summary>
public sealed class DataAccess
{
    private readonly AppDb _appDb;

    public DataAccess(AppDb appDb)
    {
        _appDb = appDb;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string storedProcedure, object? parameters = null)
    {
        using var connection = _appDb.CreateConnection();
        var rows = await connection.QueryAsync<T>(
            storedProcedure,
            parameters,
            commandType: CommandType.StoredProcedure);
        return rows.AsList();
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string storedProcedure, object? parameters = null)
    {
        using var connection = _appDb.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<T>(
            storedProcedure,
            parameters,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<T> QuerySingleAsync<T>(string storedProcedure, object? parameters = null)
    {
        using var connection = _appDb.CreateConnection();
        return await connection.QuerySingleAsync<T>(
            storedProcedure,
            parameters,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> ExecuteAsync(string storedProcedure, object? parameters = null)
    {
        using var connection = _appDb.CreateConnection();
        return await connection.ExecuteAsync(
            storedProcedure,
            parameters,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string storedProcedure, object? parameters = null)
    {
        using var connection = _appDb.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(
            storedProcedure,
            parameters,
            commandType: CommandType.StoredProcedure);
    }
}
