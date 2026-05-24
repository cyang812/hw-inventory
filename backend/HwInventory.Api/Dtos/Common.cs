namespace HwInventory.Api.Dtos;

/// <summary>
/// Uniform list-response shape used by every list endpoint, regardless of transport.
/// </summary>
public record ListResponse<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);

public record ErrorResponse(string Error, string? Detail = null);

public record LinkBody(string? Role = null);

/// <summary>
/// Common pagination + sort parameters parsed off the query string by REST endpoints
/// and translated 1:1 into the equivalent gRPC <c>ListXxxRequest</c> message.
/// </summary>
public record ListQuery(int Limit = 50, int Offset = 0, string? Sort = null, string? Q = null)
{
    public const int MaxLimit = 200;

    public ListQuery Normalise()
    {
        var lim = Limit <= 0 ? 50 : Math.Min(Limit, MaxLimit);
        var off = Math.Max(0, Offset);
        return this with { Limit = lim, Offset = off };
    }
}
