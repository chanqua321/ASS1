@echo off
setlocal
cd /d "%~dp0.."
call "%~dp0_sql-config.cmd"

set "UPLOAD_DIR=Web\App_Data\uploads"
set "SQL_FILE=%~dp0sql\clean-keep-admin.sql"

echo === Xoa file upload ===
if not exist "%UPLOAD_DIR%" mkdir "%UPLOAD_DIR%"
del /q "%UPLOAD_DIR%\*" 2>nul
if not exist "%UPLOAD_DIR%\.gitkeep" type nul > "%UPLOAD_DIR%\.gitkeep"

where sqlcmd >nul 2>&1
if errorlevel 1 (
  echo Khong tim thay sqlcmd. Cai SQL Server tools hoac chay scripts\reset-db.cmd
  exit /b 1
)

echo === Lam sach database (chi giu admin) ===
sqlcmd -S "%SQLSERVER%" -d "%SQLDATABASE%" -U "%SQLUSER%" -P "%SQLPASSWORD%" -C -b -i "%SQL_FILE%"
if errorlevel 1 (
  echo sqlcmd that bai. Thu scripts\enable-sa-localdb.cmd roi chay lai.
  exit /b 1
)

echo Done. Chi con admin@gmail.com / 123 - tao mon va teacher trong Admin UI.
endlocal
