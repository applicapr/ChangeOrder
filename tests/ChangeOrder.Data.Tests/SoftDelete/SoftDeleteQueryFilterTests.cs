using ChangeOrder.Data.Interceptors;
using ChangeOrder.Data.Persistence;
using ChangeOrder.Domain.Entities;
using ChangeOrder.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DomainChangeOrder = ChangeOrder.Domain.Entities.ChangeOrder;

namespace ChangeOrder.Data.Tests.SoftDelete;

/// <summary>
/// Validates FR-009: once a <see cref="DomainChangeOrder"/> has been soft-
/// deleted (via <c>Remove</c> + <see cref="AuditInterceptor"/>), the EF Core
/// global query filter masks it from every standard read. Using
/// <c>IgnoreQueryFilters()</c> brings it back, which is the audit/admin
/// surface reserved for future tooling.
/// </summary>
public sealed class SoftDeleteQueryFilterTests
{
    private static ApplicationDbContext CreateContext(string database)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(database)
            .AddInterceptors(new AuditInterceptor())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SoftDeletedRow_IsHiddenFromDefaultQueries()
    {
        await using ApplicationDbContext context = CreateContext(nameof(SoftDeletedRow_IsHiddenFromDefaultQueries));
        DomainChangeOrder order = NewOrder(1);
        context.ChangeOrders.Add(order);
        await context.SaveChangesAsync();

        context.ChangeOrders.Remove(order);
        await context.SaveChangesAsync();

        DomainChangeOrder? found = await context.ChangeOrders
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        found.Should().BeNull();
    }

    [Fact]
    public async Task SoftDeletedRow_IsVisibleWhenIgnoringFilters()
    {
        await using ApplicationDbContext context = CreateContext(nameof(SoftDeletedRow_IsVisibleWhenIgnoringFilters));
        DomainChangeOrder order = NewOrder(2);
        context.ChangeOrders.Add(order);
        await context.SaveChangesAsync();

        context.ChangeOrders.Remove(order);
        await context.SaveChangesAsync();

        DomainChangeOrder? found = await context.ChangeOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        found.Should().NotBeNull();
        found!.IsDeleted.Should().BeTrue();
        found.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ActiveRow_IsVisibleAndCountedByPagedQueries()
    {
        await using ApplicationDbContext context = CreateContext(nameof(ActiveRow_IsVisibleAndCountedByPagedQueries));
        DomainChangeOrder kept = NewOrder(3);
        DomainChangeOrder removed = NewOrder(4);
        context.ChangeOrders.Add(kept);
        context.ChangeOrders.Add(removed);
        await context.SaveChangesAsync();

        context.ChangeOrders.Remove(removed);
        await context.SaveChangesAsync();

        int totalVisible = await context.ChangeOrders.CountAsync();
        int totalIncludingDeleted = await context.ChangeOrders.IgnoreQueryFilters().CountAsync();

        totalVisible.Should().Be(1);
        totalIncludingDeleted.Should().Be(2);
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
}
