namespace ChangeOrder.Domain.ValueObjects;

/// <summary>
/// Snapshot of the person submitting a change request, captured at creation
/// time. Persisted as four flattened columns on <c>dbo.ChangeOrders</c> via
/// EF Core <c>OwnsOne</c>.
/// </summary>
/// <param name="Name">Full name.</param>
/// <param name="Position">Job title.</param>
/// <param name="Department">Organizational department.</param>
/// <param name="Email">Contact e-mail.</param>
public sealed record RequesterInfo(
    string Name,
    string Position,
    string Department,
    string Email);
