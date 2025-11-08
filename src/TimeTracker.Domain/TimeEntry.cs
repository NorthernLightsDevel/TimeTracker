using System;

namespace TimeTracker.Domain.Entities;

public sealed class TimeEntry
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid ProjectId { get; private set; }
    public DateTime StartLocal { get; private set; }
    public DateTime? EndLocal { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime? EndUtc { get; private set; }
    public string Notes { get; private set; }
    public bool Billable { get; private set; }
    public string Tag { get; private set; }
    public string ServerId { get; private set; }
    public bool PendingSync { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime LastModifiedUtc { get; private set; }
    public byte[] RowVersion { get; private set; }
    public Project Project { get; private set; }
    public Customer Customer { get; private set; }

    public bool IsRunning => !EndUtc.HasValue;
    public TimeSpan? Duration => EndLocal.HasValue ? EndLocal.Value - StartLocal : null;

    private TimeEntry()
    {
        Notes = string.Empty;
        Billable = true;
        PendingSync = true;
        LastModifiedUtc = DateTime.UtcNow;
    }

    public TimeEntry(
        Guid id,
        Guid customerId,
        Guid projectId,
        DateTime startLocal,
        DateTime startUtc,
        string notes = null,
        bool billable = true,
        string tag = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Time entry id cannot be empty.", nameof(id));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        if (startUtc.Kind == DateTimeKind.Unspecified)
        {
            throw new ArgumentException("UTC start time must include a kind.", nameof(startUtc));
        }

        Id = id;
        CustomerId = customerId;
        ProjectId = projectId;
        StartLocal = EnsureLocal(startLocal);
        StartUtc = EnsureUtc(startUtc);
        Notes = SanitizeNotes(notes);
        Billable = billable;
        Tag = SanitizeTag(tag);
        PendingSync = true;
        LastModifiedUtc = DateTime.UtcNow;
    }

    public TimeEntry(Guid customerId, Guid projectId, DateTime startLocal, DateTime startUtc)
        : this(Guid.NewGuid(), customerId, projectId, startLocal, startUtc)
    {
    }

    public void Stop(DateTime endLocal, DateTime endUtc)
    {
        if (endUtc.Kind == DateTimeKind.Unspecified)
        {
            throw new ArgumentException("UTC end time must include a kind.", nameof(endUtc));
        }

        endLocal = EnsureLocal(endLocal);
        endUtc = EnsureUtc(endUtc);

        if (endLocal < StartLocal)
        {
            throw new ArgumentException("Stop time cannot be earlier than the start time.", nameof(endLocal));
        }

        EndLocal = endLocal;
        EndUtc = endUtc;
        Touch();
    }

    public void AdjustStart(DateTime newStartLocal, DateTime newStartUtc)
    {
        newStartLocal = EnsureLocal(newStartLocal);
        newStartUtc = EnsureUtc(newStartUtc);

        if (EndLocal.HasValue && newStartLocal >= EndLocal.Value)
        {
            throw new ArgumentException("Start time must be earlier than the end time.", nameof(newStartLocal));
        }

        StartLocal = newStartLocal;
        StartUtc = newStartUtc;
        Touch();
    }

    public void UpdateNotes(string notes)
    {
        Notes = SanitizeNotes(notes);
        Touch();
    }

    public void SetBillable(bool billable)
    {
        if (Billable == billable)
        {
            return;
        }

        Billable = billable;
        Touch();
    }

    public void SetTag(string tag)
    {
        Tag = SanitizeTag(tag);
        Touch();
    }

    public void MarkDeleted()
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        Touch();
    }

    public void Restore()
    {
        if (!IsDeleted)
        {
            return;
        }

        IsDeleted = false;
        Touch();
    }

    public void MarkSynced(string serverId = null)
    {
        if (!string.IsNullOrWhiteSpace(serverId))
        {
            ServerId = serverId.Trim();
        }

        PendingSync = false;
        Touch();
    }

    public void MarkPendingSync()
    {
        if (PendingSync)
        {
            return;
        }

        PendingSync = true;
        Touch();
    }

    public void AttachProject(Project project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        ProjectId = project.Id;
        project.AddTimeEntry(this);
    }

    public void AttachCustomer(Customer customer)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        CustomerId = customer.Id;
    }

    public void SetRowVersion(byte[] rowVersion)
    {
        RowVersion = rowVersion ?? throw new ArgumentNullException(nameof(rowVersion));
    }

    private void Touch() => LastModifiedUtc = DateTime.UtcNow;

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private static DateTime EnsureLocal(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local),
        _ => value.ToLocalTime()
    };

    private static string SanitizeNotes(string value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? string.Empty : trimmed;
    }

    private static string SanitizeTag(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 50 ? trimmed[..50] : trimmed;
    }
}
