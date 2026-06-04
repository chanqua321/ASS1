@echo off
setlocal
cd /d "%~dp0.."
call "%~dp0_sql-config.cmd"

set "UPLOAD_DIR=Web\App_Data\uploads"
set "SQL_FILE=%~dp0sql\clear-upload-data.sql"

echo === Xoa file upload (disk) ===
if not exist "%UPLOAD_DIR%" (
  mkdir "%UPLOAD_DIR%"
  echo Thu muc uploads chua ton tai - da tao moi (trong).
) else (
  del /q "%UPLOAD_DIR%\*" 2>nul
  echo Da xoa file trong: %UPLOAD_DIR%
)
if not exist "%UPLOAD_DIR%\.gitkeep" type nul > "%UPLOAD_DIR%\.gitkeep"

if /i "%~1"=="--full" (
  echo.
  echo === Reset toan bo database ===
  call "%~dp0reset-db.cmd"
  exit /b %errorlevel%
)

where sqlcmd >nul 2>&1
if errorlevel 1 (
  echo Khong tim thay sqlcmd - dung reset-db (xoa toan DB)...
  call "%~dp0reset-db.cmd"
  exit /b %errorlevel%
)

echo.
echo === Xoa ban ghi tai lieu ^& chat trong DB ===
sqlcmd -S "%SQLSERVER%" -d "%SQLDATABASE%" -U "%SQLUSER%" -P "%SQLPASSWORD%" -C -b -i "%SQL_FILE%"
if errorlevel 1 (
  echo sqlcmd that bai - dung reset-db...
  call "%~dp0reset-db.cmd"
  exit /b %errorlevel%
)

echo Da xoa Documents, Chunks, Embeddings, Quiz va Chat (giu user/mon).
echo.
echo Done. Co the upload lai tai lieu moi.
endlocal
