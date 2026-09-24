using AuthMicroservice.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthMicroservice.Core.Data.Configurations;

internal sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("OtpCodes");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.CodeHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(o => o.Salt)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(o => o.Purpose)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.IpAddress)
            .HasMaxLength(64);

        builder.HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.UserId, o.Purpose, o.ConsumedAt });
    }
}
