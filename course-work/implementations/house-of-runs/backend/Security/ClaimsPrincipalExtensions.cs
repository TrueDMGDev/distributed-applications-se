using System.Security.Claims;

namespace HouseOfRuns.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole("Admin");

    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("Authenticated user id is missing.");
    }
}
