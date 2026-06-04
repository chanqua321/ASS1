@echo off
setlocal
cd /d "%~dp0..\Model"

set "DROP=1"
if /i "%~1"=="--no-drop" set "DROP=0"

if "%DROP%"=="1" (
  echo Dropping database (if exists)...
  dotnet ef database drop --force
  if errorlevel 1 exit /b 1
)

echo Applying migrations...
dotnet ef database update
if errorlevel 1 exit /b 1

echo Cleaning data (keep admin only)...
call "%~dp0clean-keep-admin.cmd"
if errorlevel 1 exit /b 1

echo.
echo Done. Login: admin@gmail.com / 123
echo Neu loi login sa: chay (Admin) scripts\enable-sa-localdb.cmd
endlocal
