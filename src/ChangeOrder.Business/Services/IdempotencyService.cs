using System.Security.Cryptography;
using System.Text;

namespace ChangeOrder.Business.Services;

/// <summary>
/// Calcula el hash SHA-256 de un payload para verificación de idempotencia.
/// </summary>
public static class IdempotencyService
{
    /// <summary>
    /// Calcula el hash SHA-256 en hexadecimal de un string.
    /// </summary>
    public static string ComputeHash(string payload)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
