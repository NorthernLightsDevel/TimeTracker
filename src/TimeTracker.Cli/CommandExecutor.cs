using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TimeTracker.Application.Reporting;
using TimeTracker.Application.Services;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Cli;

internal sealed class CommandExecutor
{
    private readonly ITimerSessionService _timerService;
    private readonly IProjectService _projectService;
    private readonly ILogger<CommandExecutor> _logger;
    private readonly ITimeReportExporter _reportExporter;

    public CommandExecutor(
        ITimerSessionService timerService,
        IProjectService projectService,
        ITimeReportExporter reportExporter,
        ILogger<CommandExecutor> logger)
    {
        _timerService = timerService ?? throw new ArgumentNullException(nameof(timerService));
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _reportExporter = reportExporter ?? throw new ArgumentNullException(nameof(reportExporter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelpRequest(args[0]))
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var remaining = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "status" => await HandleStatusAsync(remaining, cancellationToken).ConfigureAwait(false),
                "start" => await HandleStartAsync(remaining, cancellationToken).ConfigureAwait(false),
                "pause" => await HandlePauseAsync(cancellationToken).ConfigureAwait(false),
                "resume" => await HandleResumeAsync(cancellationToken).ConfigureAwait(false),
                "stop" => await HandleStopAsync(remaining, cancellationToken).ConfigureAwait(false),
                "toggle" => await HandleToggleAsync(cancellationToken).ConfigureAwait(false),
                "set" => await HandleSetAsync(remaining, cancellationToken).ConfigureAwait(false),
                "projects" => await HandleProjectsAsync(remaining, cancellationToken).ConfigureAwait(false),
                "comment" => await HandleCommentAsync(remaining, cancellationToken).ConfigureAwait(false),
                "waybar" => await HandleWaybarAsync(cancellationToken).ConfigureAwait(false),
                "report" => await HandleReportAsync(remaining, cancellationToken).ConfigureAwait(false),
                _ => UnknownCommand(command)
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("timetracker: operation cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while executing command {Command}", command);
            Console.Error.WriteLine($"timetracker: {ex.Message}");
            return 1;
        }
    }

