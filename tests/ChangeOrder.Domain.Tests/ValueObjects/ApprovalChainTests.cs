using ChangeOrder.Domain.Enums;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;

namespace ChangeOrder.Domain.Tests.ValueObjects;

/// <summary>
/// Tests unitarios para <see cref="ApprovalChain"/>.
/// </summary>
public sealed class ApprovalChainTests
{
    /// <summary>
    /// El constructor por defecto inicializa los cuatro niveles en Pending.
    /// </summary>
    [Fact]
    public void DefaultConstructor_AllApprovalsArePending()
    {
        // Arrange & Act
        ApprovalChain chain = new();

        // Assert
        chain.RequesterApproval.Should().Be(ApprovalStatus.Pending);
        chain.DepartmentHeadApproval.Should().Be(ApprovalStatus.Pending);
        chain.ItHeadApproval.Should().Be(ApprovalStatus.Pending);
        chain.ProgrammingDivisionApproval.Should().Be(ApprovalStatus.Pending);
    }

    /// <summary>
    /// Una expresión with sólo cambia el nivel indicado; los demás quedan igual que el original.
    /// </summary>
    [Fact]
    public void WithExpression_ChangesOnlySpecifiedApproval()
    {
        // Arrange
        ApprovalChain original = new();

        // Act
        ApprovalChain updated = original with { RequesterApproval = ApprovalStatus.Approved };

        // Assert
        updated.RequesterApproval.Should().Be(ApprovalStatus.Approved);
    }

    /// <summary>
    /// Los niveles no mencionados en el with mantienen el valor Pending original.
    /// </summary>
    [Fact]
    public void WithExpression_OtherApprovalsRemainPending()
    {
        // Arrange
        ApprovalChain original = new();

        // Act
        ApprovalChain updated = original with { DepartmentHeadApproval = ApprovalStatus.Rejected };

        // Assert
        updated.RequesterApproval.Should().Be(ApprovalStatus.Pending);
        updated.ItHeadApproval.Should().Be(ApprovalStatus.Pending);
        updated.ProgrammingDivisionApproval.Should().Be(ApprovalStatus.Pending);
    }
}
