using System.Net;
using HwInventory.Api.Dtos;
using HwInventory.Api.Services;

namespace HwInventory.Api.Endpoints;

/// <summary>
/// One central place that maps domain exceptions to HTTP problem responses. The
/// gRPC and MCP transports apply equivalent mappings using the same exception types.
/// </summary>
public class DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (NotFoundException ex)
        {
            await Write(ctx, HttpStatusCode.NotFound, "not_found", ex.Message);
        }
        catch (ConflictException ex)
        {
            await Write(ctx, HttpStatusCode.Conflict, "conflict", ex.Message);
        }
        catch (ValidationException ex)
        {
            await Write(ctx, HttpStatusCode.BadRequest, "invalid_argument", ex.Message);
        }
        catch (PreconditionFailedException ex)
        {
            await Write(ctx, (HttpStatusCode)422, "failed_precondition", ex.Message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception");
            await Write(ctx, HttpStatusCode.InternalServerError, "internal", "internal error");
        }
    }

    private static Task Write(HttpContext ctx, HttpStatusCode code, string error, string detail)
    {
        ctx.Response.StatusCode = (int)code;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsJsonAsync(new ErrorResponse(error, detail));
    }
}
