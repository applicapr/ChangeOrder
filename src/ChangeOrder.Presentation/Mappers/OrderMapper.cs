using ChangeOrder.Business.Commands.CreateOrder;
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
}
