using DP.Infrastructure.Database;
using DP.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace DP.Api.Services;

public interface IIdempotencyService
{
    Task<IdempotencyKey?> GetCachedResponseAsync(string key);
    Task CacheResponseAsync(string key, string requestBody, string responseBody, int statusCode);
    string ComputeHash(string input);
}

public class IdempotencyService : IIdempotencyService
{
    private readonly AppDbContext _dbContext;

    public IdempotencyService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IdempotencyKey?> GetCachedResponseAsync(string key)
    {
        return await _dbContext.IdempotencyKeys.FirstOrDefaultAsync(k => k.Key == key);
    }

    public async Task CacheResponseAsync(string key, string requestBody, string responseBody, int statusCode)
    {
        var requestHash = ComputeHash(requestBody);

        var idempotencyRecord = new IdempotencyKey
        {
            Key = key,
            RequestHash = requestHash,
            ResponseBody = responseBody,
            StatusCode = statusCode
        };

        try
        {
            _dbContext.IdempotencyKeys.Add(idempotencyRecord);
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // This happens if another request with the same key was processed 
            // and saved while this one was in fly. We ignore it as the response 
            // is already cached.
        }
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    public string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
