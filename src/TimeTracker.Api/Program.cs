using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Application.Repositories;
using TimeTracker.Application.Services;
using TimeTracker.Domain.Dtos;
using TimeTracker.Infrastructure;
using TimeTracker.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddTimeTrackerCore(options =>
{
    var databaseSection = builder.Configuration.GetSection("Database");
    var providerName = databaseSection.GetValue<string>("Provider");

    if (!string.IsNullOrWhiteSpace(providerName) &&
        Enum.TryParse(providerName, true, out TimeTrackerDatabaseProvider provider))
    {
        options.Provider = provider;
    }

    options.ConnectionString = databaseSection.GetValue<string>("ConnectionString");
    options.DatabasePath = databaseSection.GetValue<string>("DatabasePath");

    var pathOverride = Environment.GetEnvironmentVariable("TIMETRACKER_DB_PATH");
    if (!string.IsNullOrWhiteSpace(pathOverride))
    {
        options.DatabasePath = pathOverride;
    }
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TimeTrackerDbContext>();
    await dbContext.Database.MigrateAsync().ConfigureAwait(false);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.MapGet("/", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var timer = api.MapGroup("/timer");

timer.MapGet("/snapshot", async ([FromQuery] string date, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    DateOnly? targetDate = null;
    if (!string.IsNullOrWhiteSpace(date))
    {
        if (!DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return Results.BadRequest($"Invalid date '{date}'. Expected format is yyyy-MM-dd.");
        }

        targetDate = parsed;
    }

    var snapshot = await service.GetSnapshotAsync(targetDate, cancellationToken).ConfigureAwait(false);
    return Results.Ok(snapshot);
});

timer.MapGet("/daily-summary", async ([FromQuery] string start, [FromQuery] string end, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    if (!TryParseDate(start, out var startDate, out var startError))
    {
        return Results.BadRequest(startError);
    }

    if (!TryParseDate(end, out var endDate, out var endError))
    {
        return Results.BadRequest(endError);
    }

    var summaries = await service.GetDailySummaryAsync(startDate, endDate, cancellationToken).ConfigureAwait(false);
    return Results.Ok(summaries);
});

timer.MapGet("/history", async ([FromQuery] string date, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    if (!TryParseDate(date, out var targetDate, out var error))
    {
        return Results.BadRequest(error);
    }

    var history = await service.GetHistoryAsync(targetDate, cancellationToken).ConfigureAwait(false);
    return Results.Ok(history);
});

timer.MapPost("/start", async ([FromBody] TimerSessionStartOptions options, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    var result = await service.StartAsync(options, cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

timer.MapPost("/pause", async (ITimerSessionService service, CancellationToken cancellationToken) =>
{
    var result = await service.PauseAsync(cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

timer.MapPost("/resume", async (ITimerSessionService service, CancellationToken cancellationToken) =>
{
    var result = await service.ResumeAsync(cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

timer.MapPost("/stop", async ([FromBody] TimerSessionStopOptions options, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    var result = await service.StopAsync(options, cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

timer.MapPost("/cancel", async (ITimerSessionService service, CancellationToken cancellationToken) =>
{
    var result = await service.CancelAsync(cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

timer.MapPut("/notes", async ([FromBody] TimerNotesRequest request, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    var result = await service.UpdateNotesAsync(request?.Notes, cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

timer.MapPut("/entries/{entryId:guid}", async (Guid entryId, [FromBody] TimeEntryAdjustmentRequest request, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.BadRequest("Adjustment payload is required.");
    }

    var options = new TimeEntryAdjustmentOptions(entryId, request.StartLocal, request.EndLocal, request.Notes);
    var result = await service.AdjustEntryAsync(options, cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

timer.MapDelete("/entries/{entryId:guid}", async (Guid entryId, ITimerSessionService service, CancellationToken cancellationToken) =>
{
    var result = await service.DeleteEntryAsync(entryId, cancellationToken).ConfigureAwait(false);
    return MapTimerResult(result);
});

var customers = api.MapGroup("/customers");

customers.MapGet("/", async (ICustomerRepository repository, CancellationToken cancellationToken) =>
{
    var customers = await repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
    return Results.Ok(customers);
});

customers.MapGet("/{id:guid}", async (Guid id, ICustomerRepository repository, CancellationToken cancellationToken) =>
{
    var customer = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    return customer is null ? Results.NotFound() : Results.Ok(customer);
});

customers.MapPost("/", async ([FromBody] CustomerCreateDto dto, ICustomerRepository repository, CancellationToken cancellationToken) =>
{
    if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
    {
        return Results.BadRequest("Customer name is required.");
    }

    var created = await repository.CreateAsync(dto, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/customers/{created.Id}", created);
});

customers.MapPut("/{id:guid}", async (Guid id, [FromBody] CustomerUpdateRequest request, ICustomerRepository repository, CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.BadRequest("Customer update payload is required.");
    }

    var dto = new CustomerUpdateDto(id, request.Name, request.IsArchived);
    var updated = await repository.UpdateAsync(dto, cancellationToken).ConfigureAwait(false);
    return Results.Ok(updated);
});

customers.MapDelete("/{id:guid}", async (Guid id, ICustomerRepository repository, CancellationToken cancellationToken) =>
{
    var removed = await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    return removed ? Results.NoContent() : Results.NotFound();
});

var projects = api.MapGroup("/projects");

projects.MapGet("/list", async ([FromQuery] bool includeInactive, IProjectService service, CancellationToken cancellationToken) =>
{
    var results = await service.GetProjectsAsync(includeInactive, cancellationToken).ConfigureAwait(false);
    return Results.Ok(results);
});

projects.MapGet("/", async ([FromQuery] Guid? customerId, [FromQuery] bool includeInactive, IProjectService service, CancellationToken cancellationToken) =>
{
    if (customerId is null || customerId == Guid.Empty)
    {
        return Results.BadRequest("customerId is required.");
    }

    var results = await service.GetProjectsByCustomerAsync(customerId.Value, includeInactive, cancellationToken).ConfigureAwait(false);
    return Results.Ok(results);
});

projects.MapGet("/{id:guid}", async (Guid id, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    var project = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    return project is null ? Results.NotFound() : Results.Ok(project);
});

projects.MapPost("/", async ([FromBody] ProjectCreateRequest request, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.BadRequest("Project payload is required.");
    }

    if (request.CustomerId == Guid.Empty)
    {
        return Results.BadRequest("CustomerId is required.");
    }

    var dto = new ProjectCreateDto(request.CustomerId, request.Name, request.IsActive);
    var created = await repository.CreateAsync(dto, cancellationToken).ConfigureAwait(false);
    return Results.Created($"/api/projects/{created.Id}", created);
});

projects.MapPut("/{id:guid}", async (Guid id, [FromBody] ProjectUpdateRequest request, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    if (request is null)
    {
        return Results.BadRequest("Project payload is required.");
    }

    if (request.CustomerId == Guid.Empty)
    {
        return Results.BadRequest("CustomerId is required.");
    }

    var dto = new ProjectUpdateDto(id, request.CustomerId, request.Name, request.IsActive);
    var updated = await repository.UpdateAsync(dto, cancellationToken).ConfigureAwait(false);
    return Results.Ok(updated);
});

projects.MapDelete("/{id:guid}", async (Guid id, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    var removed = await repository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    return removed ? Results.NoContent() : Results.NotFound();
});

app.Run();

static bool TryParseDate(string value, out DateOnly result, out string error)
{
    error = null;

    if (string.IsNullOrWhiteSpace(value))
    {
        result = default;
        error = "Date is required and must be provided as yyyy-MM-dd.";
        return false;
    }

    if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
    {
        return true;
    }

    error = $"Invalid date '{value}'. Expected format is yyyy-MM-dd.";
    return false;
}

static IResult MapTimerResult(TimerCommandResultDto result)
{
    if (result is null)
    {
        return Results.Json(
            new { status = TimerCommandStatus.Failure, message = "Timer service returned no result." },
            statusCode: StatusCodes.Status500InternalServerError);
    }

    return result.Status switch
    {
        TimerCommandStatus.Success => Results.Ok(result),
        TimerCommandStatus.ValidationFailed => Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity),
        TimerCommandStatus.Conflict => Results.Json(result, statusCode: StatusCodes.Status409Conflict),
        TimerCommandStatus.NotFound => Results.Json(result, statusCode: StatusCodes.Status404NotFound),
        _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError)
    };
}

internal sealed record class TimerNotesRequest(string Notes);

internal sealed record class TimeEntryAdjustmentRequest(DateTime? StartLocal, DateTime? EndLocal, string Notes);

internal sealed record class CustomerUpdateRequest(string Name, bool IsArchived);

internal sealed record class ProjectCreateRequest(Guid CustomerId, string Name, bool IsActive);

internal sealed record class ProjectUpdateRequest(Guid CustomerId, string Name, bool IsActive);
