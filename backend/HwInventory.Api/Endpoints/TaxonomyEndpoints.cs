using HwInventory.Api.Dtos;
using HwInventory.Api.Services;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;

namespace HwInventory.Api.Endpoints;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/categories").WithTags("Categories");

        g.MapGet("", async (HttpRequest req, CategoryService svc, CancellationToken ct) =>
        {
            var (limit, offset, _, q) = QueryHelpers.ParseList(req);
            return Results.Ok(await svc.ListAsync(q, limit, offset, ct));
        });

        g.MapGet("{id:int}", async (int id, CategoryService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetAsync(id, ct)));

        g.MapPost("", async ([FromBody] CategoryCreateDto dto, CategoryService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(dto, ct);
            return Results.Created($"/api/categories/{created.Id}", created);
        });

        g.MapPatch("{id:int}", async (int id, [FromBody] CategoryUpdateDto dto, CategoryService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateAsync(id, dto, ct)));

        g.MapDelete("{id:int}", async (int id, CategoryService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}

public static class TagEndpoints
{
    public static IEndpointRouteBuilder MapTagEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/tags").WithTags("Tags");

        g.MapGet("", async (HttpRequest req, TagService svc, CancellationToken ct) =>
        {
            var (limit, offset, _, q) = QueryHelpers.ParseList(req);
            return Results.Ok(await svc.ListAsync(q, limit, offset, ct));
        });

        g.MapPost("", async ([FromBody] TagCreateDto dto, TagService svc, CancellationToken ct) =>
        {
            var created = await svc.CreateAsync(dto, ct);
            return Results.Created($"/api/tags/{created.Id}", created);
        });

        g.MapDelete("{id:int}", async (int id, TagService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        });

        return app;
    }
}
