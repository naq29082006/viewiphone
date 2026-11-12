<#
.SYNOPSIS
    Downloads and extracts FFmpeg 7.0 shared native DLLs for ViewiPhone Windows Client.
#>

$ErrorActionPreference = "Stop"

$destinationDir = "$PSScriptRoot\Viewiphone\bin\Debug\net8.0-windows7.0\ffmpeg"
if (-not (Test-Path $destinationDir)) {
    New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
}

$zipUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl-shared.zip"
$tempZip = "$env:TEMP\ffmpeg_shared.zip"
$tempExtract = "$env:TEMP\ffmpeg_extract"

Write-Host "[*] Downloading FFmpeg shared binaries from $zipUrl..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $zipUrl -OutFile $tempZip

Write-Host "[*] Extracting binaries..." -ForegroundColor Cyan
if (Test-Path $tempExtract) { Remove-Item -Recurse -Force $tempExtract }
Expand-Archive -Path $tempZip -DestinationPath $tempExtract

$binFolder = Get-ChildItem -Path $tempExtract -Recurse -Directory -Filter "bin" | Select-Object -First 1
if ($binFolder) {
    Copy-Item -Path "$($binFolder.FullName)\*.dll" -Destination $destinationDir -Force
    Write-Host "[+] Successfully installed FFmpeg DLLs to: $destinationDir" -ForegroundColor Green
} else {
    Write-Host "[-] Could not find bin directory in extracted archive" -ForegroundColor Red
}

Remove-Item -Path $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item -Recurse -Path $tempExtract -Force -ErrorAction SilentlyContinue
Write-Host "[+] Done! You can now run ViewiPhone." -ForegroundColor Green
