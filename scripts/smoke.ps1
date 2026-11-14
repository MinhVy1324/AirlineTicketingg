<#
.SYNOPSIS
    Smoke test toàn hệ thống sau khi chạy `docker compose up -d`.

.DESCRIPTION
    Kiểm tra các luồng chính trên môi trường Docker:
      1. Health check /health/database.
      2. Trang công khai: chủ, tìm chuyến, tra cứu, đăng nhập.
      3. Chặn truy cập khu quản trị khi chưa đăng nhập.
      4. Đăng nhập manager: dashboard, lịch bay, đặt chỗ, đổi/hoàn, báo cáo.
      5. Manager không vào được khu admin.
      6. Đăng nhập admin: dashboard, tài khoản, cấu hình, nhật ký; vào được khu quản lý.
      7. Đăng nhập khách hàng: vé của tôi; không vào được khu quản lý/admin.
      8. Xuất báo cáo CSV có đúng header.
      9. Tải PDF vé bằng manager, bằng chủ vé; khách ẩn danh nhận 404.
     10. Tra cứu PNR + email đúng/sai.

    Nếu không truyền -Pnr, script lấy PNR đã xác nhận từ PostgreSQL trong container `db`
    (cần Docker đang chạy). Script cần PostgreSQL để lấy PNR và email chủ vé.

.NOTES
    Giới hạn đăng nhập là 5 lần/phút/IP. Chạy smoke liên tiếp trong 1 phút có thể nhận 429;
    chờ 60 giây rồi chạy lại.

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
    [string]$AdminEmail = "admin",
    [string]$AdminPassword = "123456",
    [string]$CustomerEmail = "minhanh",
    [string]$CustomerPassword = "123456",
    [string]$Pnr,
    [string]$DbUser = "airline_app",
    [string]$DbName = "airline_ticketing"
)

$ErrorActionPreference = "Stop"
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$BaseUrl = $BaseUrl.TrimEnd("/")
$script:failures = 0
$script:passes = 0
$script:skips = 0
$pdfPath = Join-Path ([System.IO.Path]::GetTempPath()) "aeroviet-smoke-$([guid]::NewGuid().ToString('N')).pdf"

function Write-Step([string]$message) { Write-Host "`n==> $message" -ForegroundColor Cyan }
function Write-Pass([string]$message) { Write-Host "  [PASS] $message" -ForegroundColor Green; $script:passes++ }
function Write-Fail([string]$message) { Write-Host "  [FAIL] $message" -ForegroundColor Red; $script:failures++ }
function Write-Skip([string]$message) { Write-Host "  [SKIP] $message" -ForegroundColor Yellow; $script:skips++ }

function Get-HttpStatus([string]$url, $session) {
    # Dùng HttpClient với AllowAutoRedirect=false để đọc đúng 302/404 thay vì
    # để Invoke-WebRequest ném lỗi khi response bị redirect.
    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.AllowAutoRedirect = $false
    if ($session) { $handler.CookieContainer = $session.Cookies }
    $client = New-Object System.Net.Http.HttpClient($handler)
    try {
        $response = $client.GetAsync($url).GetAwaiter().GetResult()
        return [int]$response.StatusCode
    } finally {
        $client.Dispose()
    }
}

function Assert-Status([string]$url, $session, [int]$expected, [string]$label) {
    try {
        $status = Get-HttpStatus $url $session
    } catch {
        Write-Fail "$label — không gọi được $url : $($_.Exception.Message)"
        return
    }
    if ($status -eq $expected) {
        Write-Pass "$label — GET $url = $status"
    } else {
        Write-Fail "$label — GET $url = $status (mong đợi $expected)"
    }
}

function Get-AntiforgeryToken([string]$html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) {
        $match = [regex]::Match($html, 'value="([^"]+)"[^>]*name="__RequestVerificationToken"')
    }
    if (-not $match.Success) { throw "Không tìm thấy __RequestVerificationToken trong trang." }
    return $match.Groups[1].Value
}