    private static bool IsHelpRequest(string value) =>
        string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);

    private int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"timetracker: unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: timetracker <command> [options]\n");
        Console.WriteLine("Commands:");
        Console.WriteLine("  status [--json] [--pretty]       Show the current timer status (plain text by default).");
        Console.WriteLine("  start <projectId> [options]      Start or restart tracking for the specified project.");
        Console.WriteLine("  pause                            Pause the active session.");
        Console.WriteLine("  resume                           Resume the most recently paused session.");
        Console.WriteLine("  stop [options]                   Stop the active session.");
        Console.WriteLine("  toggle                           Pause if running, resume if paused.");
        Console.WriteLine("  set <projectId> [options]        Switch to or start the specified project (force restart).");
        Console.WriteLine("  comment [text|--clear|--show]    Update or display notes for the active or paused session.");
        Console.WriteLine("  waybar                          Emit status payload formatted for Waybar.");
        Console.WriteLine("  projects [--json] [customerId]  List projects (optionally filtered by customer).");
        Console.WriteLine("  report <w|m>                    Output a CSV summary for the last week or month.");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --note <text>                    Attach notes to the entry.");
        Console.WriteLine("  --tag <value>                    Apply a tag to the entry.");
        Console.WriteLine("  --billable / --non-billable      Mark the entry as billable or not.");
        Console.WriteLine("  --customer <id>                  Override the customer (requires GUID).");
        Console.WriteLine("  --force                          Restart the timer if it is already running.");
        Console.WriteLine("  --persist-empty                  Preserve zero-duration stop entries.");
    }

    private async Task<int> HandleStatusAsync(string[] args, CancellationToken cancellationToken)
    {
        bool asJson;
        bool pretty;

        try
        {
            (asJson, pretty) = ParseStatusOptions(args);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"timetracker: {ex.Message}");
            return 1;
        }

        var snapshot = await _timerService.GetSnapshotAsync(null, cancellationToken).ConfigureAwait(false);

        if (asJson)
        {
            var payload = StatusFormatter.CreatePayload(snapshot);
            var options = new JsonSerializerOptions
            {
                WriteIndented = pretty
            };
            options.Converters.Add(new JsonStringEnumConverter());
            Console.WriteLine(JsonSerializer.Serialize(payload, options));
        }
        else
        {
            Console.WriteLine(StatusFormatter.FormatPlain(snapshot));
        }

        return 0;
    }

    private async Task<int> HandleStartAsync(string[] args, CancellationToken cancellationToken)
    {
        if (!TryParseStartOptions(args, out var options, out var error))
        {
            Console.Error.WriteLine($"timetracker: {error}");
            return 1;
        }

        var result = await _timerService.StartAsync(options, cancellationToken).ConfigureAwait(false);

        return RenderCommandResult(result);
    }

    private async Task<int> HandlePauseAsync(CancellationToken cancellationToken)
    {
        var result = await _timerService.PauseAsync(cancellationToken).ConfigureAwait(false);

        return RenderCommandResult(result);
    }

    private async Task<int> HandleResumeAsync(CancellationToken cancellationToken)
    {
        var result = await _timerService.ResumeAsync(cancellationToken).ConfigureAwait(false);

        return RenderCommandResult(result);
    }

    private async Task<int> HandleStopAsync(string[] args, CancellationToken cancellationToken)
    {
        var (options, error) = ParseStopOptions(args);
        if (error is not null)
        {
            Console.Error.WriteLine($"timetracker: {error}");
            return 1;
        }

        var result = await _timerService.StopAsync(options, cancellationToken).ConfigureAwait(false);

        return RenderCommandResult(result);
    }

    private async Task<int> HandleToggleAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _timerService.GetSnapshotAsync(null, cancellationToken).ConfigureAwait(false);

        return snapshot.Status switch
        {
            TimerSessionDtos.Running => await HandlePauseAsync(cancellationToken).ConfigureAwait(false),
            TimerSessionDtos.Paused => await HandleResumeAsync(cancellationToken).ConfigureAwait(false),
            _ => Error()
        };

        int Error()
        {
            Console.Error.WriteLine("timetracker: nothing to toggle. Start a project first.");
            return 1;
        }
    }

    private async Task<int> HandleSetAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("timetracker: set requires a project id.");
            return 1;
        }

        var augmented = args.Concat(new[] { "--force" }).ToArray();
        return await HandleStartAsync(augmented, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> HandleCommentAsync(string[] args, CancellationToken cancellationToken)
    {
        var (notes, showOnly, error) = ParseCommentOptions(args);
        if (error is not null)
        {
            Console.Error.WriteLine($"timetracker: {error}");
            return 1;
        }

        if (showOnly)
        {
            var snapshot = await _timerService.GetSnapshotAsync(null, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(snapshot.ActiveSession?.Notes))
            {
                Console.WriteLine(snapshot.ActiveSession.Notes);
            }

            return 0;
        }

        var result = await _timerService.UpdateNotesAsync(notes, cancellationToken).ConfigureAwait(false);

        return RenderCommandResult(result);
    }

    private (TimerSessionStopOptions Options, string Error) ParseStopOptions(string[] args)
    {
        string notes = null;
        bool? billable = null;
        string tag = null;
        bool persistEmpty = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--note":
                    if (!TryReadNext(args, ref i, out notes))
                    {
                        return (null, "--note requires a value.");
                    }
                    break;
                case "--tag":
                    if (!TryReadNext(args, ref i, out tag))
                    {
                        return (null, "--tag requires a value.");
                    }
                    break;
                case "--billable":
                    billable = true;
                    break;
                case "--non-billable":
                case "--nonbillable":
                    billable = false;
                    break;
                case "--persist-empty":
                    persistEmpty = true;
                    break;
                default:
                    return (null, $"unknown option '{arg}'.");
            }
        }

        return (new TimerSessionStopOptions(notes, billable, tag, null, persistEmpty), null);
    }

    private (string Notes, bool ShowOnly, string Error) ParseCommentOptions(string[] args)
    {
        if (args.Length == 0)
        {
            return (string.Empty, false, null);
        }

        if (args.Length == 1 && string.Equals(args[0], "--clear", StringComparison.OrdinalIgnoreCase))
        {
            return (string.Empty, false, null);
        }

        if (args.Length == 1 && string.Equals(args[0], "--show", StringComparison.OrdinalIgnoreCase))
        {
            return (null, true, null);
        }

        if (args.Any(arg => string.Equals(arg, "--show", StringComparison.OrdinalIgnoreCase)))
        {
            return (null, false, "--show cannot be combined with other arguments.");
        }

        return (string.Join(' ', args), false, null);
    }

    private static (bool AsJson, bool Pretty) ParseStatusOptions(string[] args)
    {
        var asJson = false;
        var pretty = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--json":
                    asJson = true;
                    break;
                case "--pretty":
                    asJson = true;
                    pretty = true;
                    break;
                default:
                    throw new InvalidOperationException($"unknown option '{arg}'.");
            }
        }

        return (asJson, pretty);
    }

    private async Task<int> HandleWaybarAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _timerService.GetSnapshotAsync(null, cancellationToken).ConfigureAwait(false);

        var payload = StatusFormatter.CreateWaybarPayload(snapshot);
        Console.WriteLine(JsonSerializer.Serialize(payload));
        return 0;
    }

    private async Task<int> HandleProjectsAsync(string[] args, CancellationToken cancellationToken)
    {
        var asJson = false;
        Guid customerId = Guid.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                asJson = true;
                continue;
            }

            if (customerId != Guid.Empty)
            {
                Console.Error.WriteLine("timetracker: only one customer id can be provided.");
                return 1;
            }

            if (!Guid.TryParse(arg, out customerId))
            {
                Console.Error.WriteLine("timetracker: customer id must be a valid GUID.");
                return 1;
            }
        }

        IReadOnlyList<ProjectListItemDto> projects;

        if (customerId == Guid.Empty)
        {
            projects = await _projectService.GetProjectsAsync(includeInactive: false, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var all = await _projectService.GetProjectsAsync(includeInactive: false, cancellationToken).ConfigureAwait(false);
            projects = all.Where(project => project.CustomerId == customerId).ToList();
        }

        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(projects, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            foreach (var project in projects)
            {
                Console.WriteLine($"{project.CustomerName} ▸ {project.ProjectName} {project.ProjectId}");
            }
        }

        return 0;
    }

    private bool TryParseStartOptions(string[] args, out TimerSessionStartOptions options, out string error)
    {
        options = null;
        error = null;

        Guid? projectId = null;
        Guid? customerId = null;
        string notes = null;
        bool billable = true;
        string tag = null;
        bool force = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                switch (arg)
                {
                    case "--note":
                        if (!TryReadNext(args, ref i, out notes))
                        {
                            error = "--note requires a value.";
                            return false;
                        }
                        break;
                    case "--tag":
                        if (!TryReadNext(args, ref i, out tag))
                        {
                            error = "--tag requires a value.";
                            return false;
                        }
                        break;
                    case "--billable":
                        billable = true;
                        break;
                    case "--non-billable":
                    case "--nonbillable":
                        billable = false;
                        break;
                    case "--customer":
                        if (!TryReadNext(args, ref i, out var customerValue))
                        {
                            error = "--customer requires a GUID.";
                            return false;
                        }

                        if (!Guid.TryParse(customerValue, out var parsedCustomer))
                        {
                            error = "--customer must be a valid GUID.";
                            return false;
                        }

                        customerId = parsedCustomer;
                        break;
                    case "--force":
                        force = true;
                        break;
                    default:
                        error = $"unknown option '{arg}'.";
                        return false;
                }
            }
            else
            {
                if (projectId is not null)
                {
                    error = "project id provided multiple times.";
                    return false;
                }

                if (!Guid.TryParse(arg, out var parsedId))
                {
                    error = "project id must be a valid GUID.";
                    return false;
                }

                projectId = parsedId;
            }
        }

        if (projectId is null)
        {
            error = "project id is required.";
            return false;
        }

        options = new TimerSessionStartOptions(projectId.Value, customerId, notes, billable, tag, null, force);
        return true;
    }

    private static bool TryReadNext(string[] args, ref int index, out string value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }


    private int RenderCommandResult(TimerCommandResultDto result)
    {
        if (result is null)
        {
            Console.Error.WriteLine("timetracker: no result returned by timer service.");
            return 1;
        }

        var isSuccess = result.Status == TimerCommandStatus.Success;
        var messageTarget = isSuccess ? Console.Out : Console.Error;

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            messageTarget.WriteLine(result.Message);
        }

        if (!isSuccess)
        {
            return 1;
        }

        if (result.Snapshot is not null)
        {
            Console.WriteLine(StatusFormatter.FormatPlain(result.Snapshot));
        }

        return 0;
    }

    private static void PrintReportUsage()
    {
        Console.WriteLine("Usage: timetracker report <w|m>");
        Console.WriteLine("  w / week   Last 7 days (including today)");
        Console.WriteLine("  m / month  Last 30 days (including today)");
    }

    private async Task<int> HandleReportAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || !TryParseReportPreset(args[0], out var preset))
        {
            Console.Error.WriteLine("timetracker: report period is required (w for week, m for month).");
            PrintReportUsage();
            return 1;
        }

        try
        {
            var csv = await _reportExporter.BuildCsvAsync(preset, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(csv))
            {
                Console.WriteLine("day,customer,project,totalHours,notes");
            }
            else
            {
                Console.Write(csv);
                if (!csv.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                {
                    Console.WriteLine();
                }
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("timetracker: report cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate {Preset} report.", preset);
            Console.Error.WriteLine($"timetracker: failed to generate report ({ex.Message}).");
            return 1;
        }
    }

    private static bool TryParseReportPreset(string value, out TimeReportPreset preset)
    {
        preset = TimeReportPreset.Week;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "w":
            case "week":
                preset = TimeReportPreset.Week;
                return true;
            case "m":
            case "month":
                preset = TimeReportPreset.Month;
                return true;
            default:
                return false;
        }
    }

}
