using HwInventory.Api.Config;
using HwInventory.Api.Dtos;

namespace HwInventory.Api.Endpoints;

/// <summary>
/// Bearer-token middleware. When <c>AuthToken</c> is configured (always required in
/// production), every request to <c>/api</c>, <c>/mcp</c>, gRPC, and Scalar must
/// present a matching <c>Authorization: Bearer ...</c> header. The health endpoint
/// is allow-listed so deployment probes still work.
/// </summary>
public class BearerAuthMiddleware(RequestDelegate next, HwInventoryOptions opts)
{
    private static readonly string[] AllowList =
    [
        "/api/health",
    ];

    public async Task Invoke(HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(opts.AuthToken))
        {
            await next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "";
        if (AllowList.Any(a => string.Equals(a, path, StringComparison.OrdinalIgnoreCase)))
        {
            await next(ctx);
            return;
        }

        // Only guard the documented surface — keep static asset hosting unencumbered.
        var isGuarded =
            path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/mcp", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hwinventory.v1", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/grpc.reflection", StringComparison.OrdinalIgnoreCase);

        if (!isGuarded)
        {
            await next(ctx);
            return;
        }

        var header = ctx.Request.Headers.Authorization.FirstOrDefault();
        if (header is null || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new ErrorResponse("unauthenticated", "missing or malformed Authorization header"));
            return;
        }

        var token = header["Bearer ".Length..].Trim();
        if (!FixedTimeEquals(token, opts.AuthToken))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new ErrorResponse("permission_denied", "invalid token"));
            return;
        }

        await next(ctx);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
