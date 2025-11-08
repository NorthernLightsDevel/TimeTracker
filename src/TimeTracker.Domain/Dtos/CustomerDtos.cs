using System;

namespace TimeTracker.Domain.Dtos;

public sealed record class CustomerDto(
    Guid Id,
    string Name,
    bool IsArchived,
    DateTime CreatedUtc,
    DateTime LastModifiedUtc);

public sealed record class CustomerCreateDto(
    string Name);

public sealed record class CustomerUpdateDto(
    Guid Id,
    string Name,
    bool IsArchived);
