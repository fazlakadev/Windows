param(
    [string]$Version = "1.1.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRoot = Join-Path $root "releases"
$stagingDir = Join-Path $publishRoot "Fazlaka-v$Version-win-x64"
$zipPath = Join-Path $publishRoot "Fazlaka-v$Version-win-x64.zip"

Write-Host ""
Write-Host "  Fazlaka Windows v$Version Build" -ForegroundColor Magenta
Write-Host "  =================================" -ForegroundColor Magenta
Write-Host ""

# Clean
if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# Build main app
Write-Host "  [1/4] Building Fazlaka.Windows..." -ForegroundColor Cyan
dotnet publish "$root\src\Fazlaka.Windows\Fazlaka.Windows.csproj" `
    -c Release -r win-x64 --self-contained `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -o "$publishRoot\app" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Build installer
Write-Host "  [2/4] Building FazlakaSetup..." -ForegroundColor Cyan
dotnet publish "$root\installer\Fazlaka.Installer\Fazlaka.Installer.csproj" `
    -c Release -r win-x64 --self-contained `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -o "$publishRoot\installer" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }

# Organize output
Write-Host "  [3/4] Organizing files..." -ForegroundColor Cyan

# Copy app files (exclude PDB, exclude unnecessary files)
$appDir = Join-Path $stagingDir "app"
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
Get-ChildItem "$publishRoot\app" -File | Where-Object {
    $_.Extension -notin @('.pdb', '.log')
} | Copy-Item -Destination $appDir -Force

# Copy installer to root of staging
Copy-Item "$publishRoot\installer\FazlakaSetup.exe" -Destination $stagingDir -Force

# Create zip
Write-Host "  [4/4] Creating zip..." -ForegroundColor Cyan
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath -Force

# Summary
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "  Build complete!" -ForegroundColor Green
Write-Host "  Zip: $zipPath ($zipSize MB)" -ForegroundColor Green
Write-Host ""
