using DbApp.Application.Common.Interfaces;
using DbApp.Infrastructure;
using DbApp.Infrastructure.DependencyInjection;
using DbApp.Presentation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DbApp.Tests.Fixtures;

public class TestApiFactory(DatabaseFixture fixture) : WebApplicationFactory<Program>
{
    private readonly DatabaseFixture _fixture = fixture;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureServices((context, services) =>
        {
            // Remove existing DbContext registrations
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IApplicationDbContext>();

            // Remove Redis cache registration to avoid connection issues
            services.RemoveAll<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>();
            services.RemoveAll<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();

            // Register the test database context using the shared database from fixture
            services.AddScoped<ApplicationDbContext>(provider =>
            {
                // Use the same database name as the fixture to share data
                var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(_fixture.DatabaseName)
                    .Options;
                return new ApplicationDbContext(options);
            });

            // Register IApplicationDbContext interface
            services.AddScoped<IApplicationDbContext>(provider =>
                provider.GetRequiredService<ApplicationDbContext>());

            // Clear any existing MediatR registrations to avoid conflicts
            services.RemoveAll<MediatR.IMediator>();
            services.RemoveAll<MediatR.ISender>();
            services.RemoveAll<MediatR.IPublisher>();

            // Register MediatR with all command handlers
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(DbApp.Application.IMediatorModule).Assembly);
            });

            // Register authentication services (but skip MediatR registration since we already did it)
            RegisterAuthenticationServicesForTest(services);
        });
    }

    private static void RegisterAuthenticationServicesForTest(IServiceCollection services)
    {
        // Register domain services
        services.AddScoped<DbApp.Domain.Services.UserSystem.IAuthenticationService, DbApp.Infrastructure.Services.UserSystem.AuthenticationService>();
        services.AddScoped<DbApp.Domain.Services.UserSystem.IJwtTokenService, DbApp.Infrastructure.Services.UserSystem.JwtTokenService>();

        // Use a test cache service that doesn't require Redis
        services.AddScoped<DbApp.Domain.Services.UserSystem.ICacheService, TestCacheService>();

        // Configure in-memory cache for tests (instead of Redis)
        services.AddMemoryCache();

        // Register AutoMapper profiles
        services.AddAutoMapper(cfg => cfg.AddProfile<DbApp.Application.UserSystem.Authentication.AuthenticationMappingProfile>());

        // Configure test-specific authentication (simplified for testing)
        ConfigureTestAuthentication(services);
    }

    private static void ConfigureTestAuthentication(IServiceCollection services)
    {
        // Use simplified test authentication scheme that bypasses JWT validation
        services.AddAuthentication("Test")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", options => { });

        services.AddAuthorization();
    }
}
