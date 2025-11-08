using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTracker.Domain.Entities;

namespace TimeTracker.Persistence.Configurations;

internal sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("time_entries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedNever();

        builder.Property(entry => entry.CustomerId)
            .IsRequired();

        builder.Property(entry => entry.ProjectId)
            .IsRequired();

        builder.Property(entry => entry.StartLocal)
            .IsRequired();

        builder.Property(entry => entry.StartUtc)
            .IsRequired();

        builder.Property(entry => entry.Notes)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(entry => entry.Tag)
            .HasMaxLength(50);

        builder.Property(entry => entry.Billable)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(entry => entry.PendingSync)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(entry => entry.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(entry => entry.LastModifiedUtc)
            .IsRequired();

        builder.Property(entry => entry.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(entry => new { entry.ProjectId, entry.StartUtc });

        builder.HasOne(entry => entry.Customer)
            .WithMany()
            .HasForeignKey(entry => entry.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.Project)
            .WithMany(project => project.TimeEntries)
            .HasForeignKey(entry => entry.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
