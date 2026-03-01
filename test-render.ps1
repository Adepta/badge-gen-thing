$body = '{"templateId":"badge-pulse-a6","data":{"name":"Test User","title":"Engineer","company":"Acme"},"format":"Pdf"}'
$headers = @{ 'X-Api-Key' = 'dev-api-key-insecure' }

try {
    $r = Invoke-WebRequest -Uri 'http://localhost:7071/api/badges/render' -Method POST -Body $body -ContentType 'application/json' -Headers $headers -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
    Write-Host "HTTP $($r.StatusCode) -- bytes=$($r.RawContentLength)"
} catch {
    $resp = $_.Exception.Response
    if ($resp) {
        $stream = $resp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $text = $reader.ReadToEnd()
        Write-Host "HTTP $([int]$resp.StatusCode): $text"
    } else {
        Write-Host "Error: $($_.Exception.Message)"
    }
}
