<#
.SYNOPSIS
    Smoke test nhanh cho stack Docker: health, đăng nhập quản lý và tải PDF vé.

.DESCRIPTION
    Kiểm tra các luồng tối thiểu sau khi `docker compose up -d`:
      1. GET /health/database trả 200.
      2. Trang chủ công khai trả 200.
      3. Đăng nhập manager thành công và mở được /quan-ly.
      4. Tải /ve/{PNR}/pdf bằng quyền manager trả PDF hợp lệ.
      5. Khách chưa đăng nhập không tải được PDF (404).

    Nếu không truyền -Pnr, script lấy một mã đặt chỗ đã xác nhận từ PostgreSQL
    trong container `db` (cần Docker đang chạy).

.EXAMPLE
    pwsh ./scripts/smoke.ps1

.EXAMPLE
    pwsh ./scripts/smoke.ps1 -BaseUrl http://127.0.0.1:8080 -Pnr ABC123
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://127.0.0.1:8080",
    [string]$Email = "manager",
    [string]$Password = "123456",
    [string]$Pnr,
    [string]$DbUser = "airline_app",
    [string]$DbName = "airline_ticketing"
)

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$BaseUrl = $BaseUrl.TrimEnd("/")
$script:failures = 0
$pdfPath = Join-Path ([System.IO.Path]::GetTempPath()) "aeroviet-smoke-$([guid]::NewGuid().ToString('N')).pdf"

function Write-Step([string]$message) { Write-Host "`n==> $message" -ForegroundColor Cyan }
function Write-Pass([string]$message) { Write-Host "  [PASS] $message" -ForegroundColor Green }
function Write-Fail([string]$message) { Write-Host "  [FAIL] $message" -ForegroundColor Red; $script:failures++ }
function Write-Skip([string]$message) { Write-Host "  [SKIP] $message" -ForegroundColor Yellow }

function Get-HttpStatus([string]$url, $session) {
    try {
        $response = Invoke-WebRequest -Uri $url -WebSession $session -UseBasicParsing -MaximumRedirection 0
        return [int]$response.StatusCode
    } catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}

function Get-AntiforgeryToken([string]$html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) {
        $match = [regex]::Match($html, 'value="([^"]+)"[^>]*name="__RequestVerificationToken"')
    }
    if (-not $match.Success) { throw "Không tìm thấy __RequestVerificationToken trong trang đăng nhập." }
    return $match.Groups[1].Value
}

function Resolve-PnrFromDatabase {
    $sql = 'select "MaDatVe" from "DatVes" where "TrangThai" = ''DaXacNhan'' order by "NgayDat" desc limit 1;'
    try {
        # Đẩy SQL qua stdin để PowerShell 5.1 không làm mất dấu " trong tham số.
        $output = $sql | & docker compose exec -T db psql -U $DbUser -d $DbName -t -A 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return ($output |
            ForEach-Object { "$_".Trim() } |
            Where-Object { $_ -match '^[A-Z0-9]{4,}$' } |
            Select-Object -First 1)
    } catch {
        return $null
    }
}

Write-Step "Health check $BaseUrl/health/database"
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health/database" -UseBasicParsing
    Write-Pass "database=$($health.database), status=$($health.status)"
} catch {
    Write-Fail "Không gọi được /health/database: $($_.Exception.Message)"
}

Write-Step "Trang chủ công khai"
$publicSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$homeStatus = Get-HttpStatus "$BaseUrl/" $publicSession
if ($homeStatus -eq 200) { Write-Pass "GET / = 200" } else { Write-Fail "GET / = $homeStatus" }

Write-Step "Đăng nhập manager"
$managerSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
try {
    $loginPage = Invoke-WebRequest -Uri "$BaseUrl/dang-nhap" -WebSession $managerSession -UseBasicParsing
    $token = Get-AntiforgeryToken $loginPage.Content
    $body = @{
        __RequestVerificationToken = $token
        email                      = $Email
        password                   = $Password
        rememberMe                 = "true"
    }
    $loginResponse = Invoke-WebRequest -Uri "$BaseUrl/dang-nhap" -Method Post -Body $body `
        -WebSession $managerSession -UseBasicParsing
    # PowerShell 5.1 không luôn trả URL cuối sau redirect; xác nhận bằng cookie phiên
    # rồi kiểm tra /quan-ly ở bước sau (trang quản lý sẽ redirect nếu chưa đăng nhập).
    if ($loginResponse.StatusCode -eq 200 -and $managerSession.Cookies.Count -gt 0) {
        Write-Pass "POST /dang-nhap = 200, đã có cookie phiên"
    } else {
        Write-Fail "Đăng nhập không tạo được phiên (status $($loginResponse.StatusCode))."
    }
} catch {
    Write-Fail "Đăng nhập thất bại: $($_.Exception.Message)"
}

Write-Step "Mở khu quản lý"
$managerStatus = Get-HttpStatus "$BaseUrl/quan-ly" $managerSession
if ($managerStatus -eq 200) { Write-Pass "GET /quan-ly = 200" } else { Write-Fail "GET /quan-ly = $managerStatus" }

Write-Step "Tìm mã đặt chỗ để kiểm tra PDF"
if (-not $Pnr) { $Pnr = Resolve-PnrFromDatabase }
if ($Pnr) {
    Write-Pass "Dùng PNR $Pnr"
} else {
    Write-Skip "Không có PNR (truyền -Pnr hoặc bật Docker/PostgreSQL để tự lấy)."
}

if ($Pnr) {
    Write-Step "Tải PDF bằng quyền manager"
    try {
        Invoke-WebRequest -Uri "$BaseUrl/ve/$Pnr/pdf" -WebSession $managerSession `
            -UseBasicParsing -OutFile $pdfPath
        $bytes = [System.IO.File]::ReadAllBytes($pdfPath)
        $header = [System.Text.Encoding]::ASCII.GetString($bytes[0..4])
        if ($bytes.Length -gt 20000 -and $header -eq "%PDF-") {
            Write-Pass "GET /ve/$Pnr/pdf = %PDF ($($bytes.Length) byte)"
        } else {
            Write-Fail "PDF không hợp lệ: $($bytes.Length) byte, header '$header'"
        }
    } catch {
        Write-Fail "Không tải được PDF: $($_.Exception.Message)"
    }

    Write-Step "Khách chưa đăng nhập không tải được PDF"
    $anonymousSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $anonymousStatus = Get-HttpStatus "$BaseUrl/ve/$Pnr/pdf" $anonymousSession
    if ($anonymousStatus -eq 404) {
        Write-Pass "GET /ve/$Pnr/pdf (ẩn danh) = 404"
    } else {
        Write-Fail "GET /ve/$Pnr/pdf (ẩn danh) = $anonymousStatus, mong đợi 404"
    }
}

Remove-Item $pdfPath -ErrorAction SilentlyContinue

if ($script:failures -gt 0) {
    Write-Host "`nSmoke test thất bại: $($script:failures) kiểm tra." -ForegroundColor Red
    exit 1
}

Write-Host "`nSmoke test thành công." -ForegroundColor Green
exit 0
