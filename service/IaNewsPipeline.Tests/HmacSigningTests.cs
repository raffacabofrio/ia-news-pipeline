using IaNewsPipeline.Worker.Services;

namespace IaNewsPipeline.Tests;

/// <summary>
/// AC3: HMAC signing is proven against fixed, independently-computed test vectors (see
/// _bmad-output/implementation-artifacts/1-3-unit-tests.md, "Known HMAC test vectors"), proving
/// sha256=&lt;hex HMAC-SHA256(secret, timestamp + "." + raw_body)&gt; byte-for-byte. Each vector below was
/// re-verified independently via PowerShell's own System.Security.Cryptography.HMACSHA256 before being
/// trusted here, per the story's "regenerate independently" guardrail.
/// </summary>
public sealed class HmacSigningTests
{
    [Theory]
    [InlineData(
        "test-secret",
        "1735689600",
        """{"job_id":"11111111-1111-1111-1111-111111111111","source_url":"https://example.com/article","title":"Test Title","content_html":"<p>Test content.</p>","excerpt":"Test excerpt.","meta":{"model":"gpt-test","generated_at":"2026-07-07T12:00:00Z"}}""",
        "dd05ab6bb07f6e6476127d59417f122c26f53b6c5c056af4e633161150e0af8d")]
    [InlineData(
        "another-secret-value",
        "1735689601",
        "{}",
        "556ccd0e81a8438238935d73683faf78d9e2737a19c332bc4633bdfdfe3f14a6")]
    [InlineData(
        "",
        "1735689602",
        "x",
        "ded049e65f8ff6087e43cef7459606765bbe69ff089308c0c05475a36cf0657a")]
    public void Compute_matches_known_vector_byte_for_byte(
        string secret, string timestamp, string rawBody, string expectedHex)
    {
        var service = new WebhookSignatureService(secret);

        var signature = service.Compute(timestamp, rawBody);

        Assert.Equal(expectedHex, signature);
        Assert.Equal(64, signature.Length);
        Assert.Equal(expectedHex.ToLowerInvariant(), signature);
    }

    [Fact]
    public void Compute_signs_the_exact_raw_body_bytes_not_a_reserialized_copy()
    {
        // Two JSON strings that are semantically equivalent once parsed, but byte-different as raw text
        // (whitespace, key order). The frozen contract requires signing the exact transmitted bytes, so
        // these two raw bodies must NOT produce the same signature.
        var service = new WebhookSignatureService("shared-secret");
        const string timestamp = "1735689700";
        const string compactBody = """{"job_id":"abc","title":"Hello"}""";
        const string reformattedBody = """{"title": "Hello", "job_id": "abc"}""";

        var compactSignature = service.Compute(timestamp, compactBody);
        var reformattedSignature = service.Compute(timestamp, reformattedBody);

        Assert.NotEqual(compactSignature, reformattedSignature);
    }

    [Fact]
    public void Compute_is_deterministic_for_the_same_inputs()
    {
        var service = new WebhookSignatureService("shared-secret");

        var first = service.Compute("1735689800", "{\"a\":1}");
        var second = service.Compute("1735689800", "{\"a\":1}");

        Assert.Equal(first, second);
    }
}
