using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TimeTracker.Application.Repositories;
using TimeTracker.Domain.Dtos;
using TimeTracker.Persistence;

namespace TimeTracker.Application.Tests;

public class SqliteFileSmokeTests
{
    [Fact]
    public async Task SqliteDatabase_AllowsBasicCrud()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"timetracker-smoke-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<TimeTrackerDbContext>()
                .UseSqlite($"Data Source={databasePath}", sqlite =>
                {
                    sqlite.MigrationsAssembly(TimeTrackerDbContext.SqliteMigrationsAssembly);
                })
                .Options;

            await using var context = new TimeTrackerDbContext(options);
            await context.Database.MigrateAsync();

            var customerRepository = new CustomerRepository(context);
            var projectRepository = new ProjectRepository(context);
            var timeEntryRepository = new TimeEntryRepository(context);

            var customer = await customerRepository.CreateAsync(new CustomerCreateDto("Smoke Customer"));
            var project = await projectRepository.CreateAsync(new ProjectCreateDto(customer.Id, "Smoke Project", true));

            var startLocal = DateTime.Now;
            var startUtc = DateTime.UtcNow;

            var entry = await timeEntryRepository.CreateAsync(new TimeEntryCreateDto(
                customer.Id,
                project.Id,
                startLocal,
                startUtc,
                "Smoke entry",
                true,
                "smoke"));

            Assert.NotEqual(Guid.Empty, entry.Id);
            Assert.Equal(project.Id, entry.ProjectId);

            var fetched = await timeEntryRepository.GetByProjectAsync(project.Id);
            Assert.Single(fetched);

            var deletionResult = await timeEntryRepository.DeleteAsync(entry.Id);
            Assert.True(deletionResult);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
