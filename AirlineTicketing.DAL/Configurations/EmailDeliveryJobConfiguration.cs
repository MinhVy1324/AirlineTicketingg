using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class EmailDeliveryJobConfiguration : IEntityTypeConfiguration<EmailDeliveryJob>
{
    public void Configure(EntityTypeBuilder<EmailDeliveryJob> builder)
    {
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Actor).HasMaxLength(150);
        builder.Property(job => job.LastError).HasMaxLength(150);
        builder.Property(job => job.LeaseId).IsConcurrencyToken();
        builder.HasIndex(job => job.NextAttemptAt).HasFilter("\"CompletedAt\" IS NULL");
        builder.HasIndex(job => job.BookingId).IsUnique()
            .HasFilter("\"CompletedAt\" IS NULL AND \"BookingId\" IS NOT NULL");
        builder.HasOne<DatVe>().WithMany().HasForeignKey(job => job.BookingId).OnDelete(DeleteBehavior.Restrict);
    }
}
