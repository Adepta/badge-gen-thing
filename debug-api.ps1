param()

Write-Host "=== Templates ==="
$r = Invoke-WebRequest -Uri "http://localhost:7071/api/badges/templates" `
    -Headers @{"X-Api-Key"="dev-api-key-insecure"} -UseBasicParsing
Write-Host ("Status: " + $r.StatusCode + "  Body: " + $r.Content)

Write-Host ""
Write-Host "=== Render PDF (baseline check) ==="
$corrId = [guid]::NewGuid().ToString()
$body = "{`"templateName`":`"badge-pulse-a6`",`"variables`":{`"firstName`":`"Ada`"},`"format`":`"Pdf`",`"correlationId`":`"$corrId`"}"
try {
    $r2 = Invoke-WebRequest -Uri "http://localhost:7071/api/badges/render" `
        -Method POST -Body $body `
        -Headers @{"X-Api-Key"="dev-api-key-insecure";"Content-Type"="application/json"} `
        -UseBasicParsing -ErrorAction Stop
    $j = $r2.Content | ConvertFrom-Json
    Write-Host ("Success: " + $j.success + "  MimeType: " + $j.mimeType + "  Error: " + $j.error)
} catch {
    $resp = $_.Exception.Response
    if ($resp) {
        $s = $resp.GetResponseStream()
        $rd = New-Object System.IO.StreamReader($s)
        Write-Host ("PDF 400 body: " + $rd.ReadToEnd())
    } else {
        Write-Host ("Exception: " + $_)
    }
}

Write-Host ""
Write-Host "=== Render PNG ==="
$corrId2 = [guid]::NewGuid().ToString()
$body2 = "{`"templateName`":`"badge-pulse-a6`",`"variables`":{`"firstName`":`"Ada`"},`"format`":`"Png`",`"correlationId`":`"$corrId2`"}"
try {
    $r3 = Invoke-WebRequest -Uri "http://localhost:7071/api/badges/render" `
        -Method POST -Body $body2 `
        -Headers @{"X-Api-Key"="dev-api-key-insecure";"Content-Type"="application/json"} `
        -UseBasicParsing -ErrorAction Stop
    $j2 = $r3.Content | ConvertFrom-Json
    Write-Host ("Success: " + $j2.success + "  MimeType: " + $j2.mimeType + "  Error: " + $j2.error)
} catch {
    $resp = $_.Exception.Response
    if ($resp) {
        $s = $resp.GetResponseStream()
        $rd = New-Object System.IO.StreamReader($s)
        Write-Host ("PNG 400 body: " + $rd.ReadToEnd())
    } else {
        Write-Host ("Exception: " + $_)
    }
}
