using ChangeOrder.Data.Interceptors;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Data.Tests.Interceptors;

public sealed class AuditInterceptorTests
{
    private static ApplicationDbContext CreateContext(string database)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database)
            .AddInterceptors(new AuditInterceptor())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DomainChangeOrder NewOrder(int sequence)
    {
        OrderNumber number = OrderNumber.Create(new DateOnly(2026, 5, 12), sequence).Value!;
        ChangeOrderContent content = new(
            ProgramName: "BillingCore",
            ProductionVersion: "4.7.2",
            VersionScreenshotPath: null,
            WorkDescription: "fix",
            RequestDetails: "details",
            Justification: "justification",
            RequiredAction: "action");
        RequesterInfo requester = new("Tester", "Dev", "QA", "tester@example.com");
        return new DomainChangeOrder(number, DateTime.UtcNow, requester, content);
    }

    [Fact]
    public async Task SavingChanges_OnAdd_SetsCreatedAt()
    {
        await using ApplicationDbContext context = CreateContext(nameof(SavingChanges_OnAdd_SetsCreatedAt));
        DomainChangeOrder order = NewOrder(1);

        context.ChangeOrders.Add(order);
        await context.SaveChangesAsync();

        order.CreatedAt.Should().BeOnOrBefore(DateTime.UtcNow).And.NotBe(default);
        order.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task SavingChanges_OnModify_SetsUpdatedAt()
    {
        await using ApplicationDbContext context = CreateContext(nameof(SavingChanges_OnModify_SetsUpdatedAt));
        DomainChangeOrder order = NewOrder(2);
        context.ChangeOrders.Add(order);
        await context.SaveChangesAsync();

        ChangeOrderContent updated = new(
            ProgramName: "BillingCore",
            ProductionVersion: "4.7.3",
            VersionScreenshotPath: null,
            WorkDescription: "fix-v2",
            RequestDetails: "details-v2",
            Justification: "justification",
            RequiredAction: "action");
        RequesterInfo requester = new("Tester", "Dev", "QA", "tester@example.com");
        order.UpdateContent(requester, updated);
        await context.SaveChangesAsync();

        order.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SavingChanges_OnRemove_FlipsToSoftDelete()
    {
        await using ApplicationDbContext context = CreateContext(nameof(SavingChanges_OnRemove_FlipsToSoftDelete));
        DomainChangeOrder order = NewOrder(3);
        context.ChangeOrders.Add(order);
        await context.SaveChangesAsync();

        context.ChangeOrders.Remove(order);
        await context.SaveChangesAsync();

        order.IsDeleted.Should().BeTrue();
        order.DeletedAt.Should().NotBeNull();
    }
}
