param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Secret = "replace-me",
    [string]$JobId = ("job-" + [guid]::NewGuid().ToString()),
    [string]$MySqlRootPassword = "local-root-secret",
    [string]$WordPressDatabase = "wordpress"
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

function Invoke-JsonGet {
    param(
        [string]$Name,
        [string]$Uri
    )

    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        $statusCode = & curl.exe --silent --show-error --output $tempFile --write-out "%{http_code}" $Uri
        return [pscustomobject]@{
            Name = $Name
            StatusCode = [int]$statusCode
            Body = [System.IO.File]::ReadAllText($tempFile)
        }
    } finally {
        Remove-Item $tempFile -ErrorAction SilentlyContinue
    }
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RequiredProperty {
    param(
        $Object,
        [string]$PropertyName,
        [string]$Context
    )

    $property = $Object.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        throw "$Context is missing property '$PropertyName'."
    }

    return $property.Value
}

function Get-PostMetaMap {
    param(
        [int]$PostId
    )

    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
    $query = "SELECT meta_key, meta_value FROM wp_postmeta WHERE post_id = $PostId AND meta_key IN ('_pipeline_job_id','_pipeline_source_url','_pipeline_model','_pipeline_generated_at') ORDER BY meta_key;"
    $metaRows = & docker compose --project-directory $repoRoot exec -T mysql `
        mysql -uroot "-p$MySqlRootPassword" $WordPressDatabase --batch --raw --skip-column-names -e $query

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to inspect persisted post meta for post $PostId."
    }

    $meta = @{}
    foreach ($row in $metaRows) {
        if ([string]::IsNullOrWhiteSpace($row)) {
            continue
        }

        $parts = $row -split "`t", 2
        if ($parts.Count -eq 2) {
            $meta[$parts[0]] = $parts[1]
        }
    }

    return $meta
}

