# Làm sạch DB: chỉ giữ admin@gmail.com. Xóa môn/chương/tài liệu/chat/user khác + file upload.
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$uploadDir = Join-Path $repoRoot "Web\App_Data\uploads"

Write-Host "=== Xoa file upload ===" -ForegroundColor Cyan
if (Test-Path $uploadDir) {
    Get-ChildItem -Path $uploadDir -File -Force -ErrorAction SilentlyContinue | Remove-Item -Force
}
New-Item -ItemType Directory -Path $uploadDir -Force | Out-Null
$gitkeep = Join-Path $uploadDir ".gitkeep"
if (-not (Test-Path $gitkeep)) { New-Item -ItemType File -Path $gitkeep -Force | Out-Null }

$sql = @"
DELETE FROM MessageCitations;
DELETE FROM ChatMessages;
DELETE FROM ChatSessions;
DELETE FROM DocumentEmbeddings;
DELETE FROM DocumentChunks;
DELETE FROM Documents;
DELETE FROM SubjectEnrollments;
DELETE FROM UserLoginHistories;
UPDATE Subjects SET TeacherUserId = NULL;
DELETE FROM Chapters;
DELETE FROM Subjects;
DELETE FROM AppUsers WHERE Email <> N'admin@gmail.com';
"@

. "$PSScriptRoot\sql-sa-config.ps1"

Write-Host "=== Lam sach database (chi giu admin) ===" -ForegroundColor Cyan

if (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
    sqlcmd -S $SqlSaServer -d $SqlSaDatabase -U $SqlSaUser -P $SqlSaPassword -C -Q $sql -b
} elseif (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
    $conn = Get-SqlSaConnectionString
    Invoke-Sqlcmd -ConnectionString $conn -Query $sql
} else {
    Write-Host "Khong tim thay sqlcmd/Invoke-Sqlcmd. Chay: .\scripts\reset-db.ps1 roi apply migration RemoveDefaultSubjectSeed" -ForegroundColor Yellow
    exit 1
}

Write-Host "Done. Chi con admin@gmail.com / 123 - tao mon va teacher trong Admin UI." -ForegroundColor Green
