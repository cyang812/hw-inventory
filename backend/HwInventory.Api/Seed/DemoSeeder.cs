using System.Text.Json;
using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using HwInventory.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Seed;

/// <summary>
/// Populates the DB with ~10 hardware items, 2-3 projects, sample activities,
/// firmware history, and an open loan so the dashboard and dev UI have content
/// out of the box. Idempotent: only seeds when the DB is empty.
/// </summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var activities = sp.GetRequiredService<ActivityService>();
        var configs = sp.GetRequiredService<HardwareConfigService>();
        var loans = sp.GetRequiredService<LoanService>();

        if (await db.Hardware.IgnoreQueryFilters().AnyAsync())
        {
            Console.WriteLine("Seed: DB already has hardware, skipping.");
            return;
        }

        // ---------- Categories ----------
        var mcu = new Category { Name = "MCU", Slug = "mcu", Description = "Microcontroller units" };
        var sbc = new Category { Name = "SBC", Slug = "sbc", Description = "Single-board computers" };
        var laptop = new Category { Name = "Laptop", Slug = "laptop" };
        var phone = new Category { Name = "Phone", Slug = "phone" };
        var ssd = new Category { Name = "SSD", Slug = "ssd" };
        var devKit = new Category { Name = "Dev Kit", Slug = "dev-kit" };
        var armA72 = new Category { Name = "ARM Cortex-A72", Slug = "arm-cortex-a72" };
        var riscv = new Category { Name = "RISC-V RV32IMAC", Slug = "riscv-rv32imac" };
        var xtensa = new Category { Name = "Xtensa LX7", Slug = "xtensa-lx7" };
        var x86 = new Category { Name = "x86_64", Slug = "x86_64" };
        var armM4 = new Category { Name = "ARM Cortex-M4", Slug = "arm-cortex-m4" };

        db.Categories.AddRange(mcu, sbc, laptop, phone, ssd, devKit, armA72, riscv, xtensa, x86, armM4);

        // ---------- Tags ----------
        var hobby = new Tag { Name = "hobby", Color = "#5b9bd5" };
        var work = new Tag { Name = "work", Color = "#ed7d31" };
        var gpio = new Tag { Name = "gpio" };
        var lowPower = new Tag { Name = "low-power" };
        var lowLevel = new Tag { Name = "low-level" };
        db.Tags.AddRange(hobby, work, gpio, lowPower, lowLevel);
        await db.SaveChangesAsync();

        // ---------- Hardware ----------
        var rpi4 = new Hardware
        {
            Name = "Raspberry Pi 4B",
            Manufacturer = "Raspberry Pi Foundation",
            Model = "4 Model B",
            SerialNumber = "10000000abcd1234",
            Sku = "RPI4-4G",
            Location = "shelf A",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Specs = JsonDocument.Parse("""{"cpu":"BCM2711","ram_gb":4,"storage_gb":32,"wifi":true,"bt":true}"""),
            Links = JsonDocument.Parse("""[{"label":"Datasheet","url":"https://datasheets.raspberrypi.com/rpi4/raspberry-pi-4-product-brief.pdf","kind":"datasheet"}]"""),
            AcquiredAt = DateTimeOffset.UtcNow.AddYears(-3),
            Notes = "Lab pi — runs k3s sometimes.",
            Categories = new List<Category> { sbc, armA72 },
            Tags = new List<Tag> { hobby, gpio },
        };

        var nrf52 = new Hardware
        {
            Name = "nRF52840 DK",
            Manufacturer = "Nordic Semiconductor",
            Model = "PCA10056",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Specs = JsonDocument.Parse("""{"cpu":"Cortex-M4","flash_kb":1024,"ram_kb":256,"radios":["BLE","Thread","Zigbee"]}"""),
            Categories = new List<Category> { mcu, devKit, armM4 },
            Tags = new List<Tag> { hobby, lowPower },
        };

        var esp32s3 = new Hardware
        {
            Name = "ESP32-S3-DevKitC",
            Manufacturer = "Espressif",
            Model = "ESP32-S3-DevKitC-1",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Specs = JsonDocument.Parse("""{"cpu":"Xtensa LX7 dual-core","flash_mb":8,"psram_mb":8}"""),
            Categories = new List<Category> { mcu, devKit, xtensa },
            Tags = new List<Tag> { hobby },
        };

        var rv32 = new Hardware
        {
            Name = "ESP32-C3 SuperMini",
            Manufacturer = "Generic",
            Model = "ESP32-C3",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Specs = JsonDocument.Parse("""{"cpu":"RISC-V RV32IMAC","flash_mb":4}"""),
            Categories = new List<Category> { mcu, riscv },
            Tags = new List<Tag> { hobby, lowPower },
        };

        var xps = new Hardware
        {
            Name = "Dell XPS 13 9320",
            Manufacturer = "Dell",
            Model = "XPS 13 9320",
            AssetTag = "DELL-001",
            SerialNumber = "1234abcd",
            Status = HardwareStatus.InUse,
            Condition = HardwareCondition.Working,
            Specs = JsonDocument.Parse("""{"cpu":"i7-1260P","ram_gb":16,"ssd_gb":512}"""),
            Location = "daily-driver",
            Categories = new List<Category> { laptop, x86 },
            Tags = new List<Tag> { work },
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-1),
            LastActivityAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        var thinkpad = new Hardware
        {
            Name = "ThinkPad T480",
            Manufacturer = "Lenovo",
            Model = "T480",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Location = "drawer B",
            Categories = new List<Category> { laptop, x86 },
            Tags = new List<Tag> { hobby },
        };

        var pixel = new Hardware
        {
            Name = "Pixel 6",
            Manufacturer = "Google",
            Model = "Pixel 6",
            Identifiers = JsonDocument.Parse("""{"imei":"351111000000000"}"""),
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Categories = new List<Category> { phone },
            Tags = new List<Tag> { hobby },
        };

        var iphoneSe = new Hardware
        {
            Name = "iPhone SE (2020)",
            Manufacturer = "Apple",
            Model = "A2275",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Partial,
            Notes = "Battery is shot.",
            Categories = new List<Category> { phone },
            Tags = new List<Tag> { hobby },
        };

        var samsungSsd = new Hardware
        {
            Name = "Samsung 870 EVO 1TB",
            Manufacturer = "Samsung",
            Model = "MZ-77E1T0",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Categories = new List<Category> { ssd },
        };

        var stm32 = new Hardware
        {
            Name = "STM32F4 Discovery",
            Manufacturer = "STMicroelectronics",
            Model = "STM32F407G-DISC1",
            Status = HardwareStatus.Available,
            Condition = HardwareCondition.Working,
            Specs = JsonDocument.Parse("""{"cpu":"Cortex-M4","flash_kb":1024,"ram_kb":192}"""),
            Categories = new List<Category> { mcu, devKit, armM4 },
            Tags = new List<Tag> { hobby, lowLevel },
        };

        db.Hardware.AddRange(rpi4, nrf52, esp32s3, rv32, xps, thinkpad, pixel, iphoneSe, samsungSsd, stm32);

        // ---------- Projects ----------
        var homeAssistant = new Project
        {
            Title = "Home Assistant on Pi",
            Slug = "home-assistant",
            Description = "Run Home Assistant OS on the Raspberry Pi 4.",
            Status = ProjectStatus.InProgress,
            Priority = ProjectPriority.Medium,
            StartedAt = DateTimeOffset.UtcNow.AddMonths(-2),
        };
        var zephyrPort = new Project
        {
            Title = "Zephyr BLE peripheral",
            Slug = "zephyr-ble",
            Description = "Build a BLE peripheral on the nRF52840 with Zephyr.",
            Status = ProjectStatus.Planned,
            Priority = ProjectPriority.High,
        };
        var oldPhoneOs = new Project
        {
            Title = "Reuse old phones",
            Slug = "old-phones",
            Description = "Find an OS to reuse old Android / iPhone hardware as kiosks.",
            Status = ProjectStatus.Idea,
            Priority = ProjectPriority.Low,
        };
        db.Projects.AddRange(homeAssistant, zephyrPort, oldPhoneOs);
        await db.SaveChangesAsync();

        // ---------- Links between hardware and projects ----------
        db.HardwareProjects.AddRange(
            new HardwareProject { HardwareId = rpi4.Id, ProjectId = homeAssistant.Id, Role = "host" },
            new HardwareProject { HardwareId = nrf52.Id, ProjectId = zephyrPort.Id, Role = "target" },
            new HardwareProject { HardwareId = pixel.Id, ProjectId = oldPhoneOs.Id },
            new HardwareProject { HardwareId = iphoneSe.Id, ProjectId = oldPhoneOs.Id });
        await db.SaveChangesAsync();

        // ---------- Activities (via service, so LastUsedAt / LastActivityAt are populated correctly) ----------
        await activities.CreateAsync(rpi4.Id, new ActivityCreateDto(
            ActivityKind.Flashed, "Flashed Raspberry Pi OS Lite",
            JsonDocument.Parse("""{"os":"Raspberry Pi OS Lite","version":"2024-07-04"}""").RootElement,
            DateTimeOffset.UtcNow.AddMonths(-2)), default);

        await activities.CreateAsync(rpi4.Id, new ActivityCreateDto(
            ActivityKind.Used, "Powered up for Home Assistant test",
            null, DateTimeOffset.UtcNow.AddDays(-15)), default);

        await activities.CreateAsync(nrf52.Id, new ActivityCreateDto(
            ActivityKind.Used, "Bench session: GPIO + UART",
            null, DateTimeOffset.UtcNow.AddDays(-40)), default);

        await activities.CreateAsync(xps.Id, new ActivityCreateDto(
            ActivityKind.Used, "Daily-driver session", null,
            DateTimeOffset.UtcNow.AddDays(-1)), default);

        await activities.CreateAsync(thinkpad.Id, new ActivityCreateDto(
            ActivityKind.Moved, "Moved from desk drawer to drawer B",
            JsonDocument.Parse("""{"from":"desk drawer","to":"drawer B"}""").RootElement,
            DateTimeOffset.UtcNow.AddDays(-200)), default);

        await activities.CreateAsync(stm32.Id, new ActivityCreateDto(
            ActivityKind.Inspected, "Inventory check", null,
            DateTimeOffset.UtcNow.AddDays(-100)), default);

        await activities.CreateAsync(rv32.Id, new ActivityCreateDto(
            ActivityKind.Note, "Bought from AliExpress for project research", null,
            DateTimeOffset.UtcNow.AddMonths(-3)), default);

        // ---------- Firmware history ----------
        await configs.RecordAsync(rpi4.Id, new HardwareConfigCreateDto(
            HardwareConfigKind.Os, "Raspberry Pi OS Lite", "2024-07-04",
            "First flash", DateTimeOffset.UtcNow.AddMonths(-2), IsCurrent: true, LogActivity: false), default);

        // ---------- Open loan ----------
        await loans.StartAsync(samsungSsd.Id, new LoanCreateDto(
            "Alice", DateTimeOffset.UtcNow.AddDays(-20),
            DateTimeOffset.UtcNow.AddDays(-5),
            "Cloning her laptop"), default);

        Console.WriteLine($"Seed: {await db.Hardware.IgnoreQueryFilters().CountAsync()} hardware, " +
                          $"{await db.Projects.IgnoreQueryFilters().CountAsync()} projects, " +
                          $"{await db.Activities.CountAsync()} activities, " +
                          $"{await db.Loans.CountAsync()} loans.");
    }
}
