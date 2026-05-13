using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace ChangeOrder.Domain.Tests.ValueObjects;

public sealed class OrderNumberTests
{
    [Fact]
    public void Create_WithValidSequence_ReturnsOrderNumber()
    {
        DateOnly date = new(2026, 5, 12);

        Result<OrderNumber, Error> result = OrderNumber.Create(date, 7);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("20260512-07");
    }

    [Fact]
    public void Create_WithSequenceZero_FailsWithDailySequenceExhausted()
    {
        DateOnly date = new(2026, 5, 12);

        Result<OrderNumber, Error> result = OrderNumber.Create(date, 0);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("order.daily_sequence_exhausted");
    }

    [Fact]
    public void Create_WithSequence100_FailsWithDailySequenceExhausted()
    {
        DateOnly date = new(2026, 5, 12);

        Result<OrderNumber, Error> result = OrderNumber.Create(date, 100);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("order.daily_sequence_exhausted");
    }

    [Theory]
    [InlineData(1, "20260512-01")]
    [InlineData(9, "20260512-09")]
    [InlineData(10, "20260512-10")]
    [InlineData(99, "20260512-99")]
    public void Create_FormatsAsExpected(int sequence, string expected)
    {
        DateOnly date = new(2026, 5, 12);

        Result<OrderNumber, Error> result = OrderNumber.Create(date, sequence);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be(expected);
    }
}
