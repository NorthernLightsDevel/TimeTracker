using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeTracker.Application.Repositories;
using TimeTracker.Application.Tests.Infrastructure;
using TimeTracker.Domain.Dtos;

namespace TimeTracker.Application.Tests;

[TestCaseOrderer("TimeTracker.Application.Tests.Infrastructure.DatabaseProviderTestCaseOrderer", "TimeTracker.Application.Tests")]
public class RepositoryTests
{
    public static IEnumerable<object[]> Providers()
    {
        yield return new object[] { DatabaseProvider.Sqlite };
        yield return new object[] { DatabaseProvider.PgSql };
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task CustomerRepository_PerformsCrud(DatabaseProvider provider)
    {
        await using var harness = await DatabaseHarness.CreateAsync(provider);

        var created = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Acme Corp"));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Acme Corp", created.Name);
        Assert.False(created.IsArchived);

        var fetched = await harness.CustomerRepository.GetByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched!.Id);

        var updated = await harness.CustomerRepository.UpdateAsync(new CustomerUpdateDto(created.Id, "Acme Updated", true));
        Assert.NotNull(updated);
        Assert.True(updated!.IsArchived);
        Assert.Equal("Acme Updated", updated.Name);

        var all = await harness.CustomerRepository.GetAllAsync();
        Assert.Single(all);

        var deleteResult = await harness.CustomerRepository.DeleteAsync(created.Id);
        Assert.True(deleteResult);

        var missing = await harness.CustomerRepository.GetByIdAsync(created.Id);
        Assert.Null(missing);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task ProjectRepository_PerformsCrud(DatabaseProvider provider)
    {
        await using var harness = await DatabaseHarness.CreateAsync(provider);

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Customer"));

        var created = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project Alpha", true));
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.True(created.IsActive);

        var byCustomer = await harness.ProjectRepository.GetByCustomerAsync(customer.Id, includeInactive: true);
        Assert.Single(byCustomer);

        var updated = await harness.ProjectRepository.UpdateAsync(new ProjectUpdateDto(created.Id, customer.Id, "Project Beta", false));
        Assert.NotNull(updated);
        Assert.Equal("Project Beta", updated!.Name);
        Assert.False(updated.IsActive);

        var deleteResult = await harness.ProjectRepository.DeleteAsync(created.Id);
        Assert.True(deleteResult);
        var remaining = await harness.ProjectRepository.GetByCustomerAsync(customer.Id, includeInactive: true);
        Assert.Empty(remaining);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task TimeEntryRepository_PerformsCrud(DatabaseProvider provider)
    {
        await using var harness = await DatabaseHarness.CreateAsync(provider);

        var customer = await harness.CustomerRepository.CreateAsync(new CustomerCreateDto("Customer"));
        var project = await harness.ProjectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Project", true));

        var startLocal = DateTime.Now;
        var startUtc = DateTime.UtcNow;

        var created = await harness.TimeEntryRepository.CreateAsync(new TimeEntryCreateDto(
            customer.Id,
            project.Id,
            startLocal,
            startUtc,
            "Initial work",
            true,
            "tag1"));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.True(created.Billable);
        Assert.Equal("tag1", created.Tag);

        var active = await harness.TimeEntryRepository.GetActiveAsync();
        Assert.NotNull(active);
        Assert.Equal(created.Id, active!.Id);

        var rangeStart = startUtc.AddMinutes(-1);
        var rangeEnd = startUtc.AddMinutes(10);
        var entries = await harness.TimeEntryRepository.GetByProjectAsync(project.Id, rangeStart, rangeEnd);
        Assert.Single(entries);

        var byDate = await harness.TimeEntryRepository.GetByLocalDateAsync(DateOnly.FromDateTime(startLocal));
        Assert.Single(byDate);

        var updated = await harness.TimeEntryRepository.UpdateAsync(new TimeEntryUpdateDto(
            created.Id,
            StartLocal: null,
            StartUtc: null,
            EndLocal: startLocal.AddHours(1),
            EndUtc: startUtc.AddHours(1),
            Notes: "Completed",
            Billable: false,
            Tag: "tag2",
            PendingSync: false,
            IsDeleted: false,
            ServerId: "srv-123"));

        Assert.NotNull(updated);
        Assert.False(updated!.Billable);
        Assert.Equal("Completed", updated.Notes);
        Assert.Equal("tag2", updated.Tag);
        Assert.NotNull(updated.EndUtc);

        var adjustedStartLocal = startLocal.AddMinutes(-30);
        var startAdjusted = await harness.TimeEntryRepository.UpdateAsync(new TimeEntryUpdateDto(
            updated.Id,
            StartLocal: adjustedStartLocal,
            StartUtc: startUtc.AddMinutes(-30),
            EndLocal: null,
            EndUtc: null,
            Notes: null,
            Billable: null,
            Tag: null,
            PendingSync: null,
            IsDeleted: null,
            ServerId: null));

        Assert.NotNull(startAdjusted);
        Assert.Equal(adjustedStartLocal, startAdjusted!.StartLocal);

        var activeAfterStop = await harness.TimeEntryRepository.GetActiveAsync();
        Assert.Null(activeAfterStop);

        var mostRecent = await harness.TimeEntryRepository.GetMostRecentAsync();
        Assert.NotNull(mostRecent);
        Assert.Equal(updated.Id, mostRecent!.Id);

        var deleteResult = await harness.TimeEntryRepository.DeleteAsync(created.Id);
        Assert.True(deleteResult);

        var remaining = await harness.TimeEntryRepository.GetByProjectAsync(project.Id, rangeStart, rangeEnd);
        Assert.Empty(remaining);
    }
}
