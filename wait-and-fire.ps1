param()
$apiBase = "http://localhost:7071"
$apiKey  = "dev-api-key-insecure"

Write-Host "Waiting for Api to be ready..."
$ready = $false
for ($i = 0; $i -lt 40; $i++) {
    try {
        $r = Invoke-WebRequest -Uri ($apiBase + "/health") -UseBasicParsing -ErrorAction Stop
        Write-Host ("Api is ready! " + $r.StatusCode)
        Write-Host $r.Content
        $ready = $true
        break
    } catch {
        Write-Host ("  attempt " + $i + " - not ready yet, waiting 3s...")
        Start-Sleep -Seconds 3
    }
}

if (-not $ready) {
    Write-Host "Api did not become ready in time. Aborting."
    exit 1
}

Write-Host ""
Write-Host "Firing render request..."

$corrId = [guid]::NewGuid().ToString()

$bodyObj = [ordered]@{
    templateName  = "badge-pulse-a6"
    correlationId = $corrId
    variables     = [ordered]@{
        firstName   = "Ada"
        lastName    = "Lovelace"
        jobTitle    = "Software Engineer"
        company     = "Analytical Engine Co."
        badgeNumber = "ADA-001"
    }
    format = "Pdf"
}
$body = $bodyObj | ConvertTo-Json -Depth 5

$headers = @{
    "X-Api-Key"    = $apiKey
    "Content-Type" = "application/json"
}

try {
    $resp = Invoke-WebRequest -Uri ($apiBase + "/api/badges/render") `
        -Method POST `
        -Body $body `
        -Headers $headers `
        -UseBasicParsing `
        -ErrorAction Stop

    $json = $resp.Content | ConvertFrom-Json

    Write-Host ""
    Write-Host "=== RESULT ==="
    Write-Host ("Success:       " + $json.success)
    Write-Host ("CorrelationId: " + $json.correlationId)
    Write-Host ("MimeType:      " + $json.mimeType)
    Write-Host ("CompletedAt:   " + $json.completedAt)

    if ($json.success -and $json.documentBase64) {
        $bytes   = [Convert]::FromBase64String($json.documentBase64)
        $outPath = "E:\PoC\DocumentGenerator\sample-output.pdf"
        [IO.File]::WriteAllBytes($outPath, $bytes)
        $sz = $bytes.Length
        Write-Host ("PDF saved:     " + $outPath + " (" + $sz + " bytes)")
        Start-Process $outPath
    } else {
        Write-Host ("Error:     " + $json.error)
        Write-Host ("ErrorCode: " + $json.errorCode)
    }
} catch {
    Write-Host ("Request failed: " + $_)
}
