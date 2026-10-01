using FamilyOS.Application.Interfaces;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FamilyOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FamilyOsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=familyos;Username=familyos;Password=familyos_dev_password",
                npgsql => npgsql.MigrationsAssembly(typeof(FamilyOsDbContext).Assembly.FullName)));

        services.AddScoped<IFamilyOsDbContext>(sp => sp.GetRequiredService<FamilyOsDbContext>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();
        services.AddFamilyAuthorization();

        // MassTransit + RabbitMQ
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbitHost = configuration["RabbitMQ:Host"] ?? "localhost";
                var rabbitUser = configuration["RabbitMQ:Username"] ?? "familyos";
                var rabbitPass = configuration["RabbitMQ:Password"] ?? "familyos_dev_password";

                cfg.Host(rabbitHost, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
