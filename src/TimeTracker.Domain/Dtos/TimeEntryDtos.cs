using System;

namespace TimeTracker.Domain.Dtos;

public sealed record class TimeEntryDto(
    Guid Id,
    Guid CustomerId,
    Guid ProjectId,
    DateTime StartLocal,
    DateTime? EndLocal,
    DateTime StartUtc,
    DateTime? EndUtc,
    string Notes,
    bool Billable,
    string Tag,
    string ServerId,
    bool PendingSync,
    bool IsDeleted,
    DateTime LastModifiedUtc,
    byte[] RowVersion);

public sealed record class TimeEntryCreateDto(
    Guid CustomerId,
    Guid ProjectId,
    DateTime StartLocal,
    DateTime StartUtc,
    string Notes,
    bool Billable,
    string Tag);

public sealed record class TimeEntryUpdateDto(
    Guid Id,
    DateTime? StartLocal,
    DateTime? StartUtc,
    DateTime? EndLocal,
    DateTime? EndUtc,
    string Notes,
    bool? Billable,
    string Tag,
    bool? PendingSync,
    bool? IsDeleted,
    string ServerId);
