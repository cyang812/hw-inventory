using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HwInventory.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HwInventory.Tests;

/// <summary>
/// Spins up the full ASP.NET Core pipeline against a fresh in-memory SQLite DB so
/// REST integration tests exercise the same middleware (auth, exception mapping,
/// JSON serialization) end users hit. The override drops the registered
/// AppDbContext and installs a new one bound to a unique in-memory connection.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "test_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Drop the AppDbContext + its options registered by Program.cs and install our own.
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            // file:NAME?mode=memory&cache=shared lets the same DB be reached by all scopes.
            var conn = $"DataSource=file:{_dbName}?mode=memory&cache=shared";
            // Open a connection that lives as long as the factory so the DB persists.
            var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(conn);
            keepAlive.Open();
            services.AddSingleton(keepAlive);

            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlite(keepAlive).UseSnakeCaseNamingConvention());
        });
    }

    public async Task EnsureSchemaAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}

public class RestSmokeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public RestSmokeTests(ApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureSchemaAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Health_Returns200()
    {
        var client = _factory.CreateClient();
        var r = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task HardwareLifecycle_Create_List_Patch_Archive()
    {
        var client = _factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/hardware", new { name = "REST item", status = "available", condition = "working" });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/hardware");
        Assert.True(list.GetProperty("total").GetInt32() >= 1);

        // JSON Patch — change /name
        var patchBody = JsonSerializer.Serialize(new[]
        {
            new { op = "replace", path = "/name", value = "REST item (patched)" }
        });
        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/hardware/{id}")
        {
            Content = new StringContent(patchBody, Encoding.UTF8, "application/json"),
        };
        var patchResp = await client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, patchResp.StatusCode);
        var patched = await patchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("REST item (patched)", patched.GetProperty("name").GetString());

        // Soft delete
        var del = await client.DeleteAsync($"/api/hardware/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var listAfter = await client.GetFromJsonAsync<JsonElement>("/api/hardware");
        Assert.DoesNotContain(listAfter.GetProperty("items").EnumerateArray(),
            e => e.GetProperty("id").GetInt32() == id);

        var listIncludingArchived = await client.GetFromJsonAsync<JsonElement>("/api/hardware?include_archived=true");
        Assert.Contains(listIncludingArchived.GetProperty("items").EnumerateArray(),
            e => e.GetProperty("id").GetInt32() == id);
    }

    [Fact]
    public async Task ProjectLink_PutTwice_IsIdempotent_NoConflict()
    {
        var client = _factory.CreateClient();
        var hw = await (await client.PostAsJsonAsync("/api/hardware", new { name = "for-link" })).Content.ReadFromJsonAsync<JsonElement>();
        var pr = await (await client.PostAsJsonAsync("/api/projects", new { title = "Linker" })).Content.ReadFromJsonAsync<JsonElement>();

        var hid = hw.GetProperty("id").GetInt32();
        var pid = pr.GetProperty("id").GetInt32();

        var first = await client.PutAsJsonAsync($"/api/hardware/{hid}/projects/{pid}", new { role = "host" });
        var second = await client.PutAsJsonAsync($"/api/hardware/{hid}/projects/{pid}", new { role = "controller" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/hardware/{hid}");
        var projects = detail.GetProperty("projects").EnumerateArray().ToList();
        Assert.Single(projects);
        Assert.Equal("controller", projects[0].GetProperty("role").GetString());
    }

    [Fact]
    public async Task PatchAllowList_RejectsUnknownPath()
    {
        var client = _factory.CreateClient();
        var hw = await (await client.PostAsJsonAsync("/api/hardware", new { name = "patch-test" })).Content.ReadFromJsonAsync<JsonElement>();
        var id = hw.GetProperty("id").GetInt32();

        var patchBody = JsonSerializer.Serialize(new[]
        {
            new { op = "replace", path = "/createdAt", value = "2020-01-01T00:00:00Z" }
        });
        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/hardware/{id}")
        {
            Content = new StringContent(patchBody, Encoding.UTF8, "application/json"),
        };
        var resp = await client.SendAsync(patch);
        Assert.Equal((HttpStatusCode)422, resp.StatusCode);
    }
}
