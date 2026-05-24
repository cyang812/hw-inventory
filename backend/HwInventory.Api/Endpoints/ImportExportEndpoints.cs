using HwInventory.Api.Services;

namespace HwInventory.Api.Endpoints;

public static class ImportExportEndpoints
{
    public static IEndpointRouteBuilder MapImportExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/export/json", async (ImportExportService svc, CancellationToken ct) =>
        {
            var json = await svc.ExportJsonAsync(ct);
            return Results.Text(json, "application/json");
        }).WithTags("ImportExport");

        app.MapPost("/api/import/json", async (HttpRequest req, ImportExportService svc, CancellationToken ct) =>
        {
            var imported = await svc.ImportJsonAsync(req.Body, ct);
            return Results.Ok(new { imported });
        }).WithTags("ImportExport");

        app.MapGet("/api/export/csv", async (HttpRequest req, ImportExportService svc, CancellationToken ct) =>
        {
            var entity = QueryHelpers.ParseString(req, "entity") ?? "hardware";
            var csv = await svc.ExportCsvAsync(entity, ct);
            return Results.Text(csv, "text/csv");
        }).WithTags("ImportExport");

        return app;
    }
}
