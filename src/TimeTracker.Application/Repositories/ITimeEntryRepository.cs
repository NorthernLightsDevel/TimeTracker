using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Repositories;

public interface ITimeEntryRepository
{
    Task<TimeEntryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TimeEntryDto> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> GetByProjectAsync(
        Guid projectId,
        DateTime? rangeStartUtc = null,
        DateTime? rangeEndUtc = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TimeEntryDto>> GetByLocalDateAsync(
        DateOnly localDate,
        CancellationToken cancellationToken = default);

    Task<TimeEntryDto> GetMostRecentAsync(CancellationToken cancellationToken = default);

    Task<TimeEntryDto> CreateAsync(TimeEntryCreateDto dto, CancellationToken cancellationToken = default);

    Task<TimeEntryDto> UpdateAsync(TimeEntryUpdateDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
