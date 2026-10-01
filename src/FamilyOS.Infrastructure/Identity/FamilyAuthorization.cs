using FamilyOS.Application.Interfaces;
using FamilyOS.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyOS.Infrastructure.Identity;

public static class FamilyAuthPolicies
{
    public const string FamilyMember = "FamilyMember";
    public const string AdultOrOwner = "AdultOrOwner";
    public const string OwnerOnly = "OwnerOnly";
}

public sealed class FamilyMemberRequirement : IAuthorizationRequirement { }

public sealed class MinRoleRequirement : IAuthorizationRequirement
{
    public FamilyRole MinRole { get; }
    public MinRoleRequirement(FamilyRole minRole) => MinRole = minRole;
}

public sealed class FamilyMemberHandler : AuthorizationHandler<FamilyMemberRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public FamilyMemberHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, FamilyMemberRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var current = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        await current.EnsureLoadedAsync();

        if (current.FamilyId.HasValue && current.MemberId.HasValue)
            context.Succeed(requirement);
    }
}

public sealed class MinRoleHandler : AuthorizationHandler<MinRoleRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MinRoleHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, MinRoleRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var current = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        await current.EnsureLoadedAsync();

        if (!current.Role.HasValue) return;

        // Owner > Adult > Teen > Child
        if (RoleRank(current.Role.Value) >= RoleRank(requirement.MinRole))
            context.Succeed(requirement);
    }

    private static int RoleRank(FamilyRole role) => role switch
    {
        FamilyRole.Owner => 4,
        FamilyRole.Adult => 3,
        FamilyRole.Teen => 2,
        FamilyRole.Child => 1,
        _ => 0
    };
}

public static class FamilyAuthorizationExtensions
{
    public static IServiceCollection AddFamilyAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, FamilyMemberHandler>();
        services.AddSingleton<IAuthorizationHandler, MinRoleHandler>();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(FamilyAuthPolicies.FamilyMember, p =>
                p.RequireAuthenticatedUser().AddRequirements(new FamilyMemberRequirement()));

            options.AddPolicy(FamilyAuthPolicies.AdultOrOwner, p =>
                p.RequireAuthenticatedUser().AddRequirements(new MinRoleRequirement(FamilyRole.Adult)));

            options.AddPolicy(FamilyAuthPolicies.OwnerOnly, p =>
                p.RequireAuthenticatedUser().AddRequirements(new MinRoleRequirement(FamilyRole.Owner)));

            // Default: authenticated is enough; controllers opt into FamilyMember
            options.FallbackPolicy = null;
        });

        return services;
    }
}
