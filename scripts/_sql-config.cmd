@echo off
rem Shared SQL settings — phải khớp Model/Configuration/SqlConnectionDefaults.cs
rem Chi enable-sa-localdb.cmd dung Windows auth (-E) de BAT LOGIN sa; app/script data dung sa/12345.
set "SQLSERVER=(localdb)\MSSQLLocalDB"
set "SQLDATABASE=Assigment1DocDb"
set "SQLUSER=sa"
set "SQLPASSWORD=12345"
set "SQLLOCALDB=MSSQLLocalDB"
