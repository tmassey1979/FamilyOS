using System.Text.Json.Serialization;
using FamilyOS.Api.Middleware;
using FamilyOS.Application.Interfaces;
using FamilyOS.Application.Tasks;
using FamilyOS.Infrastructure;
using FamilyOS.Infrastructure.Identity;
using FamilyOS.Infrastructure.Persistence;
using FamilyOS.Infrastructure.Seed;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "FamilyOS.Api")
        .WriteTo.Console());

    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(TaskCommandHandlers).Assembly));

    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "Family OS API", Version = "v1" });
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme.",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });
        c.AddSecurityDefinition("DevBypass", new OpenApiSecurityScheme
        {
            Description = "Dev only: X-Dev-User header (e.g. terry.owner).",
            Name = DevAuthenticationHandler.HeaderName,
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var keycloakAuthority = builder.Configuration["Keycloak:Authority"]
        ?? "http://localhost:8080/realms/familyos";

    var useDevBypass = builder.Environment.IsDevelopment()
        || string.Equals(builder.Configuration["Auth:UseDevBypass"], "true", StringComparison.OrdinalIgnoreCase);

    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        if (useDevBypass)
        {
            options.DefaultAuthenticateScheme = "Smart";
            options.DefaultChallengeScheme = "Smart";
        }
        else
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }
    });

    authBuilder.AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username"
        };
    });

    if (useDevBypass)
    {
        authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
            DevAuthenticationHandler.SchemeName, _ => { });

        authBuilder.AddPolicyScheme("Smart", "JWT or DevBypass", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey(DevAuthenticationHandler.HeaderName))
                    return DevAuthenticationHandler.SchemeName;
                return JwtBearerDefaults.AuthenticationScheme;
            };
        });
    }

    builder.Services.AddAuthorization();
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));
    builder.Services.AddSignalR();

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("API process is running"))
        .AddCheck<PostgresHealthCheck>("postgres");

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, http) =>
        {
            diag.Set("RequestHost", http.Request.Host.Value);
            diag.Set("UserAgent", http.Request.Headers.UserAgent.ToString());
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseMiddleware<FamilyContextMiddleware>();
    app.UseAuthorization();
    app.MapControllers();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = r => r.Name == "self",
        ResponseWriter = WriteMinimalHealth
    });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        ResponseWriter = WriteDetailedHealth
    });

    app.MapGet("/", () => Results.Redirect("/swagger"));

    // Dev helper: who am I (family context)
    app.MapGet("/api/me", async (ICurrentUserService current) =>
    {
        await current.EnsureLoadedAsync();
        if (!current.IsAuthenticated)
            return Results.Unauthorized();
        return Results.Ok(new
        {
            current.ExternalIdentityId,
            current.UserId,
            current.FamilyId,
            current.MemberId,
            Role = current.Role?.ToString()
        });
    }).RequireAuthorization();

    var seedEnabled = app.Environment.IsDevelopment()
        || string.Equals(app.Configuration["Seed:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    if (seedEnabled)
    {
        try { await SeedData.InitializeAsync(app.Services); }
        catch (Exception ex) { Log.Warning(ex, "Seed failed — ensure Postgres is up."); }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static Task WriteMinimalHealth(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "text/plain";
    return ctx.Response.WriteAsync(report.Status == HealthStatus.Healthy ? "Healthy" : report.Status.ToString());
}

static async Task WriteDetailedHealth(HttpContext ctx, HealthReport report)
{
    ctx.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        entries = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds
            })
    };
    await ctx.Response.WriteAsJsonAsync(payload);
}

/// <summary>Checks Postgres connectivity via EF Core.</summary>
file sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PostgresHealthCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<FamilyOsDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Postgres reachable")
                : HealthCheckResult.Unhealthy("Postgres not reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres check failed", ex);
        }
    }
}

public partial class Program { }
