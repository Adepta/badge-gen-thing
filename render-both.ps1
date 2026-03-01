$headers  = @{ 'X-Api-Key' = 'dev-api-key-insecure' }
$outDir   = "E:\PoC\DocumentGenerator\Generated"
$bridgeUrl = "http://localhost:5100/render"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Render-Badge {
    param(
        [string]$Label,
        [string]$Template,
        [string]$Format,
        [hashtable]$Vars
    )
    $varsJson = ($Vars.GetEnumerator() | ForEach-Object { "`"$($_.Key)`":`"$($_.Value)`"" }) -join ","
    $body = "{`"templateName`":`"$Template`",`"variables`":{$varsJson},`"format`":`"$Format`"}"
    $ext  = $Format.ToLower()
    $out  = "$outDir\$Label.$ext"

    Write-Host "Rendering $Label ($Template, $Format)..."
    try {
        $r    = Invoke-WebRequest -Uri $bridgeUrl -Method POST -Body $body -ContentType 'application/json' -Headers $headers -TimeoutSec 30 -UseBasicParsing -ErrorAction Stop
        $json = $r.Content | ConvertFrom-Json
        if ($json.success) {
            $bytes = [System.Convert]::FromBase64String($json.documentBase64)
            [System.IO.File]::WriteAllBytes($out, $bytes)
            if ($Format -eq "Png") {
                Add-Type -AssemblyName System.Drawing
                $img = [System.Drawing.Image]::FromFile($out)
                Write-Host "  -> $out  ($($bytes.Length) bytes, $($img.Width)x$($img.Height)px)"
                $img.Dispose()
            } else {
                Write-Host "  -> $out  ($($bytes.Length) bytes)"
            }
            return $out
        } else {
            Write-Host "  FAILED: $($json.error)"
            return $null
        }
    } catch {
        Write-Host "  ERROR: $($_.Exception.Message)"
        return $null
    }
}

$vars = @{
    firstName = "Ada"
    lastName  = "Lovelace"
    jobTitle  = "Mathematician"
    company   = "Analytical Engine Co"
}

$files = @()

# A6 badge (portrait, 105x148mm)
$files += Render-Badge -Label "ada-badge-pulse-a6"     -Template "badge-pulse-a6"     -Format "Png" -Vars $vars
$files += Render-Badge -Label "ada-badge-pulse-a6"     -Template "badge-pulse-a6"     -Format "Pdf" -Vars $vars

# Credit card badges (85.6x53.98mm)
$files += Render-Badge -Label "ada-badge-pulse-cc"     -Template "badge-pulse-cc"     -Format "Png" -Vars $vars
$files += Render-Badge -Label "ada-badge-pulse-cc"     -Template "badge-pulse-cc"     -Format "Pdf" -Vars $vars
$files += Render-Badge -Label "ada-badge-executive-cc" -Template "badge-executive-cc" -Format "Png" -Vars $vars
$files += Render-Badge -Label "ada-badge-executive-cc" -Template "badge-executive-cc" -Format "Pdf" -Vars $vars
$files += Render-Badge -Label "ada-badge-carbon-cc"    -Template "badge-carbon-cc"    -Format "Png" -Vars $vars
$files += Render-Badge -Label "ada-badge-carbon-cc"    -Template "badge-carbon-cc"    -Format "Pdf" -Vars $vars

Write-Host ""
Write-Host "Opening all files..."
$files | Where-Object { $_ } | ForEach-Object { Start-Process $_ }
