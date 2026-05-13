using ChangeOrder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Data.Configurations;

/// <summary>
/// EF Core configuration for the technical <see cref="IdempotencyKey"/> entity.
/// The table is intentionally NOT soft-deletable: the cleanup background
/// service hard-deletes rows older than 24h.
/// </summary>
public sealed class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("IdempotencyKeys", "dbo");
        builder.HasKey(k => k.Key);

        builder.Property(k => k.Key)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(k => k.OrderId)
            .IsRequired();

        builder.Property(k => k.RequestHash)
            .HasColumnType("varbinary(32)")
            .IsRequired();

        builder.Property(k => k.CreatedAt)
            .IsRequired();

        builder.HasOne<DomainChangeOrder>()
            .WithMany()
            .HasForeignKey(k => k.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(k => k.CreatedAt)
            .HasDatabaseName("IX_IdempotencyKeys_CreatedAt");
    }
}
