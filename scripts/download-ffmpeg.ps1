param(
    [string]$OutputDir = "tools/ffmpeg"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputPath = Join-Path $repoRoot $OutputDir
$tempDir = Join-Path $env:TEMP ("inplayed-ffmpeg-" + [guid]::NewGuid().ToString("N"))

New-Item -ItemType Directory -Path $tempDir | Out-Null
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

try {
    $zipPath = Join-Path $tempDir "ffmpeg.zip"
    $url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"

    Write-Host "Downloading ffmpeg..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $url -OutFile $zipPath

    Write-Host "Extracting ffmpeg.exe..." -ForegroundColor Cyan
    Expand-Archive -Path $zipPath -DestinationPath $tempDir -Force

    $exe = Get-ChildItem -Path $tempDir -Recurse -Filter "ffmpeg.exe" |
        Where-Object { $_.FullName -like "*\bin\ffmpeg.exe" } |
        Select-Object -First 1

    if (-not $exe) {
        throw "ffmpeg.exe was not found in downloaded archive."
    }

    Copy-Item -Path $exe.FullName -Destination (Join-Path $outputPath "ffmpeg.exe") -Force
    Write-Host "Done: $outputPath\ffmpeg.exe" -ForegroundColor Green
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force
    }
}
