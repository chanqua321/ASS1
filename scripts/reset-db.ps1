param(
  [switch]$DropFirst = $true
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modelProj = Join-Path $repoRoot "Model\\Model.csproj"
$webProj = Join-Path $repoRoot "Web\\Web.csproj"

if ($DropFirst) {
  Write-Host "Dropping database (if exists)..."
  dotnet ef database drop --force --project $modelProj --startup-project $webProj | Out-Host
}

Write-Host "Applying migrations ..."
dotnet ef database update --project $modelProj --startup-project $webProj | Out-Host

Write-Host "Cleaning data (keep admin only) ..."
& (Join-Path $repoRoot "scripts\clean-keep-admin.ps1")

Write-Host "Done. Login: admin@gmail.com / 123"
Write-Host "Neu loi login sa: chay (Admin) .\scripts\enable-sa-localdb.ps1" -ForegroundColor Yellow

