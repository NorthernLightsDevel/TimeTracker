using System;

namespace TimeTracker.Domain.Dtos;

public sealed record class ProjectListItemDto(
    Guid ProjectId,
    Guid CustomerId,
    string CustomerName,
    string ProjectName,
    bool IsActive);
