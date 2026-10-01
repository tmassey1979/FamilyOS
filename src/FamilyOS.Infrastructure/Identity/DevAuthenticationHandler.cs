using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FamilyOS.Infrastructure.Identity;

/// <summary>
/// Development-only auth: header <c>X-Dev-User</c> = Keycloak subject (seed external ids).
/// Example: X-Dev-User: terry.owner
/// Enabled when Auth:UseDevBypass=true or environment is Development.
/// </summary>
public sealed class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevBypass";
    public const string HeaderName = "X-Dev-User";

    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values))
            return Task.FromResult(AuthenticateResult.NoResult());

        var sub = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sub))
            return Task.FromResult(AuthenticateResult.Fail("Empty X-Dev-User header."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim("sub", sub),
            new Claim(ClaimTypes.Name, sub),
            new Claim("preferred_username", sub)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
