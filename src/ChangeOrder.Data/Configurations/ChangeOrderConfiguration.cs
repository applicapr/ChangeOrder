using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainApprovalChain = ChangeOrder.Domain.ValueObjects.ApprovalChain;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Data.Configurations;

/// <summary>
/// EF Core configuration for the <c>ChangeOrder</c> aggregate.
/// Translates the data-model.md §1 contract into a relational schema: a single
/// <c>dbo.ChangeOrders</c> table that flattens the three embedded value
/// objects and indexes the columns required by the listing/filter use cases.
/// </summary>
public sealed class ChangeOrderConfiguration : IEntityTypeConfiguration<DomainChangeOrder>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<DomainChangeOrder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ChangeOrders", "dbo");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.OwnsOne(o => o.OrderNumber, on =>
        {
            on.Property(p => p.Value)
                .HasColumnName("OrderNumber")
                .HasColumnType("varchar(13)")
                .IsRequired();

            on.HasIndex(p => p.Value)
                .IsUnique()
                .HasDatabaseName("IX_ChangeOrders_OrderNumber");
        });

        builder.Property(o => o.ProgramName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(o => o.ProductionVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.VersionScreenshotPath)
            .HasMaxLength(500);

        builder.Property(o => o.RequestDate)
            .IsRequired();

        builder.OwnsOne(o => o.Requester, r =>
        {
            r.Property(p => p.Name)
                .HasColumnName("Requester_Name")
                .HasMaxLength(150)
                .IsRequired();
            r.Property(p => p.Position)
                .HasColumnName("Requester_Position")
                .HasMaxLength(100)
                .IsRequired();
            r.Property(p => p.Department)
                .HasColumnName("Requester_Department")
                .HasMaxLength(100)
                .IsRequired();
            r.Property(p => p.Email)
                .HasColumnName("Requester_Email")
                .HasMaxLength(200)
                .IsRequired();
        });

        builder.Property(o => o.WorkDescription)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(o => o.RequestDetails)
            .HasMaxLength(4000)
            .IsRequired();
        builder.Property(o => o.Justification)
            .HasMaxLength(2000)
            .IsRequired();
        builder.Property(o => o.RequiredAction)
            .HasMaxLength(1000)
            .IsRequired();

        builder.OwnsOne(o => o.ApprovalChain, a =>
        {
            a.Property(p => p.RequesterApproval)
                .HasColumnName("Approval_Requester")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(ApprovalStatus.Pending)
                .IsRequired();
            a.Property(p => p.DepartmentHeadApproval)
                .HasColumnName("Approval_DepartmentHead")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(ApprovalStatus.Pending)
                .IsRequired();
            a.Property(p => p.ItHeadApproval)
                .HasColumnName("Approval_ItHead")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(ApprovalStatus.Pending)
                .IsRequired();
            a.Property(p => p.ProgrammingDivisionApproval)
                .HasColumnName("Approval_ProgrammingDivision")
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(ApprovalStatus.Pending)
                .IsRequired();
        });

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(OrderStatus.Draft)
            .IsRequired();

        builder.Property(o => o.DeliveryDate);
        builder.Property(o => o.InitialEvaluationDate);
        builder.Property(o => o.ProductionDeployDate);
        builder.Property(o => o.PostDeployScreenshotPath).HasMaxLength(500);

        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.UpdatedAt);
        builder.Property(o => o.DeletedAt);
        builder.Property(o => o.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(o => o.RowVersion)
            .IsRowVersion();

        builder.HasIndex(o => o.RequestDate)
            .HasDatabaseName("IX_ChangeOrders_RequestDate");
        builder.HasIndex(o => o.Status)
            .HasDatabaseName("IX_ChangeOrders_Status");
        builder.HasIndex(o => o.IsDeleted)
            .HasDatabaseName("IX_ChangeOrders_IsDeleted");

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
