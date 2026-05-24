using System.Net;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using HwInventory.Api.Config;
using HwInventory.Api.Data;
using HwInventory.Api.Grpc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HwInventory.Tests;

/// <summary>
/// Mirrors <see cref="ApiFactory"/> but lets each test fix an <c>AuthToken</c> so the
/// bearer middleware can be exercised. Each fixture instance gets a private in-memory
/// SQLite DB so tests don't share state.
/// </summary>
public class GrpcApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = "grpc_" + Guid.NewGuid().ToString("N");
    public string? AuthToken { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(AppDbContext));

            var conn = $"DataSource=file:{_dbName}?mode=memory&cache=shared";
            var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(conn);
            keepAlive.Open();
            services.AddSingleton(keepAlive);
            services.AddDbContext<AppDbContext>(o =>
                o.UseSqlite(keepAlive).UseSnakeCaseNamingConvention());

            // Replace the HwInventoryOptions singleton (built from env vars in Program.cs
            // before ConfigureServices runs) so each test can fix its own AuthToken without
            // mutating process-wide state.
            services.RemoveAll(typeof(HwInventoryOptions));
            services.AddSingleton(new HwInventoryOptions { AuthToken = AuthToken ?? "", IdleDefaultDays = 90 });
        });
    }

    protected override void Dispose(bool disposing) => base.Dispose(disposing);

    /// <summary>
    /// Build a <see cref="GrpcChannel"/> that targets the in-process <c>TestServer</c>.
    /// The TestServer's <c>CreateHandler</c> supports HTTP/2 (required by gRPC), and the
    /// per-call <c>Authorization</c> header is set by callers via <see cref="CallOptions"/>
    /// or <see cref="CallCredentials"/>.
    /// </summary>
    public GrpcChannel CreateGrpcChannel() =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = Server.CreateHandler(),
        });
}

public class GrpcSmokeTests
{
    [Fact]
    public async Task Check_NoAuthConfigured_ReturnsOk()
    {
        using var factory = new GrpcApiFactory();
        using var channel = factory.CreateGrpcChannel();
        var client = new HealthService.HealthServiceClient(channel);

        var reply = await client.CheckAsync(new Empty());

        Assert.Equal("ok", reply.Status);
    }

    [Fact]
    public async Task Check_WithValidBearer_ReturnsOk()
    {
        using var factory = new GrpcApiFactory { AuthToken = "test-token-xyz" };
        using var channel = factory.CreateGrpcChannel();
        var client = new HealthService.HealthServiceClient(channel);

        var headers = new Metadata { { "Authorization", "Bearer test-token-xyz" } };
        var reply = await client.CheckAsync(new Empty(), headers);

        Assert.Equal("ok", reply.Status);
    }

    [Fact]
    public async Task Check_WithoutBearer_Rejected()
    {
        using var factory = new GrpcApiFactory { AuthToken = "test-token-xyz" };
        using var channel = factory.CreateGrpcChannel();
        var client = new HealthService.HealthServiceClient(channel);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.CheckAsync(new Empty()).ResponseAsync);

        // Middleware short-circuits with HTTP 401 before the gRPC framing layer runs,
        // which surfaces as StatusCode.Unknown / Internal in the client (depending on
        // ASP.NET Core version). Either way the call must fail.
        Assert.NotEqual(StatusCode.OK, ex.StatusCode);
    }

    [Fact]
    public async Task Check_WithBadBearer_Rejected()
    {
        using var factory = new GrpcApiFactory { AuthToken = "test-token-xyz" };
        using var channel = factory.CreateGrpcChannel();
        var client = new HealthService.HealthServiceClient(channel);

        var headers = new Metadata { { "Authorization", "Bearer wrong-token" } };
        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            client.CheckAsync(new Empty(), headers).ResponseAsync);

        Assert.NotEqual(StatusCode.OK, ex.StatusCode);
    }

    /// <summary>
    /// The proto declares 7 services beyond Health (Category, Tag, Hardware, Project,
    /// Activity, Dashboard, ImportExport) but only HealthService is registered with
    /// <c>MapGrpcService&lt;T&gt;()</c>. Calling any of the others must surface as
    /// <see cref="StatusCode.Unimplemented"/> — clients can rely on this until the
    /// services land. When phase 3 wires one up, replace its case here with a real
    /// behavioural test.
    /// </summary>
    [Fact]
    public async Task DeclaredButUnregisteredServices_ReturnUnimplemented()
    {
        using var factory = new GrpcApiFactory();
        using var channel = factory.CreateGrpcChannel();

        var category = new CategoryService.CategoryServiceClient(channel);
        var tag = new TagService.TagServiceClient(channel);
        var hardware = new HardwareService.HardwareServiceClient(channel);
        var project = new ProjectService.ProjectServiceClient(channel);
        var activity = new ActivityService.ActivityServiceClient(channel);
        var dashboard = new DashboardService.DashboardServiceClient(channel);
        var importExport = new ImportExportService.ImportExportServiceClient(channel);

        async Task AssertUnimplemented(Func<Task> call, string label)
        {
            var ex = await Assert.ThrowsAsync<RpcException>(call);
            Assert.True(
                ex.StatusCode == StatusCode.Unimplemented,
                $"{label}: expected Unimplemented, got {ex.StatusCode} ({ex.Status.Detail})");
        }

        await AssertUnimplemented(() => category.ListAsync(new ListCategoriesRequest()).ResponseAsync, "CategoryService.List");
        await AssertUnimplemented(() => tag.ListAsync(new ListTagsRequest()).ResponseAsync, "TagService.List");
        await AssertUnimplemented(() => hardware.ListAsync(new ListHardwareRequest()).ResponseAsync, "HardwareService.List");
        await AssertUnimplemented(() => project.ListAsync(new ListProjectsRequest()).ResponseAsync, "ProjectService.List");
        await AssertUnimplemented(() => activity.ListAsync(new ListActivitiesRequest()).ResponseAsync, "ActivityService.List");
        await AssertUnimplemented(() => dashboard.GetStatsAsync(new Empty()).ResponseAsync, "DashboardService.GetStats");
        await AssertUnimplemented(() => importExport.ExportJsonAsync(new Empty()).ResponseAsync, "ImportExportService.ExportJson");
    }
}
