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

internal sealed class ApiTimerSessionService : ApiClientBase, ITimerSessionService
{
    public ApiTimerSessionService(TimeTrackerApiHttpClient apiHttpClient)
        : base(apiHttpClient)
    {
    }

    public async Task<TimerSessionSnapshotDto> GetSnapshotAsync(DateOnly? localDate = null, CancellationToken cancellationToken = default)
    {
        var path = "api/timer/snapshot";
        if (localDate.HasValue)
        {
            path += $"?date={localDate.Value:yyyy-MM-dd}";
        }

        using var response = await HttpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<TimerSessionSnapshotDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Timer snapshot response was empty.");
        }

        return snapshot;
    }

    public async Task<IReadOnlyList<TimerHistoryEntryDto>> GetHistoryAsync(DateOnly localDate, CancellationToken cancellationToken = default)
    {
        var path = $"api/timer/history?date={localDate:yyyy-MM-dd}";
        using var response = await HttpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var entries = await response.Content.ReadFromJsonAsync<IReadOnlyList<TimerHistoryEntryDto>>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return entries ?? Array.Empty<TimerHistoryEntryDto>();
    }

    public async Task<IReadOnlyList<TimerDailySummaryDto>> GetDailySummaryAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var path = $"api/timer/daily-summary?start={startDate:yyyy-MM-dd}&end={endDate:yyyy-MM-dd}";
        using var response = await HttpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var summaries = await response.Content.ReadFromJsonAsync<IReadOnlyList<TimerDailySummaryDto>>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        return summaries ?? Array.Empty<TimerDailySummaryDto>();
    }

    public Task<TimerCommandResultDto> StartAsync(TimerSessionStartOptions options, CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Post, "api/timer/start", options, cancellationToken);

    public Task<TimerCommandResultDto> PauseAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Post, "api/timer/pause", null, cancellationToken);

    public Task<TimerCommandResultDto> ResumeAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Post, "api/timer/resume", null, cancellationToken);

    public Task<TimerCommandResultDto> StopAsync(TimerSessionStopOptions options, CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Post, "api/timer/stop", options, cancellationToken);

    public Task<TimerCommandResultDto> CancelAsync(CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Post, "api/timer/cancel", null, cancellationToken);

    public Task<TimerCommandResultDto> UpdateNotesAsync(string notes, CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Put, "api/timer/notes", new { notes }, cancellationToken);

    public Task<TimerCommandResultDto> AdjustEntryAsync(TimeEntryAdjustmentOptions options, CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Put, $"api/timer/entries/{options.TimeEntryId:D}", options, cancellationToken);

    public Task<TimerCommandResultDto> DeleteEntryAsync(Guid timeEntryId, CancellationToken cancellationToken = default)
        => SendCommandAsync(HttpMethod.Delete, $"api/timer/entries/{timeEntryId:D}", null, cancellationToken);

    private async Task<TimerCommandResultDto> SendCommandAsync(HttpMethod method, string path, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);

        if (payload is not null && method != HttpMethod.Get)
        {
            request.Content = JsonContent.Create(payload, options: SerializerOptions);
        }

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.Content is null)
        {
            var fallback = await ReadErrorAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(fallback);
        }

        var result = await response.Content.ReadFromJsonAsync<TimerCommandResultDto>(SerializerOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            var error = await ReadErrorAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(error);
        }

        return result;
    }
}
