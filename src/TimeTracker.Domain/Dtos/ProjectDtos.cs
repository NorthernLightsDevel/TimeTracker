using System;

namespace TimeTracker.Domain.Dtos;

public sealed record class ProjectDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    bool IsActive,
    DateTime CreatedUtc,
    DateTime LastModifiedUtc);

public sealed record class ProjectCreateDto(
    Guid CustomerId,
    string Name,
    bool IsActive);

public sealed record class ProjectUpdateDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    bool IsActive);
