using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Services;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> GetProjectsByCustomerAsync(
        Guid customerId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectListItemDto>> GetProjectsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);
}
