using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using HwInventory.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HwInventory.Tests;

public class ActivityServiceTests
{
    [Theory]
    [InlineData(ActivityKind.Used, true)]
    [InlineData(ActivityKind.Flashed, true)]
    [InlineData(ActivityKind.Repaired, true)]
    [InlineData(ActivityKind.Measured, true)]
    [InlineData(ActivityKind.Configured, true)]
    [InlineData(ActivityKind.Inspected, false)]
    [InlineData(ActivityKind.Moved, false)]
    [InlineData(ActivityKind.Note, false)]
    public async Task ActivityKind_UpdatesLastUsedAt_PerSpec(ActivityKind kind, bool shouldBumpLastUsed)
    {
        var (sp, db) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var activities = sp.GetRequiredService<ActivityService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("test-hw"), default);

        var when = new DateTimeOffset(2025, 1, 15, 12, 0, 0, TimeSpan.Zero);
        await activities.CreateAsync(hw.Id, new ActivityCreateDto(kind, null, null, when), default);

        var refreshed = await hardware.GetAsync(hw.Id, includeArchived: false, default);
        Assert.Equal(when, refreshed.LastActivityAt);
        if (shouldBumpLastUsed)
            Assert.Equal(when, refreshed.LastUsedAt);
        else
            Assert.Null(refreshed.LastUsedAt);
    }

    [Fact]
    public async Task BackdatedActivity_DoesNotOverwriteNewerLastUsedAt()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var activities = sp.GetRequiredService<ActivityService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("hw"), default);

        var recent = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var older = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await activities.CreateAsync(hw.Id, new ActivityCreateDto(ActivityKind.Used, null, null, recent), default);
        await activities.CreateAsync(hw.Id, new ActivityCreateDto(ActivityKind.Used, null, null, older), default);

        var refreshed = await hardware.GetAsync(hw.Id, false, default);
        Assert.Equal(recent, refreshed.LastUsedAt);
        Assert.Equal(recent, refreshed.LastActivityAt);
    }

    [Fact]
    public async Task BackdatedFutureActivity_DoesBumpLastUsedAt_WhenItIsNewerMaximum()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var activities = sp.GetRequiredService<ActivityService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("hw"), default);
        var older = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        await activities.CreateAsync(hw.Id, new ActivityCreateDto(ActivityKind.Used, null, null, older), default);
        await activities.CreateAsync(hw.Id, new ActivityCreateDto(ActivityKind.Used, null, null, newer), default);

        var refreshed = await hardware.GetAsync(hw.Id, false, default);
        Assert.Equal(newer, refreshed.LastUsedAt);
    }

    [Fact]
    public async Task InspectedActivity_DoesNotBumpLastUsedAt_ButDoesBumpLastActivityAt()
    {
        var (sp, _) = TestScope.Build();
        var hardware = sp.GetRequiredService<HardwareService>();
        var activities = sp.GetRequiredService<ActivityService>();

        var hw = await hardware.CreateAsync(new HardwareCreateDto("hw"), default);
        var when = new DateTimeOffset(2025, 7, 4, 12, 0, 0, TimeSpan.Zero);
        await activities.CreateAsync(hw.Id, new ActivityCreateDto(ActivityKind.Inspected, "shelf check", null, when), default);

        var refreshed = await hardware.GetAsync(hw.Id, false, default);
        Assert.Null(refreshed.LastUsedAt);
        Assert.Equal(when, refreshed.LastActivityAt);
    }
}
