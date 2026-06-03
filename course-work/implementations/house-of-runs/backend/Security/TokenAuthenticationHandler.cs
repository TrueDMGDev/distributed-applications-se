using System.Text.Encodings.Web;
using HouseOfRuns.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HouseOfRuns.Api.Security;

public sealed class TokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TokenService tokenService)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorization["Bearer ".Length..].Trim();
        var principal = tokenService.ValidateToken(token);
        return Task.FromResult(principal is null
            ? AuthenticateResult.Fail("Invalid bearer token.")
            : AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
