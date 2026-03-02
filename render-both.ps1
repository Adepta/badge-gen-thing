$headers   = @{ 'X-Api-Key' = 'dev-api-key-insecure'; 'Content-Type' = 'application/json' }
$outDir    = "E:\PoC\DocumentGenerator\Generated"
$bridgeUrl = "http://localhost:5100/render"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Render-Badge {
    param(
        [string]    $Label,
        [string]    $Template,
        [string]    $Format,
        [hashtable] $Vars,
        [hashtable] $Branding = $null
    )
    $payload = @{
        templateName = $Template
        variables    = $Vars
        format       = $Format
    }
    if ($Branding) { $payload.branding = $Branding }
    $body = $payload | ConvertTo-Json -Depth 5 -Compress

    $ext = $Format.ToLower()
    $out = "$outDir\$Label.$ext"

    Write-Host "Rendering $Label ($Template, $Format)..."
    try {
        $r    = Invoke-WebRequest -Uri $bridgeUrl -Method POST -Body $body -Headers $headers -TimeoutSec 60 -UseBasicParsing -ErrorAction Stop
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

# ── Shared variables for pulse / carbon templates ────────────────────────────
$pulseVars = @{
    firstName  = "Ada"
    lastName   = "Lovelace"
    jobTitle   = "Mathematician"
    company    = "Analytical Engine Co"
}

# ── Executive template — requires branding colours + event-specific fields ───
$execVars = @{
    firstName   = "Ada"
    lastName    = "Lovelace"
    role        = "Keynote Speaker"
    team        = "Analytical Engine Co"
    ticketType  = "VIP"
    attendeeId  = "ADA-001"
    eventDate   = "12 June 2026"
    eventVenue  = "Royal Institution, London"
}
$execBranding = @{
    companyName    = "TechConf 2026"
    primaryColour  = "#1A1A2E"       # dark navy background
    custom         = @{ accentColour = "#D4AF37" }  # gold accent
}

$files = @()

# A6 badge (portrait, 105x148mm)
$files += Render-Badge -Label "ada-badge-pulse-a6"     -Template "badge-pulse-a6"     -Format "Png" -Vars $pulseVars
$files += Render-Badge -Label "ada-badge-pulse-a6"     -Template "badge-pulse-a6"     -Format "Pdf" -Vars $pulseVars

# Credit-card badges (85.6x53.98mm)
$files += Render-Badge -Label "ada-badge-pulse-cc"     -Template "badge-pulse-cc"     -Format "Png" -Vars $pulseVars
$files += Render-Badge -Label "ada-badge-pulse-cc"     -Template "badge-pulse-cc"     -Format "Pdf" -Vars $pulseVars
$files += Render-Badge -Label "ada-badge-executive-cc" -Template "badge-executive-cc" -Format "Png" -Vars $execVars  -Branding $execBranding
$files += Render-Badge -Label "ada-badge-executive-cc" -Template "badge-executive-cc" -Format "Pdf" -Vars $execVars  -Branding $execBranding
$files += Render-Badge -Label "ada-badge-carbon-cc"    -Template "badge-carbon-cc"    -Format "Png" -Vars $pulseVars
$files += Render-Badge -Label "ada-badge-carbon-cc"    -Template "badge-carbon-cc"    -Format "Pdf" -Vars $pulseVars

Write-Host ""
Write-Host "Opening all files..."
$files | Where-Object { $_ } | ForEach-Object { Start-Process $_ }
