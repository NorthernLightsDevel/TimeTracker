using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Repositories;

public interface IProjectRepository
{
    Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectDto>> GetByCustomerAsync(
        Guid customerId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ProjectDto> CreateAsync(ProjectCreateDto dto, CancellationToken cancellationToken = default);

    Task<ProjectDto> UpdateAsync(ProjectUpdateDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
