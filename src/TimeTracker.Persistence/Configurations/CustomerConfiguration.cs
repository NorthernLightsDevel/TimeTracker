using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TimeTracker.Domain.Entities;

namespace TimeTracker.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .ValueGeneratedNever();

        builder.Property(customer => customer.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(customer => customer.CreatedUtc)
            .IsRequired();

        builder.Property(customer => customer.LastModifiedUtc)
            .IsRequired();

        builder.Property(customer => customer.IsArchived)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Navigation(customer => customer.Projects)
            .HasField("_projects")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
