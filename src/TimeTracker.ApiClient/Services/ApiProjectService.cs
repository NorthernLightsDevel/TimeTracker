using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using TimeTracker.ApiClient.Internal;
using TimeTracker.Application.Services;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.ApiClient.Services;

internal sealed class ApiProjectService : ApiClientBase, IProjectService
{
    public ApiProjectService(TimeTrackerApiHttpClient apiHttpClient)
        : base(apiHttpClient)
    {
    }

    public async Task<IReadOnlyList<ProjectDto>> GetProjectsByCustomerAsync(
        Guid customerId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/projects?customerId={customerId:D}&includeInactive={(includeInactive ? "true" : "false")}";
        using var response = await HttpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var projects = await response.Content.ReadFromJsonAsync<IReadOnlyList<ProjectDto>>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return projects ?? Array.Empty<ProjectDto>();
    }

    public async Task<IReadOnlyList<ProjectListItemDto>> GetProjectsAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var path = $"api/projects/list?includeInactive={(includeInactive ? "true" : "false")}";
        using var response = await HttpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var projects = await response.Content.ReadFromJsonAsync<IReadOnlyList<ProjectListItemDto>>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return projects ?? Array.Empty<ProjectListItemDto>();
    }
}
