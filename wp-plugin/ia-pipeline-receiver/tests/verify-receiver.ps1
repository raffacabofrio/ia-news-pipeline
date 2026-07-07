param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Secret = "replace-me",
    [string]$JobId = ("job-" + [guid]::NewGuid().ToString())
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function New-Signature {
    param(
        [string]$SecretValue,
        [string]$Timestamp,
        [string]$RawBody
    )

    $keyBytes = [System.Text.Encoding]::UTF8.GetBytes($SecretValue)
    $messageBytes = [System.Text.Encoding]::UTF8.GetBytes("$Timestamp.$RawBody")
    $hmac = [System.Security.Cryptography.HMACSHA256]::new($keyBytes)
    try {
        $hashBytes = $hmac.ComputeHash($messageBytes)
    } finally {
        $hmac.Dispose()
    }

    $hashHex = ([System.BitConverter]::ToString($hashBytes) -replace "-", "").ToLowerInvariant()
    return "sha256=$hashHex"
}

function Invoke-JsonPost {
    param(
        [string]$Name,
        [string]$PayloadJson,
        [int]$Timestamp,
        [string]$Signature
    )

    $uri = "$BaseUrl/wp-json/ia-pipeline/v1/posts"
    $tempFile = [System.IO.Path]::GetTempFileName()
    $bodyFile = [System.IO.Path]::GetTempFileName()
    try {
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($bodyFile, $PayloadJson, $utf8NoBom)
        $statusCode = & curl.exe --silent --show-error --output $tempFile --write-out "%{http_code}" `
            -X POST $uri `
            -H "Content-Type: application/json" `
            -H "X-Pipeline-Timestamp: $Timestamp" `
            -H "X-Pipeline-Signature: $Signature" `
            --data-binary "@$bodyFile"

        return [pscustomobject]@{
            Name = $Name
            StatusCode = [int]$statusCode
            Body = [System.IO.File]::ReadAllText($tempFile)
        }
    } finally {
        Remove-Item $tempFile -ErrorAction SilentlyContinue
        Remove-Item $bodyFile -ErrorAction SilentlyContinue
    }
}

$validPayload = [ordered]@{
    job_id = $JobId
    source_url = "https://example.com/original-article"
    title = "Pipeline generated title"
    content_html = "<p>Generated body</p>"
    excerpt = "Generated summary."
    meta = [ordered]@{
        model = "gpt-4o-mini"
        generated_at = "2026-07-07T15:00:00Z"
    }
} | ConvertTo-Json -Depth 5 -Compress

$invalidPayload = [ordered]@{
    job_id = $JobId
    source_url = "not-a-url"
    title = ""
    content_html = ""
    excerpt = ""
    meta = [ordered]@{
        model = ""
    }
} | ConvertTo-Json -Depth 5 -Compress

$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$stale = $now - 301

$validSignature = New-Signature -SecretValue $Secret -Timestamp ([string]$now) -RawBody $validPayload
$invalidSignature = New-Signature -SecretValue "wrong-secret" -Timestamp ([string]$now) -RawBody $validPayload
$invalidPayloadSignature = New-Signature -SecretValue $Secret -Timestamp ([string]$now) -RawBody $invalidPayload
$staleSignature = New-Signature -SecretValue $Secret -Timestamp ([string]$stale) -RawBody $validPayload

$results = @(
    Invoke-JsonPost -Name "created" -PayloadJson $validPayload -Timestamp $now -Signature $validSignature
    Invoke-JsonPost -Name "duplicate" -PayloadJson $validPayload -Timestamp $now -Signature $validSignature
    Invoke-JsonPost -Name "unauthorized" -PayloadJson $validPayload -Timestamp $stale -Signature $staleSignature
    Invoke-JsonPost -Name "invalid" -PayloadJson $invalidPayload -Timestamp $now -Signature $invalidPayloadSignature
)

$expectations = @{
    created = 201
    duplicate = 200
    unauthorized = 401
    invalid = 422
}

$failures = @()
foreach ($result in $results) {
    if ($result.StatusCode -ne $expectations[$result.Name]) {
        $failures += "$($result.Name): expected $($expectations[$result.Name]), got $($result.StatusCode). Body: $($result.Body)"
    }
}

$results | ConvertTo-Json -Depth 4

if ($failures.Count -gt 0) {
    throw ("Receiver verification failed:`n" + ($failures -join "`n"))
}
