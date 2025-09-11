using System.Data.Common;
using DbApp.Infrastructure;
using DbApp.Infrastructure.DataSeedings;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Respawn;

namespace DbApp.Tests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    public ApplicationDbContext DbContext { get; }
    public string DatabaseName { get; }
    private Respawner _respawner = null!;
    private DbConnection _connection = null!;

    public DatabaseFixture()
    {
        // Use InMemory database for integration tests to avoid Oracle dependency
        DatabaseName = $"TestDb_{Guid.NewGuid()}";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(DatabaseName)
            // Note: InMemory database doesn't support Oracle-specific extensions
            // like UseEnumCheckConstraints() and UseValidationCheckConstraints()
            .Options;

        DbContext = new ApplicationDbContext(options);
    }

    public async Task ResetDatabaseAsync()
    {
        // For InMemory database, we can simply clear the context and re-seed
        DbContext.ChangeTracker.Clear();

        // Remove all entities
        DbContext.RemoveRange(DbContext.Users);
        DbContext.RemoveRange(DbContext.Roles);
        DbContext.RemoveRange(DbContext.Visitors);
        DbContext.RemoveRange(DbContext.Employees);
        await DbContext.SaveChangesAsync();

        // Re-seed the database
        await DataSeeding.SeedDataAsync(DbContext);
    }

    public async Task InitializeAsync()
    {
        // For InMemory database, ensure it's created and seeded
        await DbContext.Database.EnsureCreatedAsync();

        // Seed initial data
        await DataSeeding.SeedDataAsync(DbContext);

        // For InMemory database, we don't need Respawner setup or connection handling
        // _connection = DbContext.Database.GetDbConnection(); // This doesn't work with InMemory
    }

    public async Task DisposeAsync()
    {
        // For InMemory database, just dispose the context
        await DbContext.DisposeAsync();
        // No need to dispose connection for InMemory database
    }
}
