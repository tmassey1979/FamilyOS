using FamilyOS.Application.Interfaces;

namespace FamilyOS.Api.Middleware;

/// <summary>
/// After authentication, resolve User → FamilyMember → Role into ICurrentUserService.
/// </summary>
public sealed class FamilyContextMiddleware
{
    private readonly RequestDelegate _next;

    public FamilyContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true)
            await currentUser.EnsureLoadedAsync(context.RequestAborted);

        await _next(context);
    }
}