function Get-PostIdsForJobId {
    param(
        [string]$LookupJobId
    )

    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
    $escapedJobId = $LookupJobId.Replace("'", "''")
    $query = "SELECT post_id FROM wp_postmeta WHERE meta_key = '_pipeline_job_id' AND meta_value = '$escapedJobId' ORDER BY post_id;"
    $postIds = & docker compose --project-directory $repoRoot exec -T mysql `
        mysql -uroot "-p$MySqlRootPassword" $WordPressDatabase --batch --raw --skip-column-names -e $query

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to inspect post ids for job $LookupJobId."
    }

    return @(@($postIds) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
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
    Invoke-JsonPost -Name "invalid-signature" -PayloadJson $validPayload -Timestamp $now -Signature $invalidSignature
    Invoke-JsonPost -Name "stale-signature" -PayloadJson $validPayload -Timestamp $stale -Signature $staleSignature
    Invoke-JsonPost -Name "invalid" -PayloadJson $invalidPayload -Timestamp $now -Signature $invalidPayloadSignature
)

$expectations = @{
    created = 201
    duplicate = 200
    "invalid-signature" = 401
    "stale-signature" = 401
    invalid = 422
}

$failures = @()
$parsedBodies = @{}
foreach ($result in $results) {
    if ($result.StatusCode -ne $expectations[$result.Name]) {
        $failures += "$($result.Name): expected $($expectations[$result.Name]), got $($result.StatusCode). Body: $($result.Body)"
        continue
    }

    try {
        $parsedBodies[$result.Name] = $result.Body | ConvertFrom-Json
    } catch {
        $failures += "$($result.Name): response body is not valid JSON. Body: $($result.Body)"
    }
}

if ($failures.Count -eq 0) {
    try {
        $createdBody = $parsedBodies["created"]
        $duplicateBody = $parsedBodies["duplicate"]
        $invalidSignatureBody = $parsedBodies["invalid-signature"]
        $staleSignatureBody = $parsedBodies["stale-signature"]
        $invalidBody = $parsedBodies["invalid"]

        $createdPostId = [int](Get-RequiredProperty -Object $createdBody -PropertyName "post_id" -Context "created response")
        $createdPostUrl = [string](Get-RequiredProperty -Object $createdBody -PropertyName "post_url" -Context "created response")
        $createdDuplicate = [bool](Get-RequiredProperty -Object $createdBody -PropertyName "duplicate" -Context "created response")
        Assert-True -Condition ($createdPostId -gt 0) -Message "created response must contain a positive post_id."
        Assert-True -Condition (-not [string]::IsNullOrWhiteSpace($createdPostUrl)) -Message "created response must contain post_url."
        Assert-True -Condition (-not $createdDuplicate) -Message "created response must set duplicate=false."

        $duplicatePostId = [int](Get-RequiredProperty -Object $duplicateBody -PropertyName "post_id" -Context "duplicate response")
        $duplicatePostUrl = [string](Get-RequiredProperty -Object $duplicateBody -PropertyName "post_url" -Context "duplicate response")
        $duplicateFlag = [bool](Get-RequiredProperty -Object $duplicateBody -PropertyName "duplicate" -Context "duplicate response")
        Assert-True -Condition ($duplicatePostId -eq $createdPostId) -Message "duplicate response must point to the originally created post."
        Assert-True -Condition ($duplicatePostUrl -eq $createdPostUrl) -Message "duplicate response must preserve post_url."
        Assert-True -Condition $duplicateFlag -Message "duplicate response must set duplicate=true."

        Assert-True -Condition ((Get-RequiredProperty -Object $invalidSignatureBody -PropertyName "code" -Context "invalid-signature response") -eq "invalid_signature") -Message "invalid-signature response must return code=invalid_signature."
        Assert-True -Condition ((Get-RequiredProperty -Object $staleSignatureBody -PropertyName "code" -Context "stale-signature response") -eq "stale_signature") -Message "stale-signature response must return code=stale_signature."
        Assert-True -Condition ((Get-RequiredProperty -Object $invalidBody -PropertyName "code" -Context "invalid response") -eq "invalid_payload") -Message "invalid response must return code=invalid_payload."
        Assert-True -Condition (-not [string]::IsNullOrWhiteSpace([string](Get-RequiredProperty -Object $invalidBody -PropertyName "reason" -Context "invalid response"))) -Message "invalid response must include a reason."

        $postResult = Invoke-JsonGet -Name "post" -Uri "$BaseUrl/wp-json/wp/v2/posts/$createdPostId"
        if ($postResult.StatusCode -ne 200) {
            throw "Unable to fetch created post $createdPostId from WordPress REST API. Status: $($postResult.StatusCode). Body: $($postResult.Body)"
        }

        $postBody = $postResult.Body | ConvertFrom-Json
        Assert-True -Condition ([string](Get-RequiredProperty -Object $postBody.title -PropertyName "rendered" -Context "post.title") -eq "Pipeline generated title") -Message "Created post title does not match payload."
        Assert-True -Condition ([string](Get-RequiredProperty -Object $postBody.content -PropertyName "rendered" -Context "post.content") -match "Generated body") -Message "Created post content does not contain the sanitized payload body."
        Assert-True -Condition ([string](Get-RequiredProperty -Object $postBody.excerpt -PropertyName "rendered" -Context "post.excerpt") -match "Generated summary") -Message "Created post excerpt does not match payload."

        $meta = Get-PostMetaMap -PostId $createdPostId
        Assert-True -Condition ($meta["_pipeline_job_id"] -eq $JobId) -Message "Persisted _pipeline_job_id does not match the payload."
        Assert-True -Condition ($meta["_pipeline_source_url"] -eq "https://example.com/original-article") -Message "Persisted _pipeline_source_url does not match the payload."
        Assert-True -Condition ($meta["_pipeline_model"] -eq "gpt-4o-mini") -Message "Persisted _pipeline_model does not match the payload."
        Assert-True -Condition ($meta["_pipeline_generated_at"] -eq "2026-07-07T15:00:00Z") -Message "Persisted _pipeline_generated_at does not match the payload."
    } catch {
        $failures += $_.Exception.Message
    }
}

$results | ConvertTo-Json -Depth 4

if ($failures.Count -gt 0) {
    throw ("Receiver verification failed:`n" + ($failures -join "`n"))
}

$concurrentJobId = "job-concurrent-$([guid]::NewGuid().ToString())"
$concurrentPayload = [ordered]@{
    job_id = $concurrentJobId
    source_url = "https://example.com/concurrent-article"
    title = "Concurrent pipeline title"
    content_html = "<p>Concurrent body</p>"
    excerpt = "Concurrent summary."
    meta = [ordered]@{
        model = "gpt-4o-mini"
        generated_at = "2026-07-07T15:05:00Z"
    }
} | ConvertTo-Json -Depth 5 -Compress

$concurrentTimestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$concurrentSignature = New-Signature -SecretValue $Secret -Timestamp ([string]$concurrentTimestamp) -RawBody $concurrentPayload
$concurrentUri = "$BaseUrl/wp-json/ia-pipeline/v1/posts"
$concurrentScript = {
    param(
        [string]$Uri,
        [string]$PayloadJson,
        [int]$Timestamp,
        [string]$Signature
    )

    $tempFile = [System.IO.Path]::GetTempFileName()
    $bodyFile = [System.IO.Path]::GetTempFileName()
    try {
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($bodyFile, $PayloadJson, $utf8NoBom)
        $statusCode = & curl.exe --silent --show-error --output $tempFile --write-out "%{http_code}" `
            -X POST $Uri `
            -H "Content-Type: application/json" `
            -H "X-Pipeline-Timestamp: $Timestamp" `
            -H "X-Pipeline-Signature: $Signature" `
            --data-binary "@$bodyFile"

        [pscustomobject]@{
            StatusCode = [int]$statusCode
            Body = [System.IO.File]::ReadAllText($tempFile)
        }
    } finally {
        Remove-Item $tempFile -ErrorAction SilentlyContinue
        Remove-Item $bodyFile -ErrorAction SilentlyContinue
    }
}

