using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.ApiClient.Internal;
using TimeTracker.Application.Repositories;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.ApiClient.Repositories;

internal sealed class ApiProjectRepository : ApiClientBase, IProjectRepository
{
    public ApiProjectRepository(TimeTrackerApiHttpClient apiHttpClient)
        : base(apiHttpClient)
    {
    }

    public async Task<ProjectDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync($"api/projects/{id:D}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProjectDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProjectDto>> GetByCustomerAsync(Guid customerId, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var path = $"api/projects?customerId={customerId:D}&includeInactive={(includeInactive ? "true" : "false")}";
        using var response = await HttpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var projects = await response.Content.ReadFromJsonAsync<IReadOnlyList<ProjectDto>>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return projects ?? Array.Empty<ProjectDto>();
    }

    public async Task<ProjectDto> CreateAsync(ProjectCreateDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.PostAsJsonAsync("api/projects", dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<ProjectDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (created is null)
        {
            var error = await ReadErrorAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(error);
        }

        return created;
    }

    public async Task<ProjectDto> UpdateAsync(ProjectUpdateDto dto, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.PutAsJsonAsync($"api/projects/{dto.Id:D}", dto, SerializerOptions, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<ProjectDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            var error = await ReadErrorAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(error);
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.DeleteAsync($"api/projects/{id:D}", cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }
}
