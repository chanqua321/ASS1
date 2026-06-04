$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$modelProj = Join-Path $repoRoot "Model\\Model.csproj"
$webProj = Join-Path $repoRoot "Web\\Web.csproj"

Write-Host "Updating database via SQL Server migrations..."
dotnet ef database update --project $modelProj --startup-project $webProj | Out-Host

