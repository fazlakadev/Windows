param(
    [string]$Version = "1.1.5"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$publishRoot = Join-Path $root "releases"
$stagingDir = Join-Path $publishRoot "Fazlaka-v$Version-win-x64"

Write-Host ""
Write-Host "  Fazlaka Windows v$Version Build" -ForegroundColor Magenta
Write-Host "  =================================" -ForegroundColor Magenta
Write-Host ""

# Clean
if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Path "$stagingDir\app" -Force | Out-Null

# Step 1: dotnet build (generates Fazlaka.pri with XAML resources)
Write-Host "  [1/5] Building (generates Fazlaka.pri)..." -ForegroundColor Cyan
dotnet build "$root\src\Fazlaka.Windows\Fazlaka.Windows.csproj" `
    -c Release -r win-x64 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$buildOutput = "$root\src\Fazlaka.Windows\bin\Release\net10.0-windows10.0.26100.0\win-x64"
if (!(Test-Path "$buildOutput\Fazlaka.pri")) { throw "Fazlaka.pri not found!" }
Write-Host "    Fazlaka.pri: $([math]::Round((Get-Item "$buildOutput\Fazlaka.pri").Length/1KB))KB" -ForegroundColor Green

# Step 2: dotnet publish (self-contained runtime)
Write-Host "  [2/5] Publishing (self-contained)..." -ForegroundColor Cyan
dotnet publish "$root\src\Fazlaka.Windows\Fazlaka.Windows.csproj" `
    -c Release -r win-x64 --self-contained `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -o "$publishRoot\publish" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# Copy Fazlaka.pri from build to publish
Copy-Item "$buildOutput\Fazlaka.pri" "$publishRoot\publish" -Force
Write-Host "    Fazlaka.pri copied" -ForegroundColor Green

# Step 3: Build WPF installer (single file)
Write-Host "  [3/5] Building WPF installer..." -ForegroundColor Cyan
dotnet publish "$root\installer\Fazlaka.Installer\Fazlaka.Installer.csproj" `
    -c Release -r win-x64 --self-contained `
    -p:DebugType=None `
    -o "$publishRoot\installer" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
Write-Host "    Installer: $([math]::Round((Get-Item "$publishRoot\installer\FazlakaSetup.exe").Length/1MB, 1))MB" -ForegroundColor Green

# Copy installer to staging root
Copy-Item "$publishRoot\installer\FazlakaSetup.exe" -Destination $stagingDir -Force

# Copy app files
Get-ChildItem "$publishRoot\publish" -File | Where-Object {
    $_.Extension -notin @('.pdb', '.log')
} | Copy-Item -Destination "$stagingDir\app" -Force

# Step 4: Sign
Write-Host "  [4/5] Signing..." -ForegroundColor Cyan
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
$pfx = "$root\fazlaka-signing.pfx"
if (Test-Path $pfx) {
    & $signtool sign /f $pfx /p "fazlaka2026" /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /d "Fazlaka" "$stagingDir\app\Fazlaka.exe" 2>&1 | Out-Null
    & $signtool sign /f $pfx /p "fazlaka2026" /fd SHA256 /tr "http://timestamp.digicert.com" /td SHA256 /d "Fazlaka Installer" "$stagingDir\FazlakaSetup.exe" 2>&1 | Out-Null
    Write-Host "    Signed" -ForegroundColor Green
}

# Step 5: Zip
Write-Host "  [5/5] Creating zip..." -ForegroundColor Cyan
$zipPath = "$publishRoot\Fazlaka-v$Version-win-x64.zip"
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath -Force

$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "  Build complete!" -ForegroundColor Green
Write-Host "  Zip: $zipPath ($zipSize MB)" -ForegroundColor Green
Write-Host ""
