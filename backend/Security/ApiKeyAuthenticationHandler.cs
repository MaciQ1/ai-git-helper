using System.Security.Claims;
using System.Text.Encodings.Web;
using CommitGenerator.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CommitGenerator.Security;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ApiKeyValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SecurityOptions.ApiKeyHeaderName, out var providedKey)
            || providedKey.Count != 1
            || string.IsNullOrWhiteSpace(providedKey[0]))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!validator.TryGetUserName(providedKey[0]!, out var userName))
            return Task.FromResult(AuthenticateResult.Fail("Nieprawidłowy klucz dostępu."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userName),
            new Claim(ClaimTypes.Name, userName)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
