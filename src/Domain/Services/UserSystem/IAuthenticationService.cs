using DbApp.Domain.Entities.UserSystem;

namespace DbApp.Domain.Services.UserSystem;

/// <summary>
/// Domain service interface for authentication operations.
/// Defines the contract for authentication-related business logic.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Validates user credentials against the database.
    /// </summary>
    /// <param name="username">Username to validate</param>
    /// <param name="password">Plain text password to validate</param>
    /// <returns>User entity if credentials are valid, null otherwise</returns>
    Task<User?> ValidateCredentialsAsync(string username, string password);

    /// <summary>
    /// Verifies if a password matches the stored hash.
    /// </summary>
    /// <param name="password">Plain text password</param>
    /// <param name="hash">Stored password hash</param>
    /// <returns>True if password matches hash</returns>
    bool VerifyPassword(string password, string hash);

    /// <summary>
    /// Hashes a password for storage.
    /// </summary>
    /// <param name="password">Plain text password</param>
    /// <returns>Hashed password</returns>
    string HashPassword(string password);
}
