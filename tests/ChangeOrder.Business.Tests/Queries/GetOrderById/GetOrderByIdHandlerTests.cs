using ChangeOrder.Business.Queries.GetOrderById;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Business.Tests.Queries.GetOrderById;

/// <summary>
/// Unit tests for <see cref="GetOrderByIdHandler"/>. Covers the happy path
/// (200) and the not-found path (404). Soft-delete invisibility is exercised
/// by <c>SoftDeleteQueryFilterTests</c> in the Data.Tests project.
/// </summary>
public sealed class GetOrderByIdHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_ExistingOrder_ReturnsSuccess()
    {
        DomainChangeOrder order = BuildOrder();
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(order.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(order));

        GetOrderByIdHandler handler = new(repository);
        GetOrderByIdQuery query = new(order.Id);

        Result<DomainChangeOrder, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task HandleAsync_MissingOrder_ReturnsNotFound()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DomainChangeOrder?>(null));

        GetOrderByIdHandler handler = new(repository);
        Guid missingId = Guid.NewGuid();
        GetOrderByIdQuery query = new(missingId);

        Result<DomainChangeOrder, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.not_found");
    }

    private static DomainChangeOrder BuildOrder()
    {
        Result<OrderNumber, Error> number = OrderNumber.Create(DateOnly.FromDateTime(FixedNowUtc), 1);
        RequesterInfo requester = new("Jane Doe", "QA Lead", "Quality", "jane.doe@example.com");
        ChangeOrderContent content = new(
            "BillingApp",
            "v1.0.0",
            null,
            "Fix rounding bug",
            "Add half-even rounding to totals.",
            "Customer complaints about cents off.",
            "Patch Module B, redeploy.");
        return new DomainChangeOrder(number.Value!, FixedNowUtc, requester, content);
    }
}
