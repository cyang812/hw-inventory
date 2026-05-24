using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using Microsoft.AspNetCore.Http;

namespace HwInventory.Api.Endpoints;

/// <summary>
/// Small helpers used by REST route handlers to parse list query parameters in
/// the same way across endpoints (pagination, sort, comma- and repeat-style
/// multi-values, enum parsing, time filters).
/// </summary>
public static class QueryHelpers
{
    public static int ParseInt(HttpRequest req, string name, int fallback)
    {
        var v = req.Query[name].FirstOrDefault();
        return int.TryParse(v, out var n) ? n : fallback;
    }

    public static bool ParseBool(HttpRequest req, string name, bool fallback)
    {
        var v = req.Query[name].FirstOrDefault();
        return bool.TryParse(v, out var b) ? b : fallback;
    }

    public static string? ParseString(HttpRequest req, string name)
    {
        var v = req.Query[name].FirstOrDefault();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    public static DateTimeOffset? ParseDate(HttpRequest req, string name)
    {
        var v = req.Query[name].FirstOrDefault();
        return DateTimeOffset.TryParse(v, out var d) ? d : null;
    }

    public static List<int>? ParseIntList(HttpRequest req, string name)
    {
        var values = req.Query[name];
        if (values.Count == 0) return null;
        var ints = new List<int>();
        foreach (var raw in values)
        {
            if (raw is null) continue;
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(part, out var n)) ints.Add(n);
            }
        }
        return ints.Count == 0 ? null : ints;
    }

    public static TEnum? ParseEnum<TEnum>(HttpRequest req, string name) where TEnum : struct
    {
        var v = req.Query[name].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(v)) return null;
        // Accept both PascalCase and lowercase forms.
        return Enum.TryParse<TEnum>(v, ignoreCase: true, out var e) ? e : null;
    }

    public static (int Limit, int Offset, string? Sort, string? Q) ParseList(HttpRequest req)
    {
        var q = new ListQuery(
            ParseInt(req, "limit", 50),
            ParseInt(req, "offset", 0),
            ParseString(req, "sort"),
            ParseString(req, "q")).Normalise();
        return (q.Limit, q.Offset, q.Sort, q.Q);
    }
}
