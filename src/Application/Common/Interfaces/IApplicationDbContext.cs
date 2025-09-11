using DbApp.Domain.Entities.UserSystem;
using Microsoft.EntityFrameworkCore;

namespace DbApp.Application.Common.Interfaces;

/// <summary>
/// Interface for the application database context.
/// Provides access to database sets without coupling to Infrastructure layer.
/// </summary>
public interface IApplicationDbContext
{
    /// <summary>
    /// Users database set.
    /// </summary>
    DbSet<User> Users { get; }

    /// <summary>
    /// Roles database set.
    /// </summary>
    DbSet<Role> Roles { get; }

    /// <summary>
    /// Saves all changes made in this context to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of state entries written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
