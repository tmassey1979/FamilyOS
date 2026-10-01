using System.Net;
using System.Text.Json;
using FamilyOS.Application.Common;

namespace FamilyOS.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception ex)
    {
        var (status, title, detail) = Map(ex);

        _logger.LogError(ex, "Unhandled exception {StatusCode} {Method} {Path}",
            status, context.Request.Method, context.Request.Path);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.com/{(int)status}",
            ["title"] = title,
            ["status"] = (int)status,
            ["detail"] = detail,
            ["instance"] = context.Request.Path.Value,
            ["traceId"] = context.TraceIdentifier
        };

        if (_env.IsDevelopment())
            problem["exception"] = ex.GetType().Name;

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }

    private static (HttpStatusCode status, string title, string detail) Map(Exception ex) => ex switch
    {
        NotFoundException nfe => (HttpStatusCode.NotFound, "Not Found", nfe.Message),
        ForbiddenException fe => (HttpStatusCode.Forbidden, "Forbidden", fe.Message),
        ValidationException ve => (HttpStatusCode.BadRequest, "Validation Error", ve.Message),
        DomainException de => (HttpStatusCode.Conflict, "Domain Rule Violation", de.Message),
        UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized", "Authentication required."),
        _ => (HttpStatusCode.InternalServerError, "Internal Server Error",
            "An unexpected error occurred. Reference the traceId when contacting support.")
    };
}
