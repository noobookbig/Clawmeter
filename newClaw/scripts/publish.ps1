# publish.ps1 — build self-contained single-file artefacts for both service + UI
# Usage: .\scripts\publish.ps1

Set-Location (Split-Path $PSScriptRoot -Parent)  # newClaw/

$rid  = "win-x64"
$cfg  = "Release"
$dist = Join-Path (Get-Location) "dist"

New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host "Publishing Clawmeter.Service (self-contained)..."
dotnet publish src/Clawmeter.Service -c $cfg -r $rid --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$dist/service"

Write-Host "Publishing Clawmeter.UI (self-contained)..."
dotnet publish src/Clawmeter.UI      -c $cfg -r $rid --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$dist/ui"

Write-Host ""
Write-Host "Done.  Artefacts:"
Get-ChildItem $dist -Recurse -Filter *.exe | ForEach-Object {
    $size = "{0:N1} MB" -f ($_.Length / 1MB)
    Write-Host "  $($_.FullName.Replace($dist, '.'))  ($size)"
}

Write-Host ""
Write-Host "Install the service with:  .\dist\service\Clawmeter.Installer.exe install"
