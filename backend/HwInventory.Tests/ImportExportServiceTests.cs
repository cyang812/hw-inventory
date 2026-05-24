using System.Text;
using HwInventory.Api.Data;
using HwInventory.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HwInventory.Tests;

public class ImportExportServiceTests
{
    [Fact]
    public async Task ImportJson_AcceptsCategoryIdsAndTagIds_AndLinksHardware()
    {
        var (sp, db) = TestScope.Build();
        var svc = sp.GetRequiredService<ImportExportService>();

        var json = """
        {
          "categories": [
            { "id": 10, "name": "SBC", "slug": "sbc" },
            { "id": 11, "name": "Sensors", "slug": "sensors" }
          ],
          "tags": [
            { "id": 20, "name": "lab" }
          ],
          "hardware": [
            {
              "id": 100,
              "name": "Pi 5",
              "manufacturer": "Raspberry Pi",
              "condition": "Working",
              "status": "Available",
              "categoryIds": [10, 11],
              "tagIds": [20]
            }
          ]
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var imported = await svc.ImportJsonAsync(stream, CancellationToken.None);

        Assert.Equal(1, imported);
        var hw = await db.Hardware
            .Include(h => h.Categories)
            .Include(h => h.Tags)
            .SingleAsync();
        Assert.Equal(2, hw.Categories.Count);
        Assert.Contains(hw.Categories, c => c.Slug == "sbc");
        Assert.Contains(hw.Categories, c => c.Slug == "sensors");
        var tag = Assert.Single(hw.Tags);
        Assert.Equal("lab", tag.Name);
    }

    [Fact]
    public async Task ImportJson_AcceptsNestedCategoriesAndTags_FromExportRoundTrip()
    {
        var (sp, db) = TestScope.Build();
        var svc = sp.GetRequiredService<ImportExportService>();

        // This is the shape /api/export/json emits: categories/tags as nested objects on hardware.
        var json = """
        {
          "categories": [
            { "id": 1, "name": "SBC", "slug": "sbc" }
          ],
          "tags": [
            { "id": 2, "name": "lab" }
          ],
          "hardware": [
            {
              "id": 100,
              "name": "Pi 5",
              "condition": "Working",
              "status": "Available",
              "categories": [ { "id": 1, "name": "SBC", "slug": "sbc" } ],
              "tags": [ { "id": 2, "name": "lab" } ]
            }
          ]
        }
        """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await svc.ImportJsonAsync(stream, CancellationToken.None);

        var hw = await db.Hardware
            .Include(h => h.Categories)
            .Include(h => h.Tags)
            .SingleAsync();
        Assert.Single(hw.Categories);
        Assert.Single(hw.Tags);
        Assert.Equal("sbc", hw.Categories.First().Slug);
        Assert.Equal("lab", hw.Tags.First().Name);
    }

    [Fact]
    public async Task ImportJson_RoundTripsThroughExport_PreservesCategoryAndTagLinks()
    {
        // Seed scope: create the data through the import path first, then export it.
        string dump;
        {
            var (sp, _) = TestScope.Build();
            var svc = sp.GetRequiredService<ImportExportService>();
            var seed = """
            {
              "categories": [{ "id": 1, "name": "SBC", "slug": "sbc" }],
              "tags": [{ "id": 2, "name": "lab" }],
              "hardware": [{
                "id": 100, "name": "Pi 5", "condition": "Working", "status": "Available",
                "categoryIds": [1], "tagIds": [2]
              }]
            }
            """;
            using var s = new MemoryStream(Encoding.UTF8.GetBytes(seed));
            await svc.ImportJsonAsync(s, CancellationToken.None);
            dump = await svc.ExportJsonAsync(CancellationToken.None);
        }

        // Fresh scope = fresh DB + fresh change tracker, simulating a real
        // "export from one instance, import into another" workflow.
        var (sp2, db2) = TestScope.Build();
        var svc2 = sp2.GetRequiredService<ImportExportService>();
        using (var s = new MemoryStream(Encoding.UTF8.GetBytes(dump)))
            await svc2.ImportJsonAsync(s, CancellationToken.None);

        var hw = await db2.Hardware
            .Include(h => h.Categories)
            .Include(h => h.Tags)
            .SingleAsync();
        Assert.Single(hw.Categories);
        Assert.Single(hw.Tags);
        Assert.Equal("sbc", hw.Categories.First().Slug);
        Assert.Equal("lab", hw.Tags.First().Name);
    }

    [Fact]
    public async Task ImportJson_UnknownCategoryAndTagIds_AreSilentlyDropped()
    {
        var (sp, db) = TestScope.Build();
        var svc = sp.GetRequiredService<ImportExportService>();

        var json = """
        {
          "categories": [{ "id": 1, "name": "SBC", "slug": "sbc" }],
          "hardware": [{
            "id": 100, "name": "Pi 5", "condition": "Working", "status": "Available",
            "categoryIds": [1, 999], "tagIds": [42]
          }]
        }
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await svc.ImportJsonAsync(stream, CancellationToken.None);

        var hw = await db.Hardware
            .Include(h => h.Categories)
            .Include(h => h.Tags)
            .SingleAsync();
        var cat = Assert.Single(hw.Categories);
        Assert.Equal("sbc", cat.Slug);
        Assert.Empty(hw.Tags);
    }
}
