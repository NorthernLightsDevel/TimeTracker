using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.ApiClient.Internal;
using TimeTracker.Application.Repositories;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.ApiClient.Repositories;

internal sealed class ApiCustomerRepository : ApiClientBase, ICustomerRepository
{
    public ApiCustomerRepository(TimeTrackerApiHttpClient apiHttpClient)
        : base(apiHttpClient)
    {
    }

    public async Task<CustomerDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync($"api/customers/{id:D}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<CustomerDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CustomerDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync("api/customers", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var customers = await response.Content.ReadFromJsonAsync<IReadOnlyList<CustomerDto>>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return customers ?? Array.Empty<CustomerDto>();
    }

    public async Task<CustomerDto> CreateAsync(CustomerCreateDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.PostAsJsonAsync("api/customers", dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CustomerDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (created is null)
        {
            var error = await ReadErrorAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(error);
        }

        return created;
    }

    public async Task<CustomerDto> UpdateAsync(CustomerUpdateDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.PutAsJsonAsync($"api/customers/{dto.Id:D}", dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<CustomerDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            var error = await ReadErrorAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(error);
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.DeleteAsync($"api/customers/{id:D}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
