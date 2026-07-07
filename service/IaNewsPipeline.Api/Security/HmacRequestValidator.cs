using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Primitives;

namespace IaNewsPipeline.Api.Security;

public sealed class HmacRequestValidator(string sharedSecret)
{
    public bool IsValid(IHeaderDictionary headers, string rawBody)
    {
        if (!headers.TryGetValue("X-Pipeline-Timestamp", out var timestampValues) ||
            !headers.TryGetValue("X-Pipeline-Signature", out var signatureValues))
        {
            return false;
        }

        var timestamp = SingleHeaderValue(timestampValues);
        var providedSignature = SingleHeaderValue(signatureValues);

        if (timestamp is null ||
            providedSignature is null ||
            !providedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedHash = ComputeHexHash(timestamp, rawBody);
        var providedHash = providedSignature["sha256=".Length..];

        if (providedHash.Length != expectedHash.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(providedHash),
            Encoding.ASCII.GetBytes(expectedHash));
    }

    private string ComputeHexHash(string timestamp, string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"));
        return Convert.ToHexStringLower(hash);
    }

    private static string? SingleHeaderValue(StringValues values)
        => values.Count == 1 ? values[0] : null;
}
