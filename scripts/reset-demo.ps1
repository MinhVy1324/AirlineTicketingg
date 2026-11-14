<#
.SYNOPSIS
    Reset dữ liệu demo AirlineTicketing về trạng thái seed ban đầu.

.DESCRIPTION
    Xoá schema public của PostgreSQL trong container `db`, chạy lại migration rồi để
    container web tự seed dữ liệu. Không xoá volume PostgreSQL, không sửa mã nguồn và
    không đụng tới các file ngoài Docker.

    Mặc định script cũng xoá key ring Data Protection và outbox email để buổi demo
    bắt đầu sạch (mọi phiên đăng nhập cũ bị đăng xuất). Dùng -KeepSessions hoặc
    -KeepOutbox nếu muốn giữ lại.

    CẢNH BÁO: toàn bộ tài khoản, booking, thanh toán và yêu cầu phát sinh sẽ bị xoá.
    Chỉ dùng cho môi trường demo, không chạy trên dữ liệu thật.

.PARAMETER Force
    Bỏ qua bước gõ xác nhận RESET.

.PARAMETER KeepSessions
    Giữ key ring Data Protection (người dùng không bị đăng xuất).

.PARAMETER KeepOutbox
    Giữ các file email/vé đã ghi trong App_Data/outbox.

.PARAMETER BaseUrl
    Địa chỉ instance đầy đủ dùng để chờ ứng dụng sẵn sàng.

.PARAMETER DbUser
    Tài khoản PostgreSQL (mặc định theo .env).

.PARAMETER DbName
    Tên cơ sở dữ liệu (mặc định theo .env).

.EXAMPLE
    pwsh ./scripts/reset-demo.ps1

.EXAMPLE
    pwsh ./scripts/reset-demo.ps1 -Force -BaseUrl http://127.0.0.1:8080

.NOTES
    Yêu cầu: Docker Compose đang chạy (`docker compose up -d`) và image web đã build.
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$KeepSessions,
    [switch]$KeepOutbox,
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$DbUser = "airline_app",
    [string]$DbName = "airline_ticketing"
)

$ErrorActionPreference = "Continue"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$BaseUrl = $BaseUrl.TrimEnd("/")
$seedServices = @("web", "web-admin", "web-manager", "web-user")

function Write-Step([string]$message) { Write-Host "`n==> $message" -ForegroundColor Cyan }
function Write-Ok([string]$message) { Write-Host "  [OK] $message" -ForegroundColor Green }
function Write-Warn([string]$message) { Write-Host "  [WARN] $message" -ForegroundColor Yellow }

