@echo off
cd /d "%~dp0..\Model"
dotnet ef database update %*
exit /b %errorlevel%
