using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using DomainApprovalChain = ChangeOrder.Domain.ValueObjects.ApprovalChain;

namespace ChangeOrder.Domain.Entities;

/// <summary>
/// Aggregate root that represents a production-change request and its workflow.
/// Embeds three value objects (<see cref="OrderNumber"/>, <see cref="RequesterInfo"/>,
/// <see cref="ApprovalChain"/>) and carries a SQL Server <c>rowversion</c> token
/// (<see cref="RowVersion"/>) for optimistic concurrency (FR-013).
/// </summary>
/// <remarks>
/// Audit / soft-delete columns are populated exclusively by the
/// <c>AuditInterceptor</c> (Constitution Principle IV). Status transitions are
/// validated by the methods exposed below, which return <see cref="Result{TValue, TError}"/>
/// instead of throwing.
/// </remarks>
public sealed class ChangeOrder : IAuditable, ISoftDeletable
{
    /// <summary>Primary key (clustered).</summary>
    public Guid Id { get; private set; }

    /// <summary>Public identifier in <c>yyyyMMdd-##</c> form.</summary>
    public OrderNumber OrderNumber { get; private set; } = default!;

    /// <summary>Subject application impacted by the change.</summary>
    public string ProgramName { get; private set; } = default!;

    /// <summary>Production version currently deployed before the change.</summary>
    public string ProductionVersion { get; private set; } = default!;

    /// <summary>Optional path to a pre-change screenshot evidence file.</summary>
    public string? VersionScreenshotPath { get; private set; }

    /// <summary>UTC instant at which the request was submitted (FR-1).</summary>
    public DateTime RequestDate { get; private set; }

    /// <summary>Snapshot of the submitting person at creation time.</summary>
    public RequesterInfo Requester { get; private set; } = default!;

    /// <summary>Short description of the work to be performed.</summary>
    public string WorkDescription { get; private set; } = default!;

    /// <summary>Detailed reproduction / specification of the change.</summary>
    public string RequestDetails { get; private set; } = default!;

    /// <summary>Business justification for the change.</summary>
    public string Justification { get; private set; } = default!;

    /// <summary>Concrete action(s) required to fulfil the request.</summary>
    public string RequiredAction { get; private set; } = default!;

    /// <summary>Embedded four-slot approval chain.</summary>
    public DomainApprovalChain ApprovalChain { get; private set; } = DomainApprovalChain.Empty;

    /// <summary>Current workflow state.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>UTC instant at which the change was delivered to the staging environment.</summary>
    public DateTime? DeliveryDate { get; private set; }

    /// <summary>UTC instant of the initial QA evaluation.</summary>
    public DateTime? InitialEvaluationDate { get; private set; }

    /// <summary>UTC instant of the production deployment.</summary>
    public DateTime? ProductionDeployDate { get; private set; }

    /// <summary>Optional path to a post-deploy screenshot evidence file.</summary>
    public string? PostDeployScreenshotPath { get; private set; }

    /// <inheritdoc/>
    public DateTime CreatedAt { get; set; }

    /// <inheritdoc/>
    public DateTime? UpdatedAt { get; set; }

    /// <inheritdoc/>
    public bool IsDeleted { get; set; }

    /// <inheritdoc/>
    public DateTime? DeletedAt { get; set; }

    /// <summary>SQL Server <c>rowversion</c> concurrency token (FR-013).</summary>
    public byte[] RowVersion { get; private set; } = [];

    private ChangeOrder() { }

    /// <summary>
    /// Constructs a fresh order in <see cref="OrderStatus.Draft"/> with an empty
    /// approval chain. Public mutation surface is reduced to the workflow
    /// methods declared below.
    /// </summary>
    public ChangeOrder(
        OrderNumber orderNumber,
        DateTime requestDateUtc,
        RequesterInfo requester,
        ChangeOrderContent content)
    {
        ArgumentNullException.ThrowIfNull(orderNumber);
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(content);

        Id = Guid.NewGuid();
        OrderNumber = orderNumber;
        RequestDate = requestDateUtc;
        Requester = requester;
        ProgramName = content.ProgramName;
        ProductionVersion = content.ProductionVersion;
        VersionScreenshotPath = content.VersionScreenshotPath;
        WorkDescription = content.WorkDescription;
        RequestDetails = content.RequestDetails;
        Justification = content.Justification;
        RequiredAction = content.RequiredAction;
        Status = OrderStatus.Draft;
        ApprovalChain = DomainApprovalChain.Empty;
    }

