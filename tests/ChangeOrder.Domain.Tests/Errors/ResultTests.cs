using ChangeOrder.Domain.Errors;
using FluentAssertions;

namespace ChangeOrder.Domain.Tests.Errors;

/// <summary>
/// Tests unitarios para <see cref="Result{TValue, TError}"/>.
/// </summary>
public sealed class ResultTests
{
    /// <summary>
    /// Success establece IsSuccess en true y Value con el dato proporcionado.
    /// </summary>
    [Fact]
    public void Success_IsSuccessTrue_ValueSet()
    {
        // Arrange & Act
        Result<Guid, Error> result = Result<Guid, Error>.Success(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Failure establece IsSuccess en false y Error con el objeto proporcionado.
    /// </summary>
    [Fact]
    public void Failure_IsSuccessFalse_ErrorSet()
    {
        // Arrange
        Error error = new("Test.Code", "Test message");

        // Act
        Result<Guid, Error> result = Result<Guid, Error>.Failure(error);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    /// <summary>
    /// Un resultado exitoso tiene Error nulo.
    /// </summary>
    [Fact]
    public void Success_ErrorIsNull()
    {
        // Arrange & Act
        Result<string, Error> result = Result<string, Error>.Success("value");

        // Assert
        result.Error.Should().BeNull();
    }

    /// <summary>
    /// Un resultado fallido tiene Value nulo.
    /// </summary>
    [Fact]
    public void Failure_ValueIsNull()
    {
        // Arrange
        Error error = new("Test.Code", "Test message");

        // Act
        Result<string, Error> result = Result<string, Error>.Failure(error);

        // Assert
        result.Value.Should().BeNull();
    }
}
