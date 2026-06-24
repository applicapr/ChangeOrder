using ChangeOrder.Business.Abstractions;
using ChangeOrder.Business.Commands.CreateOrder;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.Errors;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ChangeOrder.Business.Tests.Commands;

/// <summary>
/// Tests unitarios para <see cref="CreateOrderHandler"/>.
/// </summary>
public sealed class CreateOrderHandlerTests
{
    private static CreateOrderCommand BuildValidCommand(Guid? idempotencyKey = null) => new(
        IdempotencyKey: idempotencyKey ?? Guid.NewGuid(),
        ProgramName: "TestApp",
        ProductionVersion: "v1.0.0",
        VersionScreenshotPath: null,
        RequestDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        WorkDescription: "Work description",
        RequestDetails: "Request details",
        Justification: "Justification",
        RequiredAction: "Required action",
        RequesterName: "Test User",
        RequesterPosition: "Developer",
        RequesterDepartment: "IT",
        RequesterEmail: "test@example.com"
    );

    /// <summary>
    /// Un command válido crea la orden, guarda el registro de idempotencia y retorna el Id generado.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ValidCommand_CreatesOrderAndReturnsId()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IOrderNumberGenerator generator = Substitute.For<IOrderNumberGenerator>();

        generator.GenerateAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(OrderNumber.Create(DateTime.UtcNow, 1));

        repository.GetIdempotencyRecordAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IdempotencyRecord?)null);

        CreateOrderHandler handler = new(repository, unitOfWork, generator);
        CreateOrderCommand command = BuildValidCommand();

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        await repository.Received(1).AddAsync(Arg.Any<ChangeOrderEntity>(), Arg.Any<CancellationToken>());
        await repository.Received(1).AddIdempotencyRecordAsync(Arg.Any<IdempotencyRecord>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Un command con datos inválidos retorna fallo con el error de validación.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ValidationFailed_ReturnsFailureWithValidationError()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IOrderNumberGenerator generator = Substitute.For<IOrderNumberGenerator>();

        CreateOrderHandler handler = new(repository, unitOfWork, generator);

        // ProgramName vacío y email inválido
        CreateOrderCommand command = new(
            IdempotencyKey: Guid.NewGuid(),
            ProgramName: "",
            ProductionVersion: "v1.0.0",
            VersionScreenshotPath: null,
            RequestDate: DateTime.UtcNow,
            WorkDescription: "Work",
            RequestDetails: "Details",
            Justification: "Justification",
            RequiredAction: "Action",
            RequesterName: "Name",
            RequesterPosition: "Dev",
            RequesterDepartment: "IT",
            RequesterEmail: "not-an-email"
        );

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.ValidationFailed.Code);
        await repository.DidNotReceive().AddAsync(Arg.Any<ChangeOrderEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cuando la IdempotencyKey ya existe con el mismo payload, retorna el Id original sin duplicar.
    /// </summary>
    [Fact]
    public async Task HandleAsync_IdempotencyKeyExists_SamePayload_ReturnsExistingId()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IOrderNumberGenerator generator = Substitute.For<IOrderNumberGenerator>();

        CreateOrderHandler handler = new(repository, unitOfWork, generator);
        CreateOrderCommand command = BuildValidCommand();

        Guid existingResourceId = Guid.NewGuid();

        // Calculamos el mismo hash que usa el handler internamente
        string payloadString =
            $"{command.ProgramName}|{command.ProductionVersion}|{command.RequestDate:O}|" +
            $"{command.WorkDescription}|{command.RequestDetails}|{command.Justification}|" +
            $"{command.RequiredAction}|{command.RequesterName}|{command.RequesterPosition}|" +
            $"{command.RequesterDepartment}|{command.RequesterEmail}";

        string hash = Services.IdempotencyService.ComputeHash(payloadString);

        IdempotencyRecord existing = new()
        {
            Key = command.IdempotencyKey,
            PayloadHash = hash,
            ResourceId = existingResourceId,
            CreatedAt = DateTime.UtcNow
        };

        repository.GetIdempotencyRecordAsync(command.IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingResourceId);
        await repository.DidNotReceive().AddAsync(Arg.Any<ChangeOrderEntity>(), Arg.Any<CancellationToken>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Cuando la IdempotencyKey ya existe con un payload diferente, retorna IdempotencyKeyConflict.
    /// </summary>
    [Fact]
    public async Task HandleAsync_IdempotencyKeyExists_DifferentPayload_ReturnsIdempotencyKeyConflict()
    {
        // Arrange
        IChangeOrderRepository repository = Substitute.For<IChangeOrderRepository>();
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IOrderNumberGenerator generator = Substitute.For<IOrderNumberGenerator>();

        CreateOrderHandler handler = new(repository, unitOfWork, generator);
        CreateOrderCommand command = BuildValidCommand();

        IdempotencyRecord existing = new()
        {
            Key = command.IdempotencyKey,
            PayloadHash = "hash-completamente-diferente",
            ResourceId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };

        repository.GetIdempotencyRecordAsync(command.IdempotencyKey, Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        Result<Guid, Error> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(DomainErrors.Order.IdempotencyKeyConflict.Code);
        await repository.DidNotReceive().AddAsync(Arg.Any<ChangeOrderEntity>(), Arg.Any<CancellationToken>());
    }
}
