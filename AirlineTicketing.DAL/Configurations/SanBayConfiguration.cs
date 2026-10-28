using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AirlineTicketing.DAL.Configurations;

public sealed class SanBayConfiguration : IEntityTypeConfiguration<SanBay>
{
    public void Configure(EntityTypeBuilder<SanBay> builder)
    {
        builder.HasKey(sanBay => sanBay.Id);
        builder.Property(sanBay => sanBay.MaSanBay).HasMaxLength(3).IsRequired();
        builder.HasIndex(sanBay => sanBay.MaSanBay).IsUnique();
        builder.Property(sanBay => sanBay.TenSanBay).HasMaxLength(150).IsRequired();
        builder.Property(sanBay => sanBay.ThanhPho).HasMaxLength(100).IsRequired();
        builder.Property(sanBay => sanBay.QuocGia).HasMaxLength(100).IsRequired();
    }
}