function Invoke-Login([string]$login, [string]$secret, [string]$label) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    try {
        $loginPage = Invoke-WebRequest -Uri "$BaseUrl/dang-nhap" -WebSession $session -UseBasicParsing
        $token = Get-AntiforgeryToken $loginPage.Content
        $body = @{
            __RequestVerificationToken = $token
            email                      = $login
            password                   = $secret
            rememberMe                 = "true"
        }
        $loginResponse = Invoke-WebRequest -Uri "$BaseUrl/dang-nhap" -Method Post -Body $body `
            -WebSession $session -UseBasicParsing
        # PowerShell 5.1 không luôn trả URL cuối sau redirect; xác nhận bằng cookie phiên
        # rồi kiểm tra trang đích ở bước sau (trang quản lý sẽ redirect nếu chưa đăng nhập).
        if ($loginResponse.StatusCode -eq 200 -and $session.Cookies.Count -gt 0) {
            Write-Pass "Đăng nhập $label ($login) — có cookie phiên"
            return $session
        }
        Write-Fail "Đăng nhập $label ($login) — không tạo được phiên (status $($loginResponse.StatusCode))"
        return $null
    } catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -eq 429) {
            Write-Fail "Đăng nhập $label bị giới hạn 429 — chờ 60 giây rồi chạy lại."
        } else {
            Write-Fail "Đăng nhập $label thất bại: $($_.Exception.Message)"
        }
        return $null
    }
}

function Invoke-SmokeSql([string]$sql) {
    try {
        $output = $sql | & docker compose exec -T db psql -U $DbUser -d $DbName -t -A 2>$null
        if ($LASTEXITCODE -ne 0) { return $null }
        return ($output | ForEach-Object { "$_".Trim() } | Where-Object { $_ -ne "" })
    } catch {
        return $null
    }
}

function Resolve-PnrFromDatabase {
    $rows = Invoke-SmokeSql 'select "MaDatVe" from "DatVes" where "TrangThai" = ''DaXacNhan'' order by "NgayDat" desc limit 1;'
    return ($rows | Where-Object { $_ -match '^[A-Z0-9]{4,}$' } | Select-Object -First 1)
}

function Resolve-CustomerPnrFromDatabase {
    $rows = Invoke-SmokeSql 'select d."MaDatVe" from "DatVes" d join "KhachHangs" k on k."Id" = d."KhachHangId" where k."Email" = ''minhanh@example.com'' and d."TrangThai" = ''DaXacNhan'' limit 1;'
    return ($rows | Where-Object { $_ -match '^[A-Z0-9]{4,}$' } | Select-Object -First 1)
}

function Resolve-ContactEmailForPnr([string]$maDatVe) {
    if (-not $maDatVe) { return $null }
    $safe = $maDatVe -replace "[^A-Z0-9]", ""
    $rows = Invoke-SmokeSql "select k.""Email"" from ""DatVes"" d join ""KhachHangs"" k on k.""Id"" = d.""KhachHangId"" where d.""MaDatVe"" = '$safe' limit 1;"
    return ($rows | Where-Object { $_ -match '@' } | Select-Object -First 1)
}

# --- 1. Health --------------------------------------------------------------
Write-Step "Health check $BaseUrl/health/database"
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/health/database" -UseBasicParsing
    if ($health.status -eq "connected") {
        Write-Pass "database=$($health.database), status=$($health.status)"
    } else {
        Write-Fail "Health trả status='$($health.status)', mong đợi 'connected'."
    }
} catch {
    Write-Fail "Không gọi được /health/database: $($_.Exception.Message)"
}

# --- 2. Trang công khai -----------------------------------------------------
Write-Step "Trang công khai"
$publicSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
Assert-Status "$BaseUrl/" $publicSession 200 "Trang chủ"
Assert-Status "$BaseUrl/chuyen-bay?from=SGN&to=HAN&passengers=1&cabin=economy&trip=oneway" $publicSession 200 "Tìm chuyến SGN-HAN"
Assert-Status "$BaseUrl/tra-cuu" $publicSession 200 "Trang tra cứu"
Assert-Status "$BaseUrl/dang-nhap" $publicSession 200 "Trang đăng nhập"

$searchPage = Invoke-WebRequest -Uri "$BaseUrl/chuyen-bay?from=SGN&to=HAN&passengers=1" -WebSession $publicSession -UseBasicParsing
if ($searchPage.Content -match "SGN" -and $searchPage.Content -match "HAN") {
    Write-Pass "Trang kết quả hiển thị đúng cặp sân bay SGN → HAN"
} else {
    Write-Fail "Trang kết quả không chứa mã sân bay SGN/HAN."
}

# --- 3. Chặn truy cập khi chưa đăng nhập -----------------------------------
Write-Step "Chặn truy cập khu quản trị khi chưa đăng nhập"
Assert-Status "$BaseUrl/quan-ly" $publicSession 302 "Ẩn danh → /quan-ly"
Assert-Status "$BaseUrl/admin" $publicSession 302 "Ẩn danh → /admin"
Assert-Status "$BaseUrl/ve-cua-toi" $publicSession 302 "Ẩn danh → /ve-cua-toi"

# --- 4. Manager -------------------------------------------------------------
Write-Step "Vai trò Manager"
$managerSession = Invoke-Login $Email $Password "manager"
if ($managerSession) {
    Assert-Status "$BaseUrl/quan-ly" $managerSession 200 "Dashboard quản lý"
    Assert-Status "$BaseUrl/quan-ly/lich-bay" $managerSession 200 "Lịch bay"
    Assert-Status "$BaseUrl/quan-ly/dat-ve" $managerSession 200 "Đặt chỗ"
    Assert-Status "$BaseUrl/quan-ly/doi-hoan" $managerSession 200 "Đổi/hoàn"
    Assert-Status "$BaseUrl/quan-ly/bao-cao" $managerSession 200 "Báo cáo"
    Assert-Status "$BaseUrl/admin" $managerSession 302 "Manager bị chặn khỏi /admin"
}

# --- 5. Admin ---------------------------------------------------------------
Write-Step "Vai trò Admin"
$adminSession = Invoke-Login $AdminEmail $AdminPassword "admin"
if ($adminSession) {
    Assert-Status "$BaseUrl/admin" $adminSession 200 "Dashboard admin"
    Assert-Status "$BaseUrl/admin/tai-khoan" $adminSession 200 "Tài khoản"
    Assert-Status "$BaseUrl/admin/vai-tro" $adminSession 200 "Vai trò"
    Assert-Status "$BaseUrl/admin/danh-muc" $adminSession 200 "Danh mục"
    Assert-Status "$BaseUrl/admin/cau-hinh" $adminSession 200 "Cấu hình"
    Assert-Status "$BaseUrl/admin/nhat-ky" $adminSession 200 "Nhật ký"
    Assert-Status "$BaseUrl/quan-ly" $adminSession 200 "Admin vào được khu quản lý (StaffOnly)"
}

# --- 6. Khách hàng có tài khoản --------------------------------------------
Write-Step "Vai trò Khách hàng"
$customerSession = Invoke-Login $CustomerEmail $CustomerPassword "khách hàng"
if ($customerSession) {
    Assert-Status "$BaseUrl/ve-cua-toi" $customerSession 200 "Vé của tôi"
    Assert-Status "$BaseUrl/quan-ly" $customerSession 302 "Khách bị chặn khỏi /quan-ly"
    Assert-Status "$BaseUrl/admin" $customerSession 302 "Khách bị chặn khỏi /admin"
}

# --- 7. Xuất báo cáo CSV ----------------------------------------------------
Write-Step "Xuất báo cáo CSV"
if ($managerSession) {
    try {
        $reportPage = Invoke-WebRequest -Uri "$BaseUrl/quan-ly/bao-cao" -WebSession $managerSession -UseBasicParsing
        $token = Get-AntiforgeryToken $reportPage.Content
        $csv = Invoke-WebRequest -Uri "$BaseUrl/quan-ly/bao-cao/xuat" -Method Post `
            -Body @{ __RequestVerificationToken = $token; ky = ""; hang = ""; tuyen = "" } `
            -WebSession $managerSession -UseBasicParsing
        $contentType = "$($csv.Headers['Content-Type'])"
        if ($csv.StatusCode -eq 200 -and $contentType -match "text/csv" -and $csv.Content -match "Bao cao kinh doanh") {
            Write-Pass "POST /quan-ly/bao-cao/xuat = 200 ($contentType, $($csv.RawContentLength) byte)"
        } else {
            Write-Fail "CSV không hợp lệ: status=$($csv.StatusCode), content-type=$contentType"
        }
    } catch {
        Write-Fail "Không xuất được CSV: $($_.Exception.Message)"
    }
} else {
    Write-Skip "Bỏ qua CSV vì chưa có phiên manager."
}

# --- 8. Tải PDF vé ----------------------------------------------------------
Write-Step "Tìm mã đặt chỗ để kiểm tra PDF"
$customerPnr = Resolve-CustomerPnrFromDatabase
if (-not $Pnr) { $Pnr = Resolve-PnrFromDatabase }
if ($Pnr) {
    Write-Pass "Dùng PNR $Pnr"
    if ($customerPnr) { Write-Pass "PNR của minhanh: $customerPnr" }
} else {
    Write-Skip "Không có PNR (truyền -Pnr hoặc bật Docker/PostgreSQL để tự lấy)."
}

if ($Pnr) {
    Write-Step "Tải PDF bằng quyền manager"
    if ($managerSession) {
        try {
            Invoke-WebRequest -Uri "$BaseUrl/ve/$Pnr/pdf" -WebSession $managerSession `
                -UseBasicParsing -OutFile $pdfPath
            $bytes = [System.IO.File]::ReadAllBytes($pdfPath)
            $header = [System.Text.Encoding]::ASCII.GetString($bytes[0..4])
            if ($bytes.Length -gt 20000 -and $header -eq "%PDF-") {
                Write-Pass "GET /ve/$Pnr/pdf (manager) = %PDF ($($bytes.Length) byte)"
            } else {
                Write-Fail "PDF không hợp lệ: $($bytes.Length) byte, header '$header'"
            }
        } catch {
            Write-Fail "Không tải được PDF bằng manager: $($_.Exception.Message)"
        }
    } else {
        Write-Skip "Bỏ qua PDF manager vì chưa có phiên manager."
    }

    Write-Step "Khách ẩn danh không tải được PDF"
    $anonymousSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    Assert-Status "$BaseUrl/ve/$Pnr/pdf" $anonymousSession 404 "Ẩn danh → PDF"

    if ($customerPnr -and $customerSession) {
        Write-Step "Chủ vé tải PDF của mình"
        try {
            $customerPdf = Join-Path ([System.IO.Path]::GetTempPath()) "aeroviet-customer-$([guid]::NewGuid().ToString('N')).pdf"
            Invoke-WebRequest -Uri "$BaseUrl/ve/$customerPnr/pdf" -WebSession $customerSession `
                -UseBasicParsing -OutFile $customerPdf
            $bytes = [System.IO.File]::ReadAllBytes($customerPdf)
            $header = [System.Text.Encoding]::ASCII.GetString($bytes[0..4])
            if ($bytes.Length -gt 20000 -and $header -eq "%PDF-") {
                Write-Pass "GET /ve/$customerPnr/pdf (chủ vé) = %PDF ($($bytes.Length) byte)"
            } else {
                Write-Fail "PDF chủ vé không hợp lệ: $($bytes.Length) byte, header '$header'"
            }
            Remove-Item $customerPdf -ErrorAction SilentlyContinue
        } catch {
            Write-Fail "Chủ vé không tải được PDF: $($_.Exception.Message)"
        }
    } else {
        Write-Skip "Bỏ qua PDF chủ vé vì thiếu PNR của minhanh hoặc phiên khách hàng."
    }
}

# --- 9. Tra cứu PNR ---------------------------------------------------------
Write-Step "Tra cứu PNR + email liên hệ"
$contactEmail = Resolve-ContactEmailForPnr $Pnr
if ($Pnr -and $contactEmail) {
    try {
        $lookupPage = Invoke-WebRequest -Uri "$BaseUrl/tra-cuu" -WebSession $publicSession -UseBasicParsing
        $token = Get-AntiforgeryToken $lookupPage.Content
        $lookup = Invoke-WebRequest -Uri "$BaseUrl/tra-cuu" -Method Post `
            -Body @{ __RequestVerificationToken = $token; Pnr = $Pnr; Email = $contactEmail } `
            -WebSession $publicSession -UseBasicParsing
        if ($lookup.StatusCode -eq 200 -and $lookup.Content -match [regex]::Escape($Pnr)) {
            Write-Pass "Tra cứu đúng PNR + email ($Pnr / $contactEmail)"
        } else {
            Write-Fail "Tra cứu đúng thông tin nhưng không thấy kết quả PNR $Pnr."
        }

        $token = Get-AntiforgeryToken $lookup.Content
        $wrong = Invoke-WebRequest -Uri "$BaseUrl/tra-cuu" -Method Post `
            -Body @{ __RequestVerificationToken = $token; Pnr = $Pnr; Email = "wrong-$contactEmail" } `
            -WebSession $publicSession -UseBasicParsing
        if ($wrong.StatusCode -eq 200 -and $wrong.Content -match "Không tìm thấy đặt chỗ") {
            Write-Pass "Tra cứu sai email trả thông báo chung, không lộ dữ liệu"
        } else {
            Write-Fail "Tra cứu sai email không trả thông báo mong đợi."
        }
    } catch {
        Write-Fail "Không tra cứu được PNR: $($_.Exception.Message)"
    }
} else {
    Write-Skip "Bỏ qua tra cứu vì thiếu PNR hoặc email chủ vé."
}

Remove-Item $pdfPath -ErrorAction SilentlyContinue

# --- Tổng kết ---------------------------------------------------------------
Write-Host ""
Write-Host "Kết quả: $($script:passes) pass, $($script:failures) fail, $($script:skips) skip." -ForegroundColor Cyan

if ($script:failures -gt 0) {
    Write-Host "Smoke test thất bại: $($script:failures) kiểm tra." -ForegroundColor Red
    exit 1
}

Write-Host "Smoke test thành công." -ForegroundColor Green
exit 0
