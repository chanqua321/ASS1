# Bật login sa + mật khẩu trên LocalDB (chạy một lần).
#   cd scripts
#   .\enable-sa-localdb.ps1
# Nếu SSMS vẫn báo 18456: chạy lại PowerShell **Run as Administrator** (để bật Mixed Mode trong registry).

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\sql-sa-config.ps1"

function Find-LocalDbLoginModeRegistry {
    $paths = @()
    $root = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server"
    if (-not (Test-Path $root)) { return $paths }
    Get-ChildItem $root -ErrorAction SilentlyContinue | ForEach-Object {
        $serverKey = Join-Path $_.PSPath "MSSQLServer"
        if (-not (Test-Path $serverKey)) { return }
        $name = $_.PSChildName
        if ($name -match "LOCALDB|MSSQLLOCALDB" -or $name -match "\.$([regex]::Escape($SqlLocalDbInstance))$") {
            $paths += $serverKey
        }
    }
    $paths | Select-Object -Unique
}

function Test-SaLogin {
    sqlcmd -S $SqlSaServer -U $SqlSaUser -P $SqlSaPassword -C -b -Q "SELECT 1" 2>$null
    return $LASTEXITCODE -eq 0
}

Write-Host "=== Enable sa ($SqlSaServer) ===" -ForegroundColor Cyan

if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
    throw "Khong tim thay sqllocaldb."
}
if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw "Khong tim thay sqlcmd."
}

& sqllocaldb start $SqlLocalDbInstance 2>&1 | Out-Null

if (Test-SaLogin) {
    Write-Host "sa da dang nhap duoc - khong can cau hinh them." -ForegroundColor Green
    exit 0
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if ($isAdmin) {
    $regPaths = Find-LocalDbLoginModeRegistry
    foreach ($p in $regPaths) {
        $current = (Get-ItemProperty -Path $p -Name LoginMode -ErrorAction SilentlyContinue).LoginMode
        if ($current -ne 2) {
            Write-Host "Set LoginMode=2 at $p"
            Set-ItemProperty -Path $p -Name LoginMode -Value 2 -Type DWord
        }
    }
    if ($regPaths.Count -gt 0) {
        & sqllocaldb stop $SqlLocalDbInstance 2>&1 | Out-Null
        Start-Sleep -Seconds 2
        & sqllocaldb start $SqlLocalDbInstance 2>&1 | Out-Null
        Start-Sleep -Seconds 2
    }
} else {
    Write-Warning "Chua chay Admin - bo qua Mixed Mode registry. Neu van loi 18456, chay lai script bang Run as Administrator."
}

$pwdSql = $SqlSaPassword.Replace("'", "''")

Write-Host "Cau hinh sa (Windows auth) ..."
sqlcmd -S $SqlSaServer -E -C -b -Q "IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'sa') ALTER LOGIN [sa] ENABLE;"
sqlcmd -S $SqlSaServer -E -C -b -Q "IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'sa') ALTER LOGIN [sa] WITH PASSWORD = N'$pwdSql', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;"
sqlcmd -S $SqlSaServer -E -C -b -Q "IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'sa') CREATE LOGIN [sa] WITH PASSWORD = N'$pwdSql', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;"
sqlcmd -S $SqlSaServer -E -C -b -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_role_members rm JOIN sys.server_principals r ON rm.role_principal_id = r.principal_id JOIN sys.server_principals m ON rm.member_principal_id = m.principal_id WHERE r.name = N'sysadmin' AND m.name = N'sa') ALTER SERVER ROLE [sysadmin] ADD MEMBER [sa];"

if (-not (Test-SaLogin)) {
    throw "Van khong dang nhap duoc bang sa. Chay script nay bang PowerShell (Run as Administrator) roi thu lai."
}

Write-Host ""
Write-Host "OK - SSMS / app:" -ForegroundColor Green
Write-Host "  Server: $SqlSaServer | User: $SqlSaUser | Pass: $SqlSaPassword | DB: $SqlSaDatabase"
