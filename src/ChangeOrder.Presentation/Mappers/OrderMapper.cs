using ChangeOrder.Business.Commands.CreateOrder;
using ChangeOrder.Business.Commands.RecordApproval;
using ChangeOrder.Business.Commands.RecordMilestoneDates;
using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Presentation.DTOs.Requests;
using ChangeOrder.Presentation.DTOs.Responses;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Presentation.Mappers;

/// <summary>
/// Static manual mapper between Presentation DTOs and Business
/// commands / Domain entities. Constitution Principle V (NON-NEGOTIABLE)
/// forbids reflection-based mappers (AutoMapper, Mapster) — every
/// property-by-property assignment is written by hand for transparency.
/// </summary>
public static class OrderMapper
{
    /// <summary>Builds a <see cref="CreateOrderCommand"/> from the incoming HTTP request.</summary>
    /// <param name="request">DTO bound from the JSON body.</param>
    /// <param name="idempotencyKey">Value of the <c>Idempotency-Key</c> header.</param>
    public static CreateOrderCommand ToCommand(this CreateOrderRequest request, string idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new CreateOrderCommand(
            IdempotencyKey: idempotencyKey,
            ProgramName: request.ProgramName,
            ProductionVersion: request.ProductionVersion,
            VersionScreenshotPath: request.VersionScreenshotPath,
            WorkDescription: request.WorkDescription,
            RequestDetails: request.RequestDetails,
            Justification: request.Justification,
            RequiredAction: request.RequiredAction,
            RequesterName: request.Requester.Name,
            RequesterPosition: request.Requester.Position,
            RequesterDepartment: request.Requester.Department,
            RequesterEmail: request.Requester.Email);
    }

    /// <summary>
    /// Builds a <see cref="RecordApprovalCommand"/> from the URL parameters and
    /// JSON body of <c>PUT /api/v1/change-orders/{id}/approvals/{level}</c>.
    /// Parses both the <paramref name="level"/> route segment and the
    /// <c>verdict</c> body field; returns
    /// <see cref="DomainErrors.Order.InvalidStateTransition"/> shape only
    /// indirectly — invalid input here surfaces as a validation error.
    /// </summary>
    /// <param name="request">DTO bound from the JSON body.</param>
    /// <param name="orderId">Order id from the route.</param>
    /// <param name="level">Approval level string from the route.</param>
    public static Result<RecordApprovalCommand, Error> ToCommand(
        this ApprovalVerdictRequest request,
        Guid orderId,
        string level)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(level);

        if (!TryParseApprovalLevel(level, out ApprovalLevel parsedLevel))
        {
            return Result<RecordApprovalCommand, Error>.Failure(new Error(
                "validation.error",
                $"Unknown approval level '{level}'. Expected one of: requester, departmentHead, itHead, programmingDivision."));
        }

        if (!Enum.TryParse(request.Verdict, ignoreCase: true, out ApprovalStatus parsedVerdict))
        {
            return Result<RecordApprovalCommand, Error>.Failure(new Error(
                "validation.error",
                $"Unknown approval verdict '{request.Verdict}'. Expected one of: Pending, Approved, Rejected."));
        }

        return Result<RecordApprovalCommand, Error>.Success(
            new RecordApprovalCommand(orderId, parsedLevel, parsedVerdict));
    }

    /// <summary>
    /// Builds a <see cref="RecordMilestoneDatesCommand"/> from the URL <paramref name="orderId"/>
    /// and the JSON body of <c>PATCH /api/v1/change-orders/{id}/dates</c>.
    /// </summary>
    /// <param name="request">DTO bound from the JSON body.</param>
    /// <param name="orderId">Order id from the route.</param>
    public static RecordMilestoneDatesCommand ToCommand(this MilestoneDatesRequest request, Guid orderId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new RecordMilestoneDatesCommand(
            OrderId: orderId,
            DeliveryDate: NormalizeUtc(request.DeliveryDate),
            InitialEvaluationDate: NormalizeUtc(request.InitialEvaluationDate),
            ProductionDeployDate: NormalizeUtc(request.ProductionDeployDate),
            PostDeployScreenshotPath: request.PostDeployScreenshotPath);
    }

    /// <summary>Projects a persisted <see cref="DomainChangeOrder"/> onto the public <see cref="OrderResponse"/>.</summary>
    public static OrderResponse ToResponse(this DomainChangeOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        RequesterInfoResponse requester = new(
            order.Requester.Name,
            order.Requester.Position,
            order.Requester.Department,
            order.Requester.Email);

        ApprovalChainResponse approvalChain = new(
            order.ApprovalChain.RequesterApproval.ToString(),
            order.ApprovalChain.DepartmentHeadApproval.ToString(),
            order.ApprovalChain.ItHeadApproval.ToString(),
            order.ApprovalChain.ProgrammingDivisionApproval.ToString());

        return new OrderResponse(
            Id: order.Id,
            OrderNumber: order.OrderNumber.Value,
            Status: order.Status.ToString(),
            ProgramName: order.ProgramName,
            ProductionVersion: order.ProductionVersion,
            VersionScreenshotPath: order.VersionScreenshotPath,
            WorkDescription: order.WorkDescription,
            RequestDetails: order.RequestDetails,
            Justification: order.Justification,
            RequiredAction: order.RequiredAction,
            Requester: requester,
            ApprovalChain: approvalChain,
            RequestDate: order.RequestDate,
            DeliveryDate: order.DeliveryDate,
            InitialEvaluationDate: order.InitialEvaluationDate,
            ProductionDeployDate: order.ProductionDeployDate,
            PostDeployScreenshotPath: order.PostDeployScreenshotPath,
            CreatedAt: order.CreatedAt,
            UpdatedAt: order.UpdatedAt,
            RowVersion: Convert.ToBase64String(order.RowVersion));
    }

    private static bool TryParseApprovalLevel(string raw, out ApprovalLevel level)
    {
        if (string.Equals(raw, "requester", StringComparison.OrdinalIgnoreCase))
        {
            level = ApprovalLevel.Requester;
            return true;
        }

        if (string.Equals(raw, "departmentHead", StringComparison.OrdinalIgnoreCase))
        {
            level = ApprovalLevel.DepartmentHead;
            return true;
        }

        if (string.Equals(raw, "itHead", StringComparison.OrdinalIgnoreCase))
        {
            level = ApprovalLevel.ItHead;
            return true;
        }

        if (string.Equals(raw, "programmingDivision", StringComparison.OrdinalIgnoreCase))
        {
            level = ApprovalLevel.ProgrammingDivision;
            return true;
        }

        level = default;
        return false;
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        DateTime raw = value.Value;
        return raw.Kind switch
        {
            DateTimeKind.Utc => raw,
            DateTimeKind.Local => raw.ToUniversalTime(),
            _ => DateTime.SpecifyKind(raw, DateTimeKind.Utc)
        };
    }
}
