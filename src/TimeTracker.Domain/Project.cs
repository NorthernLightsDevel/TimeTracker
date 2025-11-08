using System;
using System.Collections.Generic;

namespace TimeTracker.Domain.Entities;

public sealed class Project
{
    private readonly List<TimeEntry> _entries = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime LastModifiedUtc { get; private set; }
    public Customer Customer { get; private set; }
    public IReadOnlyList<TimeEntry> TimeEntries => _entries;

    private Project()
    {
        Name = string.Empty;
        CreatedUtc = DateTime.UtcNow;
        LastModifiedUtc = CreatedUtc;
        IsActive = true;
    }

    public Project(Guid id, Guid customerId, string name, DateTime createdUtc, bool isActive = true)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Project id cannot be empty.", nameof(id));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Project must have a customer id.", nameof(customerId));
        }

        Id = id;
        CustomerId = customerId;
        Name = NormalizeName(name);
        CreatedUtc = EnsureUtc(createdUtc);
        LastModifiedUtc = CreatedUtc;
        IsActive = isActive;
    }

    public Project(Guid customerId, string name)
        : this(Guid.NewGuid(), customerId, name, DateTime.UtcNow)
    {
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        Touch();
    }

    public void SetActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        Touch();
    }

    public void AttachCustomer(Customer customer)
    {
        Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        CustomerId = customer.Id;
        customer.AddProject(this);
    }

    public void AddTimeEntry(TimeEntry entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (entry.ProjectId != Id)
        {
            throw new InvalidOperationException("Time entry belongs to a different project.");
        }

        if (_entries.Contains(entry))
        {
            return;
        }

        _entries.Add(entry);
    }

    private void Touch() => LastModifiedUtc = DateTime.UtcNow;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 200)
        {
            throw new ArgumentException("Project name cannot exceed 200 characters.", nameof(name));
        }

        return trimmed;
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
