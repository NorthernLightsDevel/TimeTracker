using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Domain.Dtos;
using TimeTracker.Domain.Entities;
using TimeTracker.Persistence;

namespace TimeTracker.Application.Repositories;

public sealed class TimeEntryRepository(TimeTrackerDbContext dbContext) : ITimeEntryRepository
{
    private readonly TimeTrackerDbContext _dbContext = dbContext;

    public async Task<TimeEntryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TimeEntries.AsNoTracking()
            .FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<TimeEntryDto> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TimeEntries.AsNoTracking()
            .Where(entry => entry.EndUtc == null && !entry.IsDeleted)
            .OrderByDescending(entry => entry.StartUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> GetByProjectAsync(
        Guid projectId,
        DateTime? rangeStartUtc = null,
        DateTime? rangeEndUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.TimeEntries.AsNoTracking()
            .Where(entry => entry.ProjectId == projectId);

        if (rangeStartUtc.HasValue)
        {
            query = query.Where(entry => entry.StartUtc >= rangeStartUtc.Value);
        }

        if (rangeEndUtc.HasValue)
        {
            query = query.Where(entry => entry.StartUtc <= rangeEndUtc.Value);
        }

        var entries = await query
            .OrderBy(entry => entry.StartUtc)
            .ToListAsync(cancellationToken);

        return entries.ConvertAll(ToDto);
    }

    public async Task<IReadOnlyList<TimeEntryDto>> GetByLocalDateAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local);
        var endExclusive = startOfDay.AddDays(1);

        var entries = await _dbContext.TimeEntries.AsNoTracking()
            .Where(entry => !entry.IsDeleted && entry.StartLocal >= startOfDay && entry.StartLocal < endExclusive)
            .OrderBy(entry => entry.StartLocal)
            .ToListAsync(cancellationToken);

        return entries.ConvertAll(ToDto);
    }

    public async Task<TimeEntryDto> GetMostRecentAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.TimeEntries.AsNoTracking()
            .Where(entry => !entry.IsDeleted)
            .OrderByDescending(entry => entry.StartUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<TimeEntryDto> CreateAsync(TimeEntryCreateDto dto, CancellationToken cancellationToken = default)
    {
        var entry = new TimeEntry(dto.CustomerId, dto.ProjectId, dto.StartLocal, dto.StartUtc);

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            entry.UpdateNotes(dto.Notes);
        }

        if (!dto.Billable)
        {
            entry.SetBillable(false);
        }

        if (!string.IsNullOrWhiteSpace(dto.Tag))
        {
            entry.SetTag(dto.Tag);
        }

        _dbContext.TimeEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entry);
    }

    public async Task<TimeEntryDto> UpdateAsync(TimeEntryUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.TimeEntries
            .FirstOrDefaultAsync(e => e.Id == dto.Id, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        if (dto.StartLocal.HasValue && dto.StartUtc.HasValue)
        {
            entry.AdjustStart(dto.StartLocal.Value, dto.StartUtc.Value);
        }

        if (dto.EndLocal.HasValue && dto.EndUtc.HasValue)
        {
            entry.Stop(dto.EndLocal.Value, dto.EndUtc.Value);
        }

        if (dto.Notes is not null)
        {
            entry.UpdateNotes(dto.Notes);
        }

        if (dto.Billable.HasValue)
        {
            entry.SetBillable(dto.Billable.Value);
        }

        if (dto.Tag is not null)
        {
            entry.SetTag(dto.Tag);
        }

        if (dto.IsDeleted.HasValue)
        {
            if (dto.IsDeleted.Value)
            {
                entry.MarkDeleted();
            }
            else
            {
                entry.Restore();
            }
        }

        if (dto.PendingSync.HasValue)
        {
            if (dto.PendingSync.Value)
            {
                entry.MarkPendingSync();
            }
            else
            {
                entry.MarkSynced(dto.ServerId);
            }
        }
        else if (dto.ServerId is not null)
        {
            entry.MarkSynced(dto.ServerId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(entry);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _dbContext.TimeEntries
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (entry is null)
        {
            return false;
        }

        _dbContext.TimeEntries.Remove(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static TimeEntryDto ToDto(TimeEntry entry) => new(
        entry.Id,
        entry.CustomerId,
        entry.ProjectId,
        entry.StartLocal,
        entry.EndLocal,
        entry.StartUtc,
        entry.EndUtc,
        entry.Notes,
        entry.Billable,
        entry.Tag ?? string.Empty,
        entry.ServerId,
        entry.PendingSync,
        entry.IsDeleted,
        entry.LastModifiedUtc,
        entry.RowVersion is null ? null : entry.RowVersion.ToArray());
}
