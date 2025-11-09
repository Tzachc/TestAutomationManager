# PowerShell script to completely clean Visual Studio cache and rebuild
# Run this to fix IntelliSense errors

Write-Host "🧹 Cleaning Visual Studio cache and build artifacts..." -ForegroundColor Cyan

# Close Visual Studio first (optional - uncomment if you want to force close)
# Get-Process "devenv" -ErrorAction SilentlyContinue | Stop-Process -Force

# Navigate to solution directory
$solutionDir = Split-Path $PSScriptRoot
Set-Location $solutionDir

# Delete .vs folder (IntelliSense database)
if (Test-Path ".vs") {
    Write-Host "  Deleting .vs folder..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force ".vs"
}

# Delete bin and obj folders
Get-ChildItem -Path "." -Include bin,obj -Recurse -Directory | ForEach-Object {
    Write-Host "  Deleting $($_.FullName)..." -ForegroundColor Yellow
    Remove-Item $_.FullName -Recurse -Force
}

Write-Host "`n✅ Cleanup complete!" -ForegroundColor Green
Write-Host "`n📋 Next steps:" -ForegroundColor Cyan
Write-Host "  1. Open TestAutomationManager.sln in Visual Studio"
Write-Host "  2. Build → Clean Solution"
Write-Host "  3. Build → Rebuild Solution"
Write-Host "  4. All errors should be gone! ✨"
Write-Host ""
