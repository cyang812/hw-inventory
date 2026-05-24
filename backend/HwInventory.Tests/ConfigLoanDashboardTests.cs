using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using HwInventory.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HwInventory.Tests;

public class ConfigLoanTests
{
    [Fact]
    public async Task RecordingNewCurrentConfig_ClearsPreviousCurrentForSameKind()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var configs = sp.GetRequiredService<HardwareConfigService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("rpi"), default);
        var first = await configs.RecordAsync(hw.Id, new HardwareConfigCreateDto(
            HardwareConfigKind.Os, "RPi OS Lite", "2024-07-04", null, null, IsCurrent: true, LogActivity: false), default);
        var second = await configs.RecordAsync(hw.Id, new HardwareConfigCreateDto(
            HardwareConfigKind.Os, "Ubuntu Server", "24.04", null, null, IsCurrent: true, LogActivity: false), default);

        var list = await configs.ListAsync(hw.Id, new HardwareConfigListFilter(Kind: HardwareConfigKind.Os), default);
        Assert.Equal(2, list.Total);

        var refreshed = list.Items.ToDictionary(c => c.Id);
        Assert.False(refreshed[first.Id].IsCurrent);
        Assert.True(refreshed[second.Id].IsCurrent);
    }

    [Fact]
    public async Task RecordFirmware_LogsAFlashedActivity_AndBumpsLastUsedAt()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var configs = sp.GetRequiredService<HardwareConfigService>();
        var activities = sp.GetRequiredService<ActivityService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("dev"), default);
        await configs.RecordAsync(hw.Id, new HardwareConfigCreateDto(
            HardwareConfigKind.Firmware, "Zephyr", "3.7",
            null, new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero), IsCurrent: true, LogActivity: true), default);

        var list = await activities.ListAsync(new ActivityListFilter(HardwareId: hw.Id), default);
        Assert.Contains(list.Items, a => a.Kind == ActivityKind.Flashed);

        var detail = await hardware.GetAsync(hw.Id, false, default);
        Assert.NotNull(detail.LastUsedAt);
    }

    [Fact]
    public async Task StartLoan_FlipsStatusToLoaned_ReturnFlipsBackToAvailable()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var loans = sp.GetRequiredService<LoanService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("ssd"), default);
        var loan = await loans.StartAsync(hw.Id, new LoanCreateDto("Alice"), default);
        Assert.Equal(HardwareStatus.Loaned, (await hardware.GetAsync(hw.Id, false, default)).Status);

        await loans.ReturnAsync(hw.Id, loan.Id, null, default);
        Assert.Equal(HardwareStatus.Available, (await hardware.GetAsync(hw.Id, false, default)).Status);
    }

    [Fact]
    public async Task StartingSecondLoan_WhileOneOpen_Throws()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var loans = sp.GetRequiredService<LoanService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("ssd"), default);
        await loans.StartAsync(hw.Id, new LoanCreateDto("Alice"), default);
        await Assert.ThrowsAsync<ConflictException>(() => loans.StartAsync(hw.Id, new LoanCreateDto("Bob"), default));
    }
}

public class DashboardTests
{
    [Fact]
    public async Task Suggestions_ExcludesItemsLinkedToActiveProjects()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var projects = sp.GetRequiredService<ProjectService>();
        var activities = sp.GetRequiredService<ActivityService>();
        var dashboard = sp.GetRequiredService<DashboardService>();

        var idleWithProject = await hardware.CreateAsync(new HardwareCreateDto("with-proj"), default);
        var idleAlone = await hardware.CreateAsync(new HardwareCreateDto("idle-alone"), default);

        await activities.CreateAsync(idleWithProject.Id, new ActivityCreateDto(ActivityKind.Used, null, null, DateTimeOffset.UtcNow.AddDays(-200)), default);
        await activities.CreateAsync(idleAlone.Id, new ActivityCreateDto(ActivityKind.Used, null, null, DateTimeOffset.UtcNow.AddDays(-200)), default);

        var pr = await projects.CreateAsync(new ProjectCreateDto("Active", Status: ProjectStatus.InProgress), default);
        await hardware.LinkProjectAsync(idleWithProject.Id, pr.Id, null, default);

        var sugg = await dashboard.ListSuggestionsAsync(90, 50, 0, default);
        Assert.Contains(sugg.Items, i => i.Id == idleAlone.Id);
        Assert.DoesNotContain(sugg.Items, i => i.Id == idleWithProject.Id);
    }
}
