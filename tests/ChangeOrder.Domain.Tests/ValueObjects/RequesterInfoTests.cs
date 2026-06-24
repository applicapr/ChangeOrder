using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;

namespace ChangeOrder.Domain.Tests.ValueObjects;

/// <summary>
/// Tests unitarios para <see cref="RequesterInfo"/>.
/// </summary>
public sealed class RequesterInfoTests
{
    /// <summary>
    /// El constructor asigna correctamente todas las propiedades.
    /// </summary>
    [Fact]
    public void Constructor_ValidData_SetsAllProperties()
    {
        // Arrange & Act
        RequesterInfo info = new("Ana García", "Analista", "Sistemas", "ana@example.com");

        // Assert
        info.Name.Should().Be("Ana García");
        info.Position.Should().Be("Analista");
        info.Department.Should().Be("Sistemas");
        info.Email.Should().Be("ana@example.com");
    }

    /// <summary>
    /// Dos instancias con los mismos datos son iguales por valor (record equality).
    /// </summary>
    [Fact]
    public void TwoInstancesWithSameData_AreEqual()
    {
        // Arrange
        RequesterInfo first = new("Ana García", "Analista", "Sistemas", "ana@example.com");
        RequesterInfo second = new("Ana García", "Analista", "Sistemas", "ana@example.com");

        // Act & Assert
        first.Should().Be(second);
        (first == second).Should().BeTrue();
    }
}
