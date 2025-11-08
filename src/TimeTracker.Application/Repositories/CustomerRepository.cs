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

public sealed class CustomerRepository(TimeTrackerDbContext dbContext) : ICustomerRepository
{
    private readonly TimeTrackerDbContext _dbContext = dbContext;

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

        return entity is null ? null : ToDto(entity);
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var customers = await _dbContext.Customers.AsNoTracking()
            .OrderBy(customer => customer.Name)
            .ToListAsync(cancellationToken);

        return customers.ConvertAll(ToDto);
    }

    public async Task<CustomerDto> CreateAsync(CustomerCreateDto dto, CancellationToken cancellationToken = default)
    {
        var customer = new Customer(dto.Name);

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(CustomerUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(c => c.Id == dto.Id, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        customer.Rename(dto.Name);
        customer.SetArchived(dto.IsArchived);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(customer);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .Include(c => c.Projects)
            .ThenInclude(p => p.TimeEntries)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (customer is null)
        {
            return false;
        }

        _dbContext.Customers.Remove(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static CustomerDto ToDto(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.IsArchived,
        customer.CreatedUtc,
        customer.LastModifiedUtc);
}
