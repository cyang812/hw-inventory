using HwInventory.Api.Data;
using HwInventory.Api.Dtos;
using HwInventory.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HwInventory.Api.Services;

public class HardwareConfigService(AppDbContext db, ActivityService activities)
{
    public async Task<ListResponse<HardwareConfigDto>> ListAsync(int hardwareId, HardwareConfigListFilter f, CancellationToken ct)
    {
        if (!await db.Hardware.IgnoreQueryFilters().AnyAsync(h => h.Id == hardwareId, ct))
            throw new NotFoundException($"hardware {hardwareId}");

        var query = db.HardwareConfigs.AsNoTracking().Where(c => c.HardwareId == hardwareId);
        if (f.Kind is HardwareConfigKind k) query = query.Where(c => c.Kind == k);
        if (f.Current is bool cur) query = query.Where(c => c.IsCurrent == cur);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.IsCurrent)
            .ThenByDescending(c => c.InstalledAt ?? c.CreatedAt)
            .Skip(f.Offset).Take(f.Limit)
            .ToListAsync(ct);
        return new ListResponse<HardwareConfigDto>(items.Select(Mapping.ToDto).ToList(), total, f.Limit, f.Offset);
    }

    public async Task<HardwareConfigDto> RecordAsync(int hardwareId, HardwareConfigCreateDto dto, CancellationToken ct)
    {
        var hw = await db.Hardware.IgnoreQueryFilters().FirstOrDefaultAsync(h => h.Id == hardwareId, ct)
            ?? throw new NotFoundException($"hardware {hardwareId}");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ValidationException("name is required");

        // Enforce "only one IsCurrent per (HardwareId, Kind)" — clear previous current.
        if (dto.IsCurrent)
        {
            var prevCurrents = await db.HardwareConfigs
                .Where(c => c.HardwareId == hardwareId && c.Kind == dto.Kind && c.IsCurrent)
                .ToListAsync(ct);
            foreach (var p in prevCurrents) p.IsCurrent = false;
        }

        Activity? activity = null;
        if (dto.LogActivity)
        {
            // Use the service so timestamps + recompute logic stay centralised.
            var meta = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = dto.Kind.ToString().ToLowerInvariant(),
                name = dto.Name,
                version = dto.Version,
            }));
            var actDto = await activities.CreateAsync(hardwareId, new ActivityCreateDto(
                ActivityKind.Flashed,
                $"Recorded {dto.Kind.ToString().ToLowerInvariant()} {dto.Name}{(dto.Version is null ? "" : $" v{dto.Version}")}",
                meta.RootElement,
                dto.InstalledAt), ct);
            activity = await db.Activities.FirstOrDefaultAsync(a => a.Id == actDto.Id, ct);
        }

        var entity = new HardwareConfig
        {
            HardwareId = hardwareId,
            Kind = dto.Kind,
            Name = dto.Name,
            Version = dto.Version,
            Notes = dto.Notes,
            InstalledAt = dto.InstalledAt,
            IsCurrent = dto.IsCurrent,
            ActivityId = activity?.Id,
        };
        db.HardwareConfigs.Add(entity);
        await db.SaveChangesAsync(ct);
        return Mapping.ToDto(entity);
    }

    public async Task DeleteAsync(int hardwareId, int configId, CancellationToken ct)
    {
        var c = await db.HardwareConfigs.FirstOrDefaultAsync(x => x.Id == configId && x.HardwareId == hardwareId, ct)
            ?? throw new NotFoundException($"hardware config {configId}");
        db.HardwareConfigs.Remove(c);
        await db.SaveChangesAsync(ct);
    }
}

public class LoanService(AppDbContext db)
{
    public async Task<ListResponse<LoanDto>> ListAsync(int hardwareId, CancellationToken ct)
    {
        if (!await db.Hardware.IgnoreQueryFilters().AnyAsync(h => h.Id == hardwareId, ct))
            throw new NotFoundException($"hardware {hardwareId}");

        var rows = await db.Loans.AsNoTracking()
            .Where(l => l.HardwareId == hardwareId)
            .OrderByDescending(l => l.LoanedAt)
            .ToListAsync(ct);
        return new ListResponse<LoanDto>(rows.Select(Mapping.ToDto).ToList(), rows.Count, rows.Count, 0);
    }

    public async Task<LoanDto> StartAsync(int hardwareId, LoanCreateDto dto, CancellationToken ct)
    {
        var hw = await db.Hardware.IgnoreQueryFilters().FirstOrDefaultAsync(h => h.Id == hardwareId, ct)
            ?? throw new NotFoundException($"hardware {hardwareId}");
        if (string.IsNullOrWhiteSpace(dto.LoanedTo)) throw new ValidationException("loanedTo is required");

        var openLoan = await db.Loans.AnyAsync(l => l.HardwareId == hardwareId && l.ReturnedAt == null, ct);
        if (openLoan) throw new ConflictException("hardware already has an open loan");

        var loan = new Loan
        {
            HardwareId = hardwareId,
            LoanedTo = dto.LoanedTo.Trim(),
            LoanedAt = dto.LoanedAt ?? DateTimeOffset.UtcNow,
            DueAt = dto.DueAt,
            Notes = dto.Notes,
        };
        db.Loans.Add(loan);
        if (hw.Status != HardwareStatus.Archived) hw.Status = HardwareStatus.Loaned;
        await db.SaveChangesAsync(ct);
        return Mapping.ToDto(loan);
    }

    public async Task<LoanDto> UpdateAsync(int hardwareId, int loanId, LoanUpdateDto dto, CancellationToken ct)
    {
        var loan = await db.Loans.FirstOrDefaultAsync(l => l.Id == loanId && l.HardwareId == hardwareId, ct)
            ?? throw new NotFoundException($"loan {loanId}");

        if (dto.DueAt is not null) loan.DueAt = dto.DueAt;
        if (dto.Notes is not null) loan.Notes = dto.Notes;
        if (dto.ClearReturned)
        {
            loan.ReturnedAt = null;
        }
        else if (dto.ReturnedAt is not null)
        {
            loan.ReturnedAt = dto.ReturnedAt;
        }

        await db.SaveChangesAsync(ct);

        // If we just closed the only open loan, drop the Hardware back to Available.
        if (loan.ReturnedAt is not null)
        {
            var stillOpen = await db.Loans.AnyAsync(l => l.HardwareId == hardwareId && l.ReturnedAt == null, ct);
            if (!stillOpen)
            {
                var hw = await db.Hardware.IgnoreQueryFilters().FirstOrDefaultAsync(h => h.Id == hardwareId, ct);
                if (hw is not null && hw.Status == HardwareStatus.Loaned)
                {
                    hw.Status = HardwareStatus.Available;
                    await db.SaveChangesAsync(ct);
                }
            }
        }

        return Mapping.ToDto(loan);
    }

    public async Task<LoanDto> ReturnAsync(int hardwareId, int loanId, DateTimeOffset? returnedAt, CancellationToken ct) =>
        await UpdateAsync(hardwareId, loanId, new LoanUpdateDto(ReturnedAt: returnedAt ?? DateTimeOffset.UtcNow), ct);
}
