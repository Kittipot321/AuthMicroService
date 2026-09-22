using AuthMicroservice.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthMicroservice.Core.Data.Configurations;

internal sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.Property(r => r.Description)
            .HasMaxLength(256);

        builder.Property(r => r.IsSystem)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired();
    }
}
