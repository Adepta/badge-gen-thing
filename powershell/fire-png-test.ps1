$body = '{"templateName":"badge-pulse-a6","variables":{"firstName":"Ada","lastName":"Lovelace","jobTitle":"Mathematician","company":"Analytical Engine Co"},"format":"Png"}'
$headers = @{ 'X-Api-Key' = 'dev-api-key-insecure' }

Write-Host "Firing PNG render via Bridge..."
try {
    $r = Invoke-WebRequest -Uri 'http://localhost:5100/render' -Method POST -Body $body -ContentType 'application/json' -Headers $headers -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
    $json = $r.Content | ConvertFrom-Json
    Write-Host "HTTP $($r.StatusCode) -- success=$($json.success) mimeType=$($json.mimeType)"
    if ($json.success -and $json.documentBase64) {
        $pngBytes = [System.Convert]::FromBase64String($json.documentBase64)
        $outPath = "E:\PoC\DocumentGenerator\test-output.png"
        [System.IO.File]::WriteAllBytes($outPath, $pngBytes)
        Write-Host "Saved $($pngBytes.Length) bytes to test-output.png"

        Add-Type -AssemblyName System.Drawing
        $img = [System.Drawing.Image]::FromFile($outPath)
        Write-Host "PNG dimensions: $($img.Width) x $($img.Height) px"
        $img.Dispose()
    } else {
        Write-Host "Render failed: $($json.error)"
    }
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
