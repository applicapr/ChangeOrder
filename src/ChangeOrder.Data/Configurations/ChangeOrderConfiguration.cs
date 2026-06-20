using ChangeOrder.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChangeOrder.Data.Configurations;

/// <summary>
/// Configuración de EF Core para ChangeOrderEntity.
/// Mapea la entidad completa a la tabla dbo.ChangeOrders incluyendo
/// Value Objects, enums, auditoría y soft-delete.
/// </summary>

public sealed class ChangeOrderConfiguration : IEntityTypeConfiguration<ChangeOrderEntity>
{
    /// <summary>
    /// Aplica el mapeo completo de la entidad a la tabla SQL.
    /// </summary>

    public void Configure(EntityTypeBuilder<ChangeOrderEntity> builder)
    {
        builder.ToTable("ChangeOrders");
        builder.HasKey(x => x.Id);

        // --- Numero de orden (Value Object) ---
        builder.OwnsOne(x => x.Number, n =>
        {
            n.Property(p => p.Value)
            .HasColumnName("OrderNumber")
            .HasMaxLength(13)
            .IsRequired();
            n.HasIndex(p => p.Value).IsUnique();
        });

        // --- Informacion del programa ---
        builder.Property(x => x.ProgramName)
        .HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductionVersion)
        .HasMaxLength(50).IsRequired();
        builder.Property(x => x.VersionScreenshotPath)
        .HasMaxLength(500);

        // --- Solicitud ---
        builder.Property(x => x.RequestDate).IsRequired();
        builder.Property(x => x.WorkDescription)
        .HasMaxLength(500).IsRequired();
        builder.Property(x => x.RequestDetails)
        .HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.Justification)
        .HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.RequiredAction)
        .HasColumnType("nvarchar(max)").IsRequired();

        // --- Solicitante (Value Object) ---
        builder.OwnsOne(x => x.Requester, r =>
        {
            r.Property(p => p.Name).HasMaxLength(150).IsRequired();
            r.Property(p => p.Position).HasMaxLength(100).IsRequired();
            r.Property(p => p.Department).HasMaxLength(100).IsRequired();
            r.Property(p => p.Email).HasMaxLength(200).IsRequired();
        });

        // --- Aprobaciones (Value Object) ---
        builder.OwnsOne(x => x.Approval, a => {
            a.Property(p => p.RequesterApproval)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            a.Property(p => p.DepartmentHeadApproval)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            a.Property(p => p.ItHeadApproval)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            a.Property(p => p.ProgrammingDivisionApproval)
                .HasConversion<string>().HasMaxLength(20).IsRequired();
        });

        // --- Estado y fechas de seguimiento ---
        builder.Property(x => x.Status)
        .HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(x => x.DeliveryDate);
        builder.Property(x => x.InitialEvaluationDate);
        builder.Property(x => x.ProductionDeployDate);
        builder.Property(x => x.PostDeployScreenshotPath)
        .HasMaxLength(500);

        // --- Auditoria ---
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt);

        // --- Soft Delete ---
        builder.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.DeletedAt);
        builder.HasQueryFilter(x => !x.IsDeleted);

        // --- Concurrencia optimista ---
        builder.Property(x => x.RowVersion)
        .IsRowVersion();
    }
}
