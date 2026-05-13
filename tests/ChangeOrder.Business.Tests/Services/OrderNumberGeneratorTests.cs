using ChangeOrder.Business.Services;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ChangeOrder.Business.Tests.Services;

public sealed class OrderNumberGeneratorTests
{
    private static readonly DateOnly Today = new(2026, 5, 13);

    [Fact]
    public async Task GenerateAsync_FirstCallOfTheDay_ReturnsSequence01()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetNextSequenceForDateAsync(Today, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        OrderNumberGenerator generator = new(repository);

        Result<OrderNumber, Error> result = await generator.GenerateAsync(Today, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("20260513-01");
    }

    [Fact]
    public async Task GenerateAsync_AdvancingSequence_ReturnsIncrementedOrderNumber()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        int[] sequences = [1, 2, 3];
        int call = 0;
        repository.GetNextSequenceForDateAsync(Today, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(sequences[call++]));
        OrderNumberGenerator generator = new(repository);

        Result<OrderNumber, Error> first = await generator.GenerateAsync(Today, CancellationToken.None);
        Result<OrderNumber, Error> second = await generator.GenerateAsync(Today, CancellationToken.None);
        Result<OrderNumber, Error> third = await generator.GenerateAsync(Today, CancellationToken.None);

        first.Value!.Value.Should().Be("20260513-01");
        second.Value!.Value.Should().Be("20260513-02");
        third.Value!.Value.Should().Be("20260513-03");
    }

    [Fact]
    public async Task GenerateAsync_WhenSequenceExceeds99_ReturnsDailySequenceExhausted()
    {
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        repository.GetNextSequenceForDateAsync(Today, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(100));
        OrderNumberGenerator generator = new(repository);

        Result<OrderNumber, Error> result = await generator.GenerateAsync(Today, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("order.daily_sequence_exhausted");
    }
}
