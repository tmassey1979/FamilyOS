using FamilyOS.Application.Interfaces;
using FamilyOS.Infrastructure.Chat;
using FamilyOS.Infrastructure.Notifications;
using FamilyOS.Infrastructure.Google;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Messaging;
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
        services.AddSingleton<NotificationPushInterceptor>();

        services.AddDbContext<FamilyOsDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5432;Database=familyos;Username=familyos;Password=familyos_dev_password",
                npgsql => npgsql.MigrationsAssembly(typeof(FamilyOsDbContext).Assembly.FullName));
            options.AddInterceptors(sp.GetRequiredService<NotificationPushInterceptor>());
        });

        services.AddScoped<IFamilyOsDbContext>(sp => sp.GetRequiredService<FamilyOsDbContext>());
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpContextAccessor();
        services.AddFamilyAuthorization();

        // Default no-op; API host replaces with SignalRFamilyChatNotifier after AddSignalR.
        services.AddSingleton<IFamilyChatNotifier, NullFamilyChatNotifier>();

        services.AddHttpClient("ExpoPush");
        services.AddScoped<IPushNotificationSender, ExpoPushNotificationSender>();

        services.AddHttpClient<IGoogleCalendarClient, GoogleCalendarClient>();
        services.AddHostedService<GoogleCalendarSyncWorker>();

        // Domain events (#51): Rabbit when enabled, else in-memory so CI / local API still boots
        var rabbitEnabled = !string.Equals(
            configuration["RabbitMQ:Enabled"], "false", StringComparison.OrdinalIgnoreCase);

        services.AddMassTransit(x =>
        {
            x.AddConsumer<TaskCompletedConsumer>();

            if (rabbitEnabled)
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
            }
            else
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        services.AddScoped<IDomainEventBus, MassTransitDomainEventBus>();

        return services;
    }
}
