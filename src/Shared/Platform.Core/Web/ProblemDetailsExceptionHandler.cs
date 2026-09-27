using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Platform.Core.Web;

/// <summary>
/// Converts exceptions into RFC 7807 problem responses (TETA SRS §39). Expected platform exceptions
/// map to 4xx with their message; anything else becomes a generic 500 — stack traces, SQL and
/// secrets are never returned to the client, only logged server-side with the correlation ID.
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;
    private readonly string _typeBaseUri;

    public ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger, ProblemTypeOptions options)
    {
        _logger = logger;
        _typeBaseUri = options.TypeBaseUri.TrimEnd('/');
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdMiddleware.Get(httpContext) ?? httpContext.TraceIdentifier;
        var problem = Map(exception);
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["correlationId"] = correlationId;

        if (problem.Status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception; correlation {CorrelationId}", correlationId);
        }
        else
        {
            _logger.LogInformation("Request rejected with {Status} ({Code}): {Message}; correlation {CorrelationId}",
                problem.Status, (exception as PlatformException)?.Code, exception.Message, correlationId);
        }

        httpContext.Response.StatusCode = problem.Status ?? 500;
        // Serialise using the runtime type so ValidationProblemDetails keeps its "errors" member.
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json", cancellationToken);
        return true;
    }

    private ProblemDetails Map(Exception exception)
    {
        switch (exception)
        {
            case ValidationException v:
                var vp = new ValidationProblemDetails(v.Errors.ToDictionary(k => k.Key, k => k.Value))
                {
                    Type = $"{_typeBaseUri}/validation",
                    Title = "Validation failed",
                    Status = StatusCodes.Status400BadRequest
                };
                return vp;
            case DomainException d:
                return WithCode(new ProblemDetails
                {
                    Type = $"{_typeBaseUri}/business-rule",
                    Title = "Business rule violated",
                    Detail = d.Message,
                    Status = StatusCodes.Status422UnprocessableEntity
                }, d.Code);
            case ForbiddenException f:
                return WithCode(new ProblemDetails
                {
                    Type = $"{_typeBaseUri}/forbidden",
                    Title = "Forbidden",
                    Detail = f.Message,
                    Status = StatusCodes.Status403Forbidden
                }, f.Code);
            case NotFoundException n:
                return new ProblemDetails
                {
                    Type = $"{_typeBaseUri}/not-found",
                    Title = "Not found",
                    Detail = n.Message,
                    Status = StatusCodes.Status404NotFound
                };
            case ConflictException c:
                return WithCode(new ProblemDetails
                {
                    Type = $"{_typeBaseUri}/conflict",
                    Title = "Conflict",
                    Detail = c.Message,
                    Status = StatusCodes.Status409Conflict
                }, c.Code);
            default:
                // EF Core optimistic concurrency failures are recognised by type name so this shared
                // library does not need a dependency on EF Core.
                if (exception.GetType().Name == "DbUpdateConcurrencyException")
                {
                    return new ProblemDetails
                    {
                        Type = $"{_typeBaseUri}/concurrency",
                        Title = "Record changed by another user",
                        Detail = "The record was modified by someone else after you loaded it. Reload and try again.",
                        Status = StatusCodes.Status409Conflict
                    };
                }

                return new ProblemDetails
                {
                    Type = $"{_typeBaseUri}/server-error",
                    Title = "An unexpected error occurred",
                    Detail = "The request could not be completed. Quote the correlation ID when contacting support.",
                    Status = StatusCodes.Status500InternalServerError
                };
        }
    }

    private static ProblemDetails WithCode(ProblemDetails problem, string? code)
    {
        if (!string.IsNullOrEmpty(code)) problem.Extensions["code"] = code;
        return problem;
    }
}

public sealed class ProblemTypeOptions
{
    public string TypeBaseUri { get; set; } = "https://errors.platform.local";
}

public static class ProblemDetailsExtensions
{
    public static IServiceCollection AddPlatformProblemDetails(this IServiceCollection services, string typeBaseUri)
    {
        services.AddSingleton(new ProblemTypeOptions { TypeBaseUri = typeBaseUri });
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }
}
