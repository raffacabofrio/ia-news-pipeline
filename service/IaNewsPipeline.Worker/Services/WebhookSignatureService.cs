using System.Security.Cryptography;
using System.Text;

namespace IaNewsPipeline.Worker.Services;

public sealed class WebhookSignatureService(string secret)
{
    public string Compute(string timestamp, string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{rawBody}"));
        return Convert.ToHexStringLower(hash);
    }
}
