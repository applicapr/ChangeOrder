using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChangeOrder.Domain.Abstractions;
using ChangeOrder.Domain.Entities;

namespace ChangeOrder.Business.Services;

/// <summary>
/// Resolves the idempotency contract on <c>POST /api/v1/change-orders</c>
/// (research.md R-2). Computes a SHA-256 over the canonicalized JSON of the
/// incoming request body, looks the key up, and reports back one of three
/// outcomes via <see cref="IdempotencyOutcome"/>.
/// </summary>
public sealed class IdempotencyService
{
    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        WriteIndented = false
    };

    private readonly IChangeOrderRepository _repository;

    /// <summary>Builds an idempotency service bound to the given repository abstraction.</summary>
    public IdempotencyService(IChangeOrderRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <summary>
    /// Looks <paramref name="idempotencyKey"/> up and compares its stored hash
    /// against a freshly computed hash of <paramref name="requestPayload"/>.
    /// </summary>
    /// <param name="idempotencyKey">Client-supplied key (e.g., a UUID).</param>
    /// <param name="requestPayload">Object whose canonicalized JSON drives the hash.</param>
    /// <param name="cancellationToken">Token propagated to the repository call.</param>
    public async Task<IdempotencyOutcome> ResolveAsync(
        string idempotencyKey,
        object requestPayload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentNullException.ThrowIfNull(requestPayload);

        byte[] hash = ComputeRequestHash(requestPayload);

        IdempotencyKey? existing = await _repository
            .FindIdempotencyAsync(idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return new IdempotencyOutcome.Fresh(hash);
        }

        bool matches = CryptographicOperations.FixedTimeEquals(existing.RequestHash, hash);
        return matches
            ? new IdempotencyOutcome.Existing(existing.OrderId)
            : new IdempotencyOutcome.Conflict();
    }

    /// <summary>
    /// Canonicalizes the payload (alphabetical property ordering, no
    /// whitespace) and returns the SHA-256 digest.
    /// </summary>
    /// <remarks>
    /// Exposed as <c>internal static</c> so handlers / tests that need the
    /// same hash can reuse it without re-implementing canonicalization.
    /// </remarks>
    internal static byte[] ComputeRequestHash(object requestPayload)
    {
        string json = JsonSerializer.Serialize(requestPayload, SerializeOptions);
        JsonNode? node = JsonNode.Parse(json);
        string canonical = node is null
            ? json
            : Canonicalize(node).ToJsonString(SerializeOptions);
        return SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
    }

    private static JsonNode Canonicalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            JsonObject ordered = new();
            foreach (KeyValuePair<string, JsonNode?> property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                JsonNode? value = property.Value;
                ordered[property.Key] = value is null ? null : Canonicalize(value);
            }
            return ordered;
        }

        if (node is JsonArray array)
        {
            JsonArray clone = new();
            foreach (JsonNode? element in array)
            {
                clone.Add(element is null ? null : Canonicalize(element));
            }
            return clone;
        }

        return node.DeepClone();
    }
}