function Invoke-Sql([string]$sql) {
    # Đẩy SQL qua stdin để PowerShell 5.1 không làm mất dấu " trong tham số.
    # stderr ghi ra file tạm: psql in NOTICE (DROP ... CASCADE) ra stderr và PowerShell 5.1
    # coi đó là lỗi khi $ErrorActionPreference = Stop.
    $errFile = [System.IO.Path]::GetTempFileName()
    try {
        $output = $sql | & docker compose exec -T db psql -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -t -A 2>$errFile
        if ($LASTEXITCODE -ne 0) {
            $err = ""
            if (Test-Path $errFile) { $err = Get-Content $errFile -Raw }
            throw "psql thất bại (exit $LASTEXITCODE):`n$err$($output -join "`n")"
        }
        return $output
    } finally {
        Remove-Item $errFile -ErrorAction SilentlyContinue
    }
}

function Get-DataSummary {
    try {
        $rows = Invoke-Sql @"
select 'tai_khoan=' || count(*) from "TaiKhoans"
union all select 'dat_ve=' || count(*) from "DatVes"
union all select 'chuyen_bay=' || count(*) from "ChuyenBays";
"@
        return ($rows -join ", ")
    } catch {
        return "chưa có schema dữ liệu"
    }
}

function Get-RunningServices {
    return @(& docker compose ps --services --filter status=running 2>$null)
}

# --- 1. Kiểm tra môi trường -------------------------------------------------
Write-Step "Kiểm tra Docker Compose"
$running = Get-RunningServices
if ($running -notcontains "db") {
    throw "Container 'db' chưa chạy. Hãy chạy 'docker compose up -d' trước."
}
if (-not ($running | Where-Object { $seedServices -contains $_ })) {
    throw "Không có container web nào đang chạy. Hãy chạy 'docker compose up -d' trước."
}
Write-Ok "Đang chạy: $($running -join ', ')"

$before = Get-DataSummary
Write-Host "  Hiện tại: $before"

# --- 2. Xác nhận ------------------------------------------------------------
if (-not $Force) {
    Write-Host ""
    Write-Host "THAO TÁC NÀY SẼ XOÁ TOÀN BỘ DỮ LIỆU DEMO HIỆN CÓ." -ForegroundColor Yellow
    Write-Host "Dữ liệu seed sẽ được tạo lại từ đầu; tài khoản demo giữ nguyên mật khẩu 123456." -ForegroundColor Yellow
    $answer = Read-Host "Gõ RESET để tiếp tục"
    if ($answer -ne "RESET") {
        Write-Host "Đã huỷ, không thay đổi dữ liệu." -ForegroundColor Yellow
        exit 1
    }
}

# --- 3. Xoá schema và chạy lại migration ------------------------------------
Write-Step "Xoá schema public"
Invoke-Sql @"
drop schema if exists public cascade;
create schema public;
"@ | Out-Null
Write-Ok "Đã xoá schema public"

Write-Step "Chạy migration"
$migrateErr = [System.IO.Path]::GetTempFileName()
try {
    & docker compose run --rm migrate 2>$migrateErr | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $err = ""
        if (Test-Path $migrateErr) { $err = Get-Content $migrateErr -Raw }
        throw "Migration thất bại (exit $LASTEXITCODE):`n$err`nKiểm tra 'docker compose logs migrate' hoặc chạy 'docker compose up -d --build'."
    }
} finally {
    Remove-Item $migrateErr -ErrorAction SilentlyContinue
}
Write-Ok "Migration hoàn tất"

# --- 4. Xoá phiên cũ và outbox (tuỳ chọn) -----------------------------------
if (-not $KeepSessions) {
    Write-Step "Xoá key ring Data Protection (đăng xuất mọi phiên cũ)"
    & docker compose exec -T web sh -c "rm -f /app/App_Data/keys/*" 2>$null | Out-Null
    Write-Ok "Đã xoá key ring; key mới sẽ được tạo khi web khởi động"
}

if (-not $KeepOutbox) {
    Write-Step "Dọn outbox email/vé"
    foreach ($service in $seedServices) {
        if ($running -contains $service) {
            & docker compose exec -T $service sh -c "rm -f /app/App_Data/outbox/*" 2>$null | Out-Null
        }
    }
    Write-Ok "Đã dọn outbox của các container đang chạy"
}

# --- 5. Khởi động lại web theo thứ tự để seed một lần ----------------------
Write-Step "Khởi động lại 'web' và chờ seed dữ liệu"
& docker compose restart web 2>$null | Out-Null

$deadline = (Get-Date).AddSeconds(120)
$healthy = $false
while ((Get-Date) -lt $deadline) {
    try {
        $health = Invoke-RestMethod -Uri "$BaseUrl/health/database" -UseBasicParsing -TimeoutSec 5
        if ($health.status -eq "connected") { $healthy = $true; break }
    } catch { }
    Start-Sleep -Seconds 2
}
if (-not $healthy) {
    throw "Ứng dụng chưa sẵn sàng sau 120 giây. Xem 'docker compose logs --tail=100 web'."
}
Write-Ok "Health check: database connected"

$seeded = $false
while ((Get-Date) -lt $deadline) {
    $countText = (Invoke-Sql 'select count(*) from "TaiKhoans";' | Select-Object -Last 1)
    $accountCount = 0
    if ([int]::TryParse("$countText".Trim(), [ref]$accountCount) -and $accountCount -gt 0) {
        $seeded = $true
        break
    }
    Start-Sleep -Seconds 2
}
if (-not $seeded) {
    throw "Chưa thấy dữ liệu seed sau 120 giây. Xem 'docker compose logs --tail=100 web'."
}
Write-Ok "Đã seed $accountCount tài khoản"

$otherServices = $seedServices | Where-Object { $_ -ne "web" -and $running -contains $_ }
if ($otherServices.Count -gt 0) {
    Write-Step "Khởi động lại các instance còn lại: $($otherServices -join ', ')"
    & docker compose restart $otherServices 2>$null | Out-Null
    Write-Ok "Đã khởi động lại"
}

# --- 6. Tổng kết ------------------------------------------------------------
Write-Step "Dữ liệu demo sau reset"
$after = Invoke-Sql @"
select 'tai_khoan=' || count(*) from "TaiKhoans"
union all select 'dat_ve=' || count(*) from "DatVes"
union all select 'chuyen_bay=' || count(*) from "ChuyenBays"
union all select 'yeu_cau_doi_hoan=' || count(*) from "YeuCauDoiHoans";
"@
$after | ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "Reset demo hoàn tất." -ForegroundColor Green
Write-Host "  Khách hàng : $BaseUrl/"
Write-Host "  Manager    : $BaseUrl/quan-ly   (manager / 123456)"
Write-Host "  Admin      : $BaseUrl/admin     (admin / 123456)"
Write-Host "  Khách hàng có tài khoản: $BaseUrl/ve-cua-toi  (minhanh / 123456)"
