# Xóa toàn bộ file upload trên disk + metadata tài liệu trong DB (giữ user/môn seed).
param(
    [switch]$FullDatabaseReset
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$uploadDir = Join-Path $repoRoot "Web\App_Data\uploads"
$modelProj = Join-Path $repoRoot "Model\Model.csproj"
$webProj = Join-Path $repoRoot "Web\Web.csproj"

Write-Host "=== Xóa file upload (disk) ===" -ForegroundColor Cyan
if (-not (Test-Path $uploadDir)) {
    New-Item -ItemType Directory -Path $uploadDir -Force | Out-Null
    Write-Host "Thư mục uploads chưa tồn tại — đã tạo mới (trống)."
} else {
    $files = Get-ChildItem -Path $uploadDir -File -Force -ErrorAction SilentlyContinue
    $count = @($files).Count
    foreach ($f in $files) {
        Remove-Item -LiteralPath $f.FullName -Force
    }
    Write-Host "Đã xóa $count file trong: $uploadDir"
}

$gitkeep = Join-Path $uploadDir ".gitkeep"
if (-not (Test-Path $gitkeep)) {
    New-Item -ItemType File -Path $gitkeep -Force | Out-Null
}

if ($FullDatabaseReset) {
    Write-Host "`n=== Reset toàn bộ database ===" -ForegroundColor Cyan
    & (Join-Path $repoRoot "scripts\reset-db.ps1")
    Write-Host "Hoàn tất: DB mới + không còn tài liệu/chat cũ."
    exit 0
}

Write-Host "`n=== Xóa bản ghi tài liệu & chat trong DB ===" -ForegroundColor Cyan
. "$PSScriptRoot\sql-sa-config.ps1"
$conn = Get-SqlSaConnectionString
$sql = @"
DELETE FROM MessageCitations;
DELETE FROM ChatMessages;
DELETE FROM ChatSessions;
DELETE FROM DocumentEmbeddings;
DELETE FROM DocumentChunks;
DELETE FROM Documents;
"@

if (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
    Invoke-Sqlcmd -ConnectionString $conn -Query $sql
    Write-Host "Đã xóa Documents, Chunks, Embeddings và Chat (giữ user/môn)."
} else {
    Write-Host "Invoke-Sqlcmd không có — dùng reset-db (xóa toàn DB)..." -ForegroundColor Yellow
    & (Join-Path $repoRoot "scripts\reset-db.ps1")
}

Write-Host "`nDone. Có thể upload lại tài liệu mới." -ForegroundColor Green
