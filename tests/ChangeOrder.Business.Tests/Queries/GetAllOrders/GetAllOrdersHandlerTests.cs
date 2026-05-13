using ChangeOrder.Business.Common;
using ChangeOrder.Business.Queries.GetAllOrders;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;
using DomainPagedRequest = ChangeOrder.Domain.Abstractions.PagedRequest;

namespace ChangeOrder.Business.Tests.Queries.GetAllOrders;

/// <summary>
/// Unit tests for <see cref="GetAllOrdersHandler"/>. Exercises pagination
/// math, the page-size [1..50] guard and the "empty list" happy path. The
/// repository is faked so the handler can be tested in isolation.
/// </summary>
public sealed class GetAllOrdersHandlerTests
{
    private static readonly DateTime FixedNowUtc = new(2026, 5, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_EmptyRepository_ReturnsEmptyPage()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.ListAsync(Arg.Any<DomainPagedRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DomainChangeOrder>>(Array.Empty<DomainChangeOrder>()));
        repository.CountAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0));

        GetAllOrdersHandler handler = new(repository);
        GetAllOrdersQuery query = new(Page: 1, PageSize: 10);

        Result<PagedResponse<DomainChangeOrder>, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value!.TotalCount.Should().Be(0);
        result.Value!.Page.Should().Be(1);
        result.Value!.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_FirstPageOfThree_ReturnsTwoItemsAndTotalCount()
    {
        DomainChangeOrder first = BuildOrder(1);
        DomainChangeOrder second = BuildOrder(2);
        IReadOnlyList<DomainChangeOrder> page = new List<DomainChangeOrder> { first, second };

        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.ListAsync(Arg.Is<DomainPagedRequest>(p => p.Page == 1 && p.PageSize == 2), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(page));
        repository.CountAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(3));

        GetAllOrdersHandler handler = new(repository);
        GetAllOrdersQuery query = new(Page: 1, PageSize: 2);

        Result<PagedResponse<DomainChangeOrder>, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value!.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_LastPage_ReturnsTheTailRow()
    {
        DomainChangeOrder tail = BuildOrder(3);
        IReadOnlyList<DomainChangeOrder> page = new List<DomainChangeOrder> { tail };

        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.ListAsync(Arg.Is<DomainPagedRequest>(p => p.Page == 2 && p.PageSize == 2), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(page));
        repository.CountAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(3));

        GetAllOrdersHandler handler = new(repository);
        GetAllOrdersQuery query = new(Page: 2, PageSize: 2);

        Result<PagedResponse<DomainChangeOrder>, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value!.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task HandleAsync_PageBelowOne_ReturnsValidationError()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetAllOrdersHandler handler = new(repository);
        GetAllOrdersQuery query = new(Page: 0, PageSize: 10);

        Result<PagedResponse<DomainChangeOrder>, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("validation.error");
        await repository.DidNotReceive().ListAsync(Arg.Any<DomainPagedRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PageSizeAboveFifty_ReturnsValidationError()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetAllOrdersHandler handler = new(repository);
        GetAllOrdersQuery query = new(Page: 1, PageSize: 51);

        Result<PagedResponse<DomainChangeOrder>, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("validation.error");
        await repository.DidNotReceive().ListAsync(Arg.Any<DomainPagedRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PageSizeZero_ReturnsValidationError()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        GetAllOrdersHandler handler = new(repository);
        GetAllOrdersQuery query = new(Page: 1, PageSize: 0);

        Result<PagedResponse<DomainChangeOrder>, Error> result = await handler.HandleAsync(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("validation.error");
    }

    private static DomainChangeOrder BuildOrder(int sequence)
    {
        Result<OrderNumber, Error> number = OrderNumber.Create(DateOnly.FromDateTime(FixedNowUtc), sequence);
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
