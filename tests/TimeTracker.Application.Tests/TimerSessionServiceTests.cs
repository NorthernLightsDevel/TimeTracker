using System;
using System.Linq;
using System.Threading.Tasks;
using TimeTracker.Application.Repositories;
using TimeTracker.Application.Services;
using TimeTracker.Application.Tests.Infrastructure;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Tests;

public class TimerSessionServiceTests
{
    [Fact]
    public async Task StartAsync_CreatesRoundedEntryAndActiveSnapshot()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-01T12:07:29Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        var result = await service.StartAsync(new TimerSessionStartOptions(project.Id));

        Assert.Equal(TimerCommandStatus.Success, result.Status);
        Assert.Equal(TimerSessionDtos.Running, result.Snapshot.Status);
        Assert.NotNull(result.Snapshot.ActiveSession);

        var active = result.Snapshot.ActiveSession!;
        var expectedLocalNow = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, TimeZoneInfo.Local);
        var expectedStart = expectedLocalNow;

        Assert.Equal(expectedStart, active.StartLocal);
        Assert.Equal(TimeSpan.Zero, active.AccumulatedDuration);
        Assert.Equal(TimeSpan.FromMinutes(15), active.RoundedDuration);
    }

    [Fact]
    public async Task PauseResumeStop_FlowsProduceExpectedDurations()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-01T08:05:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));

        clock.Advance(TimeSpan.FromMinutes(27));
        var pauseResult = await service.PauseAsync();

        Assert.Equal(TimerSessionDtos.Paused, pauseResult.Snapshot.Status);
        var paused = pauseResult.Snapshot.ActiveSession!;
        Assert.True(paused.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(27), paused.AccumulatedDuration);
        Assert.Equal(TimeSpan.FromMinutes(30), paused.RoundedDuration);

        clock.Advance(TimeSpan.FromMinutes(5));
        var resumeResult = await service.ResumeAsync();

        Assert.Equal(TimerSessionDtos.Running, resumeResult.Snapshot.Status);
        var running = resumeResult.Snapshot.ActiveSession!;
        Assert.False(running.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(27), running.AccumulatedDuration);

        clock.Advance(TimeSpan.FromMinutes(20));
        var stopResult = await service.StopAsync(new TimerSessionStopOptions());

        Assert.Equal(TimerSessionDtos.Idle, stopResult.Snapshot.Status);
        Assert.Null(stopResult.Snapshot.ActiveSession);

        var history = await service.GetHistoryAsync(stopResult.Snapshot.LocalDate);
        Assert.Equal(2, history.Count);
        Assert.True(history.All(entry => entry.Billable));
        Assert.Equal(TimeSpan.FromMinutes(27), history[0].Duration);
        Assert.Equal(TimeSpan.FromMinutes(30), history[0].RoundedDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), history[1].Duration);
        Assert.Equal(TimeSpan.FromMinutes(15), history[1].RoundedDuration);
    }

    [Fact]
    public async Task ResumeAsync_ImmediatelyAfterPause_UsesPauseTimestamp()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-06T10:00:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));
        clock.Advance(TimeSpan.FromMinutes(3));

        var pauseLocal = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, TimeZoneInfo.Local);
        await service.PauseAsync();

        var resumeResult = await service.ResumeAsync();
        var resumed = resumeResult.Snapshot.ActiveSession!;

        Assert.Equal(pauseLocal, resumed.StartLocal);
        Assert.False(resumed.IsPaused);
    }

    [Fact]
    public async Task ResumeAsync_AfterShortDelay_UsesCurrentLocalTime()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-06T11:30:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));
        clock.Advance(TimeSpan.FromMinutes(4));
        await service.PauseAsync();

        clock.Advance(TimeSpan.FromSeconds(10));
        var expectedResumeLocal = TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, TimeZoneInfo.Local);

        var resumeResult = await service.ResumeAsync();
        var resumed = resumeResult.Snapshot.ActiveSession!;

        Assert.Equal(expectedResumeLocal, resumed.StartLocal);
        Assert.False(resumed.IsPaused);
    }

    [Fact]
    public async Task StopAsync_WithPastOverride_ClampsEndToStart()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-07T07:00:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        var startResult = await service.StartAsync(new TimerSessionStartOptions(project.Id));
        var active = startResult.Snapshot.ActiveSession!;

        var pastOverride = active.StartLocal.AddMinutes(-10);
        var stopResult = await service.StopAsync(new TimerSessionStopOptions(StopLocalOverride: pastOverride));

        Assert.Equal(TimerSessionDtos.Idle, stopResult.Snapshot.Status);
        var entry = Assert.Single(stopResult.Snapshot.Entries);
        Assert.Equal(entry.StartLocal, entry.EndLocal);
        Assert.Equal(TimeSpan.Zero, entry.Duration);

        var persisted = await harness.TimeEntryRepository.GetByIdAsync(entry.TimeEntryId);
        Assert.NotNull(persisted);
        Assert.Equal(persisted!.StartLocal, persisted.EndLocal);
    }

    [Fact]
    public async Task GetDailySummaryAsync_RoundsConcatenatedDurationsPerProject()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-05T09:00:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var projectA = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project A", true));
        var projectB = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project B", true));

        var service = CreateService(harness, clock);

        var baseStart = DateTime.SpecifyKind(DateTime.Parse("2024-03-05T09:00:00"), DateTimeKind.Local);

        await CreateCompletedEntryAsync(harness.TimeEntryRepository, customer.Id, projectA.Id, baseStart, TimeSpan.FromMinutes(10));
        await CreateCompletedEntryAsync(harness.TimeEntryRepository, customer.Id, projectA.Id, baseStart.AddHours(1), TimeSpan.FromMinutes(10));
        await CreateCompletedEntryAsync(harness.TimeEntryRepository, customer.Id, projectB.Id, baseStart.AddHours(2), TimeSpan.FromMinutes(10));

        var summaries = await service.GetDailySummaryAsync(DateOnly.FromDateTime(baseStart), DateOnly.FromDateTime(baseStart));

        var summary = Assert.Single(summaries);
        Assert.Equal(TimeSpan.FromMinutes(30), summary.TotalDuration);
        Assert.Equal(TimeSpan.FromMinutes(30), summary.TotalRoundedDuration);
    }

    [Fact]
    public async Task AdjustEntryAsync_UpdatesNotesWhenProvided()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-08T08:00:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id, Notes: "Initial"));
        clock.Advance(TimeSpan.FromMinutes(45));
        await service.StopAsync(new TimerSessionStopOptions());

        var snapshot = await service.GetSnapshotAsync();
        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal("Initial", entry.Notes);

        var adjustment = new TimeEntryAdjustmentOptions(entry.TimeEntryId, null, null, "Updated note");
        var adjustResult = await service.AdjustEntryAsync(adjustment);

        Assert.Equal(TimerCommandStatus.Success, adjustResult.Status);
        var updatedEntry = Assert.Single(adjustResult.Snapshot.Entries);
        Assert.Equal("Updated note", updatedEntry.Notes);
    }

    [Fact]
    public async Task CancelAsync_RemovesActiveEntry()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-01T10:00:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));
        var cancelResult = await service.CancelAsync();

        Assert.Equal(TimerCommandStatus.Success, cancelResult.Status);
        Assert.Equal(TimerSessionDtos.Idle, cancelResult.Snapshot.Status);

        var activeEntry = await harness.TimeEntryRepository.GetActiveAsync();
        Assert.Null(activeEntry);

        var todayHistory = await service.GetHistoryAsync(cancelResult.Snapshot.LocalDate);
        Assert.Empty(todayHistory);
    }

    [Fact]
    public async Task DeleteEntryAsync_RemovesEntryAndRefreshesSnapshot()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-02T07:15:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));
        clock.Advance(TimeSpan.FromMinutes(25));
        var stopResult = await service.StopAsync(new TimerSessionStopOptions());

        var entry = Assert.Single(stopResult.Snapshot.Entries);

        var deleteResult = await service.DeleteEntryAsync(entry.TimeEntryId);

        Assert.Equal(TimerCommandStatus.Success, deleteResult.Status);
        Assert.Empty(deleteResult.Snapshot.Entries);

        var lookup = await harness.TimeEntryRepository.GetByIdAsync(entry.TimeEntryId);
        Assert.Null(lookup);
    }

    [Fact]
    public async Task StartAsync_ConflictsWithoutForceRestart()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-01T09:00:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));
        var second = await service.StartAsync(new TimerSessionStartOptions(project.Id));

        Assert.Equal(TimerCommandStatus.Conflict, second.Status);
        Assert.Equal(TimerSessionDtos.Running, second.Snapshot.Status);
    }

    [Fact]
    public async Task StartAsync_WithForceRestartStopsExistingSession()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-01T09:07:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));
        clock.Advance(TimeSpan.FromMinutes(10));
        var restart = await service.StartAsync(new TimerSessionStartOptions(project.Id, ForceRestart: true));

        Assert.Equal(TimerCommandStatus.Success, restart.Status);
        Assert.Equal(TimerSessionDtos.Running, restart.Snapshot.Status);

        var history = await service.GetHistoryAsync(restart.Snapshot.LocalDate);
        Assert.Single(history);
        Assert.True(history[0].RoundedDuration >= TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task UpdateNotesAsync_WhenRunning_UpdatesActiveEntry()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-03T14:00:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id));
        var updateResult = await service.UpdateNotesAsync("Deep work");

        Assert.Equal(TimerCommandStatus.Success, updateResult.Status);
        Assert.Equal("Deep work", updateResult.Snapshot.ActiveSession!.Notes);

        var activeEntry = await harness.TimeEntryRepository.GetActiveAsync();
        Assert.NotNull(activeEntry);
        Assert.Equal("Deep work", activeEntry!.Notes);
    }

    [Fact]
    public async Task UpdateNotesAsync_WhenPaused_UpdatesLastEntry()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-03T08:15:00Z"));

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var service = CreateService(harness, clock);

        await service.StartAsync(new TimerSessionStartOptions(project.Id, Notes: "Initial"));
        clock.Advance(TimeSpan.FromMinutes(25));
        var pauseResult = await service.PauseAsync();

        Assert.Equal(TimerSessionDtos.Paused, pauseResult.Snapshot.Status);
        var pausedEntryId = pauseResult.Snapshot.ActiveSession!.TimeEntryId;

        var updateResult = await service.UpdateNotesAsync("Code review");

        Assert.Equal(TimerCommandStatus.Success, updateResult.Status);
        Assert.Equal(TimerSessionDtos.Paused, updateResult.Snapshot.Status);
        Assert.Equal("Code review", updateResult.Snapshot.ActiveSession!.Notes);

        var pausedEntry = await harness.TimeEntryRepository.GetByIdAsync(pausedEntryId);
        Assert.NotNull(pausedEntry);
        Assert.Equal("Code review", pausedEntry!.Notes);
    }

    [Fact]
    public async Task UpdateNotesAsync_WhenIdle_ReturnsNotFound()
    {
        await using var harness = await DatabaseHarness.CreateAsync(DatabaseProvider.Sqlite);
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2024-03-04T09:00:00Z"));

        var service = CreateService(harness, clock);

        var result = await service.UpdateNotesAsync("Nothing running");

        Assert.Equal(TimerCommandStatus.NotFound, result.Status);
        Assert.Equal(TimerSessionDtos.Idle, result.Snapshot.Status);
    }

    private static TimerSessionService CreateService(DatabaseHarness harness, FakeTimeProvider clock)
    {
        return new TimerSessionService(
            harness.TimeEntryRepository,
            harness.ProjectRepository,
            harness.CustomerRepository,
            clock,
            TimeZoneInfo.Local);
    }

    private static async Task CreateCompletedEntryAsync(
        ITimeEntryRepository repository,
        Guid customerId,
        Guid projectId,
        DateTime startLocal,
        TimeSpan duration)
    {
        if (startLocal.Kind != DateTimeKind.Local)
        {
            startLocal = DateTime.SpecifyKind(startLocal, DateTimeKind.Local);
        }

        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal);
        var entry = await repository.CreateAsync(new TimeEntryCreateDto(
            customerId,
            projectId,
            startLocal,
            startUtc,
            string.Empty,
            true,
            string.Empty));

        var endLocal = startLocal.Add(duration);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal);

        var updateDto = new TimeEntryUpdateDto(
            entry.Id,
            null,
            null,
            endLocal,
            endUtc,
            null,
            null,
            null,
            null,
            null,
            null);

        await repository.UpdateAsync(updateDto);
    }
}
