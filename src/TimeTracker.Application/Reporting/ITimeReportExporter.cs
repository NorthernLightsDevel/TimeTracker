using System;
using System.Threading;
using System.Threading.Tasks;

namespace TimeTracker.Application.Reporting;

public interface ITimeReportExporter
{
    Task<string> BuildCsvAsync(TimeReportPreset preset, CancellationToken cancellationToken = default);

    Task<string> BuildCsvAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}
