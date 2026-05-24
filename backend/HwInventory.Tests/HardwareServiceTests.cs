using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using HwInventory.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HwInventory.Tests;

public class HardwareServiceTests
{
    [Fact]
    public async Task LinkProject_IsIdempotent()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var projects = sp.GetRequiredService<ProjectService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("hw"), default);
        var pr = await projects.CreateAsync(new ProjectCreateDto("Proj"), default);

        await hardware.LinkProjectAsync(hw.Id, pr.Id, role: "host", default);
        await hardware.LinkProjectAsync(hw.Id, pr.Id, role: null, default); // re-link should not throw
        await hardware.LinkProjectAsync(hw.Id, pr.Id, role: "controller", default); // role update

        var detail = await hardware.GetAsync(hw.Id, includeArchived: false, default);
        var link = Assert.Single(detail.Projects);
        Assert.Equal("controller", link.Role);
    }

    [Fact]
    public async Task SoftDelete_HidesHardwareFromDefaultList_ButShowsWithIncludeArchived()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("hw"), default);
        await hardware.ArchiveAsync(hw.Id, default);

        var listDefault = await hardware.ListAsync(new HardwareListFilter(), default);
        Assert.DoesNotContain(listDefault.Items, i => i.Id == hw.Id);

        var listWithArchived = await hardware.ListAsync(new HardwareListFilter(IncludeArchived: true), default);
        Assert.Contains(listWithArchived.Items, i => i.Id == hw.Id);

        var detail = await hardware.GetAsync(hw.Id, includeArchived: true, default);
        Assert.NotNull(detail.ArchivedAt);
        Assert.Equal(HardwareStatus.Archived, detail.Status);
    }

    [Fact]
    public async Task IdleFilter_OnlyMatchesItemsOlderThanCutoff()
    {
        var (sp, db) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var activities = sp.GetRequiredService<ActivityService>();

        var freshUsage = await hardware.CreateAsync(new HardwareCreateDto("fresh"), default);
        await activities.CreateAsync(freshUsage.Id, new ActivityCreateDto(ActivityKind.Used, null, null, DateTimeOffset.UtcNow.AddDays(-1)), default);

        var oldUsage = await hardware.CreateAsync(new HardwareCreateDto("old"), default);
        await activities.CreateAsync(oldUsage.Id, new ActivityCreateDto(ActivityKind.Used, null, null, DateTimeOffset.UtcNow.AddDays(-200)), default);

        var idle = await hardware.ListAsync(new HardwareListFilter(IdleDays: 90), default);
        Assert.DoesNotContain(idle.Items, i => i.Id == freshUsage.Id);
        Assert.Contains(idle.Items, i => i.Id == oldUsage.Id);
    }

    [Fact]
    public async Task Categories_Multi_AreAndFiltered()
    {
        var (sp, _) = TestScope.Build();
        var categories = sp.GetRequiredService<CategoryService>();
        var hardware = sp.GetRequiredService<HardwareService>();

        var mcu = await categories.CreateAsync(new CategoryCreateDto("MCU", "mcu"), default);
        var rv = await categories.CreateAsync(new CategoryCreateDto("RISC-V", "riscv"), default);

        var both = await hardware.CreateAsync(new HardwareCreateDto("ESP32-C3",
            CategoryIds: new[] { mcu.Id, rv.Id }), default);
        var onlyMcu = await hardware.CreateAsync(new HardwareCreateDto("nRF52",
            CategoryIds: new[] { mcu.Id }), default);

        var bothOnly = await hardware.ListAsync(new HardwareListFilter(CategoryIds: new[] { mcu.Id, rv.Id }), default);
        Assert.Single(bothOnly.Items);
        Assert.Equal(both.Id, bothOnly.Items[0].Id);

        var anyMcu = await hardware.ListAsync(new HardwareListFilter(CategoryIds: new[] { mcu.Id }), default);
        Assert.Equal(2, anyMcu.Total);
    }
}
