@echo off
setlocal
call "%~dp0_sql-config.cmd"

where sqllocaldb >nul 2>&1
if errorlevel 1 (
  echo Khong tim thay sqllocaldb.
  exit /b 1
)
where sqlcmd >nul 2>&1
if errorlevel 1 (
  echo Khong tim thay sqlcmd.
  exit /b 1
)

echo === Enable sa (%SQLSERVER%) ===
sqllocaldb start %SQLLOCALDB% >nul 2>&1

sqlcmd -S "%SQLSERVER%" -U "%SQLUSER%" -P "%SQLPASSWORD%" -C -b -Q "SELECT 1" >nul 2>&1
if not errorlevel 1 (
  echo sa da dang nhap duoc - khong can cau hinh them.
  goto :ok
)

net session >nul 2>&1
if not errorlevel 1 (
  echo Dang thu bat Mixed Mode (can quyen Admin)...
  for /f "tokens=*" %%K in ('reg query "HKLM\SOFTWARE\Microsoft\Microsoft SQL Server" /s /f "LoginMode" /k 2^>nul ^| findstr /i "MSSQLServer$"') do (
    reg add "%%K" /v LoginMode /t REG_DWORD /d 2 /f >nul 2>&1
  )
  sqllocaldb stop %SQLLOCALDB% >nul 2>&1
  timeout /t 2 /nobreak >nul
  sqllocaldb start %SQLLOCALDB% >nul 2>&1
  timeout /t 2 /nobreak >nul
) else (
  echo Canh bao: chua chay Admin - bo qua Mixed Mode registry.
  echo Neu van loi 18456, chay lai CMD bang Run as Administrator.
)

echo Cau hinh sa (Windows auth)...
sqlcmd -S "%SQLSERVER%" -E -C -b -Q "IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'sa') ALTER LOGIN [sa] ENABLE;"
sqlcmd -S "%SQLSERVER%" -E -C -b -Q "IF EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'sa') ALTER LOGIN [sa] WITH PASSWORD = N'%SQLPASSWORD%', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;"
sqlcmd -S "%SQLSERVER%" -E -C -b -Q "IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'sa') CREATE LOGIN [sa] WITH PASSWORD = N'%SQLPASSWORD%', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;"
sqlcmd -S "%SQLSERVER%" -E -C -b -Q "IF NOT EXISTS (SELECT 1 FROM sys.server_role_members rm JOIN sys.server_principals r ON rm.role_principal_id = r.principal_id JOIN sys.server_principals m ON rm.member_principal_id = m.principal_id WHERE r.name = N'sysadmin' AND m.name = N'sa') ALTER SERVER ROLE [sysadmin] ADD MEMBER [sa];"

sqlcmd -S "%SQLSERVER%" -U "%SQLUSER%" -P "%SQLPASSWORD%" -C -b -Q "SELECT 1" >nul 2>&1
if errorlevel 1 (
  echo Van khong dang nhap duoc bang sa. Chay script bang CMD (Run as Administrator) roi thu lai.
  exit /b 1
)

:ok
echo.
echo OK - SSMS / app:
echo   Server: %SQLSERVER% ^| User: %SQLUSER% ^| Pass: %SQLPASSWORD% ^| DB: %SQLDATABASE%
endlocal
