using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;

namespace ChangeOrder.Domain.Tests.ValueObjects;

/// <summary>
/// Tests unitarios para <see cref="OrderNumber"/>.
/// </summary>
public sealed class OrderNumberTests
{
    /// <summary>
    /// Create con fecha y secuencia válidas produce el formato yyyyMMdd-## esperado.
    /// </summary>
    [Fact]
    public void Create_ValidDateAndSequence_ReturnsFormattedValue()
    {
        // Arrange
        DateTime date = new(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);
        int sequence = 5;

        // Act
        OrderNumber number = OrderNumber.Create(date, sequence);

        // Assert
        number.Value.Should().Be("20260613-05");
    }

    /// <summary>
    /// Secuencia 1 produce sufijo "01", no "1" — valida el padding con D2.
    /// </summary>
    [Fact]
    public void Create_Sequence1_ReturnsPaddedSequence()
    {
        // Arrange
        DateTime date = new(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

        // Act
        OrderNumber number = OrderNumber.Create(date, 1);

        // Assert
        number.Value.Should().Be("20260613-01");
        number.Value.Should().NotBe("20260613-1");
    }

    /// <summary>
    /// Dos instancias creadas con la misma fecha y secuencia son iguales por valor (record equality).
    /// </summary>
    [Fact]
    public void Create_SameDateAndSequence_AreEqual()
    {
        // Arrange
        DateTime date = new(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc);

        // Act
        OrderNumber first = OrderNumber.Create(date, 3);
        OrderNumber second = OrderNumber.Create(date, 3);

        // Assert
        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }
}
