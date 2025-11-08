using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Application.Repositories;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Services;

public sealed class ProjectService : IProjectService
{
    private readonly IProjectRepository _projects;
    private readonly ICustomerRepository _customers;

    public ProjectService(IProjectRepository projects, ICustomerRepository customers)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _customers = customers ?? throw new ArgumentNullException(nameof(customers));
    }

    public Task<IReadOnlyList<ProjectDto>> GetProjectsByCustomerAsync(
        Guid customerId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        return _projects.GetByCustomerAsync(customerId, includeInactive, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> GetProjectsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var customers = await _customers.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var results = new List<ProjectListItemDto>();

        foreach (var customer in customers)
        {
            var entries = await _projects.GetByCustomerAsync(customer.Id, includeInactive, cancellationToken).ConfigureAwait(false);
            foreach (var project in entries)
            {
                if (!includeInactive && !project.IsActive)
                {
                    continue;
                }

                results.Add(new ProjectListItemDto(
                    project.Id,
                    customer.Id,
                    customer.Name ?? "Unassigned",
                    project.Name,
                    project.IsActive));
            }
        }

        return results;
    }
}
