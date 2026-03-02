param()
$bridgeBase = "http://localhost:5100"

Write-Host "Waiting for Bridge to be ready..."
$ready = $false
for ($i = 0; $i -lt 40; $i++) {
    try {
        $r = Invoke-WebRequest -Uri ($bridgeBase + "/health") -UseBasicParsing -ErrorAction Stop
        Write-Host ("Bridge is ready! " + $r.StatusCode)
        Write-Host $r.Content
        $ready = $true
        break
    } catch {
        Write-Host ("  attempt " + $i + " - not ready yet, waiting 3s...")
        Start-Sleep -Seconds 3
    }
}

if (-not $ready) {
    Write-Host "Bridge did not become ready in time. Aborting."
    exit 1
}

Write-Host ""
Write-Host "=== Test 1: Templates list (Bridge -> Api, fixed) ==="
try {
    $tr = Invoke-WebRequest -Uri ($bridgeBase + "/templates") -UseBasicParsing -ErrorAction Stop
    Write-Host ("Status: " + $tr.StatusCode)
    Write-Host $tr.Content
} catch {
    Write-Host ("Templates request failed: " + $_)
}

Write-Host ""
Write-Host "=== Test 2: Render PNG (Bridge -> Api -> Kafka -> Console) ==="

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
    format = "Png"
}
$body = $bodyObj | ConvertTo-Json -Depth 5
$headers = @{ "Content-Type" = "application/json" }

try {
    $resp = Invoke-WebRequest -Uri ($bridgeBase + "/render") `
        -Method POST -Body $body -Headers $headers `
        -UseBasicParsing -ErrorAction Stop

    $json = $resp.Content | ConvertFrom-Json
    Write-Host ("Success:       " + $json.success)
    Write-Host ("CorrelationId: " + $json.correlationId)
    Write-Host ("MimeType:      " + $json.mimeType)

    if ($json.success -and $json.documentBase64) {
        $bytes   = [Convert]::FromBase64String($json.documentBase64)
        $outPath = "E:\PoC\DocumentGenerator\sample-bridge-render.png"
        [IO.File]::WriteAllBytes($outPath, $bytes)
        $sz = $bytes.Length
        Write-Host ("PNG saved:     " + $outPath + " (" + $sz + " bytes)")
        Start-Process $outPath
    } else {
        Write-Host ("Error: " + $json.error)
    }
} catch {
    Write-Host ("PNG render request failed: " + $_)
}
