using ChangeOrder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChangeOrder.Data.Configurations;

/// <summary>
/// Configuración de EF Core para IdempotencyRecord.
/// </summary>
public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    /// <summary>
    /// Aplica el mapeo de la entidad a la tabla SQL.
    /// </summary>
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(x => x.Key);

        builder.Property(x => x.Key).IsRequired();
        builder.Property(x => x.PayloadHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResourceId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.CreatedAt);
    }
}
