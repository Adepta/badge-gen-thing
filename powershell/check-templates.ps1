param()
try {
    $r = Invoke-WebRequest -Uri "http://localhost:5100/templates" -UseBasicParsing -ErrorAction Stop
    Write-Host $r.Content
} catch {
    $resp = $_.Exception.Response
    if ($resp) {
        $code = [int]$resp.StatusCode
        Write-Host ("HTTP " + $code)
        $stream = $resp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        Write-Host $reader.ReadToEnd()
    } else {
        Write-Host $_
    }
}
