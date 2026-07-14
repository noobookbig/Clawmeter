# dev-cmd.ps1 — one-liner dev workflow for newClaw
# Usage: .\scripts\dev-cmd.ps1 [ui|service|all]
param([string]$Target = "all")

Set-Location (Split-Path $PSScriptRoot -Parent)  # newClaw/

switch ($Target.ToLower()) {
    "ui"      { dotnet run --project src/Clawmeter.UI      }
    "service" { dotnet run --project src/Clawmeter.Service  -- --console }
    default   {
        Write-Host "Building all projects..."
        dotnet build Clawmeter.sln -c Debug
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Write-Host "Run tests..."
        dotnet test tests/Clawmeter.Tests
        Write-Host "Done.  Next:  .\scripts\dev-cmd.ps1 ui   or   service"
    }
}
