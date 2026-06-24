using ChangeOrder.Domain.Errors;
using FluentAssertions;

namespace ChangeOrder.Domain.Tests.Errors;

/// <summary>
/// Tests unitarios para <see cref="DomainErrors"/>.
/// Verifica que los códigos de error del dominio no cambien accidentalmente.
/// </summary>
public sealed class DomainErrorsTests
{
    /// <summary>
    /// Order.NotFound tiene el código "Order.NotFound".
    /// </summary>
    [Fact]
    public void Order_NotFound_HasCorrectCode()
    {
        DomainErrors.Order.NotFound.Code.Should().Be("Order.NotFound");
    }

    /// <summary>
    /// Order.ValidationFailed tiene el código "Order.ValidationFailed".
    /// </summary>
    [Fact]
    public void Order_ValidationFailed_HasCorrectCode()
    {
        DomainErrors.Order.ValidationFailed.Code.Should().Be("Order.ValidationFailed");
    }

    /// <summary>
    /// Order.ConcurrencyConflict tiene el código "Order.ConcurrencyConflict".
    /// </summary>
    [Fact]
    public void Order_ConcurrencyConflict_HasCorrectCode()
    {
        DomainErrors.Order.ConcurrencyConflict.Code.Should().Be("Order.ConcurrencyConflict");
    }

    /// <summary>
    /// Order.IdempotencyKeyConflict tiene el código "Order.IdempotencyKeyConflict".
    /// </summary>
    [Fact]
    public void Order_IdempotencyKeyConflict_HasCorrectCode()
    {
        DomainErrors.Order.IdempotencyKeyConflict.Code.Should().Be("Order.IdempotencyKeyConflict");
    }
}
