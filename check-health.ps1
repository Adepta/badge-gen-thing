try {
    $r = Invoke-WebRequest -Uri 'http://localhost:7071/health' -TimeoutSec 5 -UseBasicParsing
    Write-Host "Api :7071 -> HTTP $($r.StatusCode)"
} catch {
    Write-Host "Api :7071 -> NOT UP: $($_.Exception.Message)"
}
try {
    $r = Invoke-WebRequest -Uri 'http://localhost:5100/health' -TimeoutSec 5 -UseBasicParsing
    Write-Host "Bridge :5100 -> HTTP $($r.StatusCode)"
} catch {
    Write-Host "Bridge :5100 -> NOT UP: $($_.Exception.Message)"
}
