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

public sealed class ProjectRepository(TimeTrackerDbContext dbContext) : IProjectRepository
{
    private readonly TimeTrackerDbContext _dbContext = dbContext;

    public async Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Projects.AsNoTracking()
            .FirstOrDefaultAsync(project => project.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<ProjectDto>> GetByCustomerAsync(
        Guid customerId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Projects.AsNoTracking()
            .Where(project => project.CustomerId == customerId);

        if (!includeInactive)
        {
            query = query.Where(project => project.IsActive);
        }

        var projects = await query
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);

        return projects.ConvertAll(ToDto);
    }

    public async Task<ProjectDto> CreateAsync(ProjectCreateDto dto, CancellationToken cancellationToken = default)
    {
        var project = new Project(dto.CustomerId, dto.Name);

        if (!dto.IsActive)
        {
            project.SetActive(false);
        }

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    public async Task<ProjectDto> UpdateAsync(ProjectUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(p => p.Id == dto.Id, cancellationToken);

        if (project is null)
        {
            return null;
        }

        project.Rename(dto.Name);
        project.SetActive(dto.IsActive);

        if (project.CustomerId != dto.CustomerId)
        {
            var customer = await _dbContext.Customers
                .FirstOrDefaultAsync(c => c.Id == dto.CustomerId, cancellationToken);

            if (customer is null)
            {
                return null;
            }

            project.AttachCustomer(customer);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects
            .Include(p => p.TimeEntries)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project is null)
        {
            return false;
        }

        _dbContext.Projects.Remove(project);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static ProjectDto ToDto(Project project) => new(
        project.Id,
        project.CustomerId,
        project.Name,
        project.IsActive,
        project.CreatedUtc,
        project.LastModifiedUtc);
}
