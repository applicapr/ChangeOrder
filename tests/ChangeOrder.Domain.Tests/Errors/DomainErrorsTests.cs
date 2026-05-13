using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.Errors;
using FluentAssertions;
using Xunit;

namespace ChangeOrder.Domain.Tests.Errors;

public sealed class DomainErrorsTests
{
    [Fact]
    public void Order_NotFound_HasExpectedCode()
    {
        Error error = DomainErrors.Order.NotFound(Guid.NewGuid());
        error.Code.Should().Be("order.not_found");
    }

    [Fact]
    public void Order_DuplicateNumber_HasExpectedCode()
    {
        Error error = DomainErrors.Order.DuplicateNumber("20260512-01");
        error.Code.Should().Be("order.duplicate_number");
    }

    [Fact]
    public void Order_InvalidStateTransition_HasExpectedCode()
    {
        Error error = DomainErrors.Order.InvalidStateTransition(OrderStatus.Draft, OrderStatus.Approved);
        error.Code.Should().Be("order.invalid_transition");
    }

    [Fact]
    public void Order_EditAfterDraft_HasExpectedCode()
    {
        Error error = DomainErrors.Order.EditAfterDraft();
        error.Code.Should().Be("order.edit_after_draft");
    }

    [Fact]
    public void Order_DailySequenceExhausted_HasExpectedCode()
    {
        Error error = DomainErrors.Order.DailySequenceExhausted(new DateOnly(2026, 5, 12));
        error.Code.Should().Be("order.daily_sequence_exhausted");
    }

    [Fact]
    public void Order_ConcurrencyConflict_HasExpectedCode()
    {
        Error error = DomainErrors.Order.ConcurrencyConflict();
        error.Code.Should().Be("order.concurrency_conflict");
    }

    [Fact]
    public void Idempotency_PayloadDivergence_HasExpectedCode()
    {
        Error error = DomainErrors.Idempotency.PayloadDivergence("abc-123");
        error.Code.Should().Be("idempotency.payload_divergence");
    }
}
