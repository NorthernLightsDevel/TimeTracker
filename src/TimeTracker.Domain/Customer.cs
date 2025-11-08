using System;
using System.Collections.Generic;

namespace TimeTracker.Domain.Entities;

public sealed class Customer
{
    private readonly List<Project> _projects = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime LastModifiedUtc { get; private set; }
    public bool IsArchived { get; private set; }
    public IReadOnlyList<Project> Projects => _projects;

    private Customer()
    {
        Name = string.Empty;
        CreatedUtc = DateTime.UtcNow;
        LastModifiedUtc = CreatedUtc;
    }

    public Customer(Guid id, string name, DateTime createdUtc, bool isArchived = false)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Customer id cannot be empty.", nameof(id));
        }

        Id = id;
        Name = NormalizeName(name);
        CreatedUtc = EnsureUtc(createdUtc);
        LastModifiedUtc = CreatedUtc;
        IsArchived = isArchived;
    }

    public Customer(string name)
        : this(Guid.NewGuid(), name, DateTime.UtcNow)
    {
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        Touch();
    }

    public void SetArchived(bool archived)
    {
        if (IsArchived == archived)
        {
            return;
        }

        IsArchived = archived;
        Touch();
    }

    public void AddProject(Project project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (project.CustomerId != Id)
        {
            throw new InvalidOperationException("Project belongs to a different customer.");
        }

        if (_projects.Contains(project))
        {
            return;
        }

        _projects.Add(project);
    }

    private void Touch() => LastModifiedUtc = DateTime.UtcNow;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Customer name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > 200)
        {
            throw new ArgumentException("Customer name cannot exceed 200 characters.", nameof(name));
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
