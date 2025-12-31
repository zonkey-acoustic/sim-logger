# SimLogger Build Script
# Builds a self-contained single-file executable for distribution

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "dist"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SimLogger Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# Clean previous build
Write-Host "[1/4] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}

# Restore packages
Write-Host "[2/4] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to restore packages" -ForegroundColor Red
    exit 1
}

# Build solution
Write-Host "[3/4] Building solution in $Configuration mode..." -ForegroundColor Yellow
dotnet build -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed" -ForegroundColor Red
    exit 1
}

# Publish single-file executable
Write-Host "[4/4] Publishing self-contained single-file executable..." -ForegroundColor Yellow
dotnet publish SimLogger.UI/SimLogger.UI.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed" -ForegroundColor Red
    exit 1
}

# Show results
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output location: $OutputDir" -ForegroundColor White

$exePath = Join-Path $OutputDir "SimLogger.exe"
if (Test-Path $exePath) {
    $fileInfo = Get-Item $exePath
    $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
    Write-Host "Executable: SimLogger.exe ($sizeMB MB)" -ForegroundColor White
}

Write-Host ""
Write-Host "The executable is self-contained and ready for distribution." -ForegroundColor Cyan
Write-Host "Users do not need .NET installed to run it." -ForegroundColor Cyan
