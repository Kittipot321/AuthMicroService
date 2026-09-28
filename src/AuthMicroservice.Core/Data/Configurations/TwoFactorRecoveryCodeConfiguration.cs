using AuthMicroservice.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthMicroservice.Core.Data.Configurations;

internal sealed class TwoFactorRecoveryCodeConfiguration : IEntityTypeConfiguration<TwoFactorRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TwoFactorRecoveryCode> builder)
    {
        builder.ToTable("TwoFactorRecoveryCodes");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.CodeHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(r => r.Salt)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(r => r.IpAddress)
            .HasMaxLength(64);

        builder.HasOne(r => r.User)
            .WithMany(u => u.RecoveryCodes)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.UserId, r.ConsumedAt });
    }
}