$concurrentJobs = @(
    Start-Job -ScriptBlock $concurrentScript -ArgumentList $concurrentUri, $concurrentPayload, $concurrentTimestamp, $concurrentSignature
    Start-Job -ScriptBlock $concurrentScript -ArgumentList $concurrentUri, $concurrentPayload, $concurrentTimestamp, $concurrentSignature
)

try {
    $concurrentResults = $concurrentJobs | Wait-Job | Receive-Job
} finally {
    $concurrentJobs | Remove-Job -Force -ErrorAction SilentlyContinue
}

$concurrentStatuses = @($concurrentResults | ForEach-Object { [int]$_.StatusCode } | Sort-Object)
Assert-True -Condition ($concurrentStatuses.Count -eq 2) -Message "Concurrent verification did not return two responses."
Assert-True -Condition ($concurrentStatuses[0] -eq 200 -and $concurrentStatuses[1] -eq 201) -Message "Concurrent verification must produce one 201 and one 200 response. Got: $($concurrentStatuses -join ', ')"

$concurrentBodies = @($concurrentResults | ForEach-Object { $_.Body | ConvertFrom-Json })
$concurrentPostIds = @($concurrentBodies | ForEach-Object { [int](Get-RequiredProperty -Object $_ -PropertyName "post_id" -Context "concurrent response") } | Sort-Object -Unique)
Assert-True -Condition ($concurrentPostIds.Count -eq 1) -Message "Concurrent verification must resolve to exactly one persisted post id."
Assert-True -Condition ((@($concurrentBodies | Where-Object { -not [bool](Get-RequiredProperty -Object $_ -PropertyName "duplicate" -Context "concurrent response") }).Count) -eq 1) -Message "Concurrent verification must contain exactly one non-duplicate response."
Assert-True -Condition ((@($concurrentBodies | Where-Object { [bool](Get-RequiredProperty -Object $_ -PropertyName "duplicate" -Context "concurrent response") }).Count) -eq 1) -Message "Concurrent verification must contain exactly one duplicate response."

$persistedConcurrentPostIds = @(Get-PostIdsForJobId -LookupJobId $concurrentJobId)
Assert-True -Condition ($persistedConcurrentPostIds.Count -eq 1) -Message "Concurrent verification persisted $($persistedConcurrentPostIds.Count) posts for the same job id; expected exactly one."
