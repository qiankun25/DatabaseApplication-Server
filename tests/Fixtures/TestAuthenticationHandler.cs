using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace DbApp.Tests.Fixtures;

/// <summary>
/// Simple test authentication handler that bypasses JWT validation for integration tests.
/// </summary>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // For tests, we'll create a simple authenticated user
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim("UserId", "1"),
            new Claim("Username", "testuser")
        };

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