    /// <summary>
    /// Replaces the editable content of the order. Only allowed while
    /// <see cref="Status"/> is <see cref="OrderStatus.Draft"/> (FR-006 / C-1).
    /// </summary>
    public Result<TVoid, Error> UpdateContent(RequesterInfo requester, ChangeOrderContent content)
    {
        ArgumentNullException.ThrowIfNull(requester);
        ArgumentNullException.ThrowIfNull(content);

        if (Status != OrderStatus.Draft)
        {
            return Result<TVoid, Error>.Failure(DomainErrors.Order.EditAfterDraft());
        }

        Requester = requester;
        ProgramName = content.ProgramName;
        ProductionVersion = content.ProductionVersion;
        VersionScreenshotPath = content.VersionScreenshotPath;
        WorkDescription = content.WorkDescription;
        RequestDetails = content.RequestDetails;
        Justification = content.Justification;
        RequiredAction = content.RequiredAction;
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    /// <summary>
    /// Transitions <see cref="OrderStatus.Draft"/> → <see cref="OrderStatus.PendingApproval"/>.
    /// </summary>
    public Result<TVoid, Error> SubmitForApproval()
    {
        if (Status != OrderStatus.Draft)
        {
            return Result<TVoid, Error>.Failure(
                DomainErrors.Order.InvalidStateTransition(Status, OrderStatus.PendingApproval));
        }

        Status = OrderStatus.PendingApproval;
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    /// <summary>
    /// Records a verdict on one of the four approval slots. May advance the
    /// status to <see cref="OrderStatus.Approved"/> when all four are
    /// <see cref="ApprovalStatus.Approved"/>.
    /// </summary>
    public Result<TVoid, Error> RecordApproval(ApprovalLevel level, ApprovalStatus verdict)
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.PendingApproval))
        {
            return Result<TVoid, Error>.Failure(
                DomainErrors.Order.InvalidStateTransition(Status, OrderStatus.PendingApproval));
        }

        ApprovalChain = level switch
        {
            ApprovalLevel.Requester => ApprovalChain with { RequesterApproval = verdict },
            ApprovalLevel.DepartmentHead => ApprovalChain with { DepartmentHeadApproval = verdict },
            ApprovalLevel.ItHead => ApprovalChain with { ItHeadApproval = verdict },
            ApprovalLevel.ProgrammingDivision => ApprovalChain with { ProgrammingDivisionApproval = verdict },
            _ => ApprovalChain
        };

        if (Status == OrderStatus.Draft)
        {
            Status = OrderStatus.PendingApproval;
        }

        if (ApprovalChain.AllApproved())
        {
            Status = OrderStatus.Approved;
        }

        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    /// <summary>
    /// Records the delivery date. Drives <see cref="OrderStatus.Approved"/> →
    /// <see cref="OrderStatus.InProgress"/>.
    /// </summary>
    public Result<TVoid, Error> RecordDeliveryDate(DateTime deliveryDateUtc)
    {
        if (Status != OrderStatus.Approved)
        {
            return Result<TVoid, Error>.Failure(
                DomainErrors.Order.InvalidStateTransition(Status, OrderStatus.InProgress));
        }

        DeliveryDate = deliveryDateUtc;
        Status = OrderStatus.InProgress;
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    /// <summary>Records the initial evaluation date. Does not change status.</summary>
    public Result<TVoid, Error> RecordInitialEvaluationDate(DateTime evaluationDateUtc)
    {
        if (Status is not (OrderStatus.InProgress or OrderStatus.Approved))
        {
            return Result<TVoid, Error>.Failure(
                DomainErrors.Order.InvalidStateTransition(Status, Status));
        }

        InitialEvaluationDate = evaluationDateUtc;
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    /// <summary>
    /// Records the production deployment date. Drives
    /// <see cref="OrderStatus.InProgress"/> → <see cref="OrderStatus.Deployed"/>.
    /// </summary>
    public Result<TVoid, Error> RecordProductionDeploy(DateTime deployDateUtc, string? postDeployScreenshotPath)
    {
        if (Status != OrderStatus.InProgress)
        {
            return Result<TVoid, Error>.Failure(
                DomainErrors.Order.InvalidStateTransition(Status, OrderStatus.Deployed));
        }

        ProductionDeployDate = deployDateUtc;
        PostDeployScreenshotPath = postDeployScreenshotPath;
        Status = OrderStatus.Deployed;
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    /// <summary>
    /// Cancels the order. Allowed from <see cref="OrderStatus.Draft"/>,
    /// <see cref="OrderStatus.PendingApproval"/> and <see cref="OrderStatus.InProgress"/>.
    /// Forbidden once <see cref="OrderStatus.Deployed"/> or already
    /// <see cref="OrderStatus.Cancelled"/>.
    /// </summary>
    public Result<TVoid, Error> Cancel()
    {
        if (Status is OrderStatus.Deployed or OrderStatus.Cancelled)
        {
            return Result<TVoid, Error>.Failure(
                DomainErrors.Order.InvalidStateTransition(Status, OrderStatus.Cancelled));
        }

        Status = OrderStatus.Cancelled;
        return Result<TVoid, Error>.Success(TVoid.Instance);
    }

    /// <summary>Sets the EF Core concurrency token explicitly from a client-provided value (FR-013).</summary>
    public void AttachConcurrencyToken(byte[] rowVersion)
    {
        ArgumentNullException.ThrowIfNull(rowVersion);
        RowVersion = rowVersion;
    }
}

