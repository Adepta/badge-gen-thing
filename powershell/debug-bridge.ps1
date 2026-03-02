param()

# Direct Api call to confirm it works
Write-Host "=== Direct Api: templates ==="
$r1 = Invoke-WebRequest -Uri "http://localhost:7071/api/badges/templates" -Headers @{"X-Api-Key"="dev-api-key-insecure"} -UseBasicParsing
Write-Host $r1.Content

Write-Host ""
Write-Host "=== Direct Api: render PNG ==="
$body = '{"templateName":"badge-pulse-a6","variables":{"firstName":"Ada"},"format":"Png","correlationId":"' + [guid]::NewGuid().ToString() + '"}'
$r2 = Invoke-WebRequest -Uri "http://localhost:7071/api/badges/render" -Method POST -Body $body -Headers @{"X-Api-Key"="dev-api-key-insecure";"Content-Type"="application/json"} -UseBasicParsing
$json = $r2.Content | ConvertFrom-Json
Write-Host ("Success: " + $json.success + "  MimeType: " + $json.mimeType)
if ($json.documentBase64) {
    $bytes = [Convert]::FromBase64String($json.documentBase64)
    $out = "E:\PoC\DocumentGenerator\sample-direct-api.png"
    [IO.File]::WriteAllBytes($out, $bytes)
    Write-Host ("PNG saved: " + $out + " (" + $bytes.Length + " bytes)")
    Start-Process $out
}

Write-Host ""
Write-Host "=== Bridge: templates (to see actual response) ==="
try {
    $r3 = Invoke-WebRequest -Uri "http://localhost:5100/templates" -UseBasicParsing -ErrorAction Stop
    Write-Host $r3.Content
} catch {
    $resp = $_.Exception.Response
    $code = [int]$resp.StatusCode
    $stream = $resp.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($stream)
    Write-Host ("HTTP " + $code + ": " + $reader.ReadToEnd())
}

Write-Host ""
Write-Host "=== Bridge: render PNG ==="
$body2 = '{"templateName":"badge-pulse-a6","variables":{"firstName":"Ada","lastName":"Lovelace"},"format":"Png","correlationId":"' + [guid]::NewGuid().ToString() + '"}'
try {
    $r4 = Invoke-WebRequest -Uri "http://localhost:5100/render" -Method POST -Body $body2 -Headers @{"Content-Type"="application/json"} -UseBasicParsing -ErrorAction Stop
    $json2 = $r4.Content | ConvertFrom-Json
    Write-Host ("Success: " + $json2.success + "  MimeType: " + $json2.mimeType + "  Error: " + $json2.error)
} catch {
    Write-Host ("Failed: " + $_)
}
