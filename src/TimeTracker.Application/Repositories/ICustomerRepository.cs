using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Repositories;

public interface ICustomerRepository
{
    Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(CustomerCreateDto dto, CancellationToken cancellationToken = default);

    Task<CustomerDto> UpdateAsync(CustomerUpdateDto dto, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
