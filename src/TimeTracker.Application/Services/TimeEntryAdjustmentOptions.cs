using System;

namespace TimeTracker.Application.Services;

public sealed record class TimeEntryAdjustmentOptions
{
    public TimeEntryAdjustmentOptions(Guid timeEntryId, DateTime? startLocal, DateTime? endLocal, string notes = null)
    {
        if (timeEntryId == Guid.Empty)
        {
            throw new ArgumentException("Time entry id is required.", nameof(timeEntryId));
        }

        TimeEntryId = timeEntryId;
        StartLocal = startLocal;
        EndLocal = endLocal;
        Notes = notes;
    }

    public Guid TimeEntryId { get; }

    public DateTime? StartLocal { get; }

    public DateTime? EndLocal { get; }

    public string Notes { get; }
}
