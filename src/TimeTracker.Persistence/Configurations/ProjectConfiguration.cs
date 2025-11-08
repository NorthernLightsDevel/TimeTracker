using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTracker.Domain.Entities;

namespace TimeTracker.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Id)
            .ValueGeneratedNever();

        builder.Property(project => project.CustomerId)
            .IsRequired();

        builder.Property(project => project.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(project => project.CreatedUtc)
            .IsRequired();

        builder.Property(project => project.LastModifiedUtc)
            .IsRequired();

        builder.Property(project => project.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.HasIndex(project => new { project.CustomerId, project.Name })
            .IsUnique();

        builder.HasOne(project => project.Customer)
            .WithMany(customer => customer.Projects)
            .HasForeignKey(project => project.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.TimeEntries)
            .WithOne(t => t.Project)
            .HasForeignKey(t => t.ProjectId);

        builder.Navigation(project => project.TimeEntries)
            .HasField("_entries")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
