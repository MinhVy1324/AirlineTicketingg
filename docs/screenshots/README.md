# Ảnh chụp màn hình

Bộ ảnh dùng cho README, báo cáo và slide bảo vệ. Tất cả chụp ở độ rộng **1440 px**, dữ liệu seed
sau `scripts/reset-demo.ps1` (12 tài khoản, 36 đặt chỗ, hơn 4.500 chuyến từ 24/10/2026 đến 31/12/2026),
giao diện tiếng Việt.

| Tệp | Màn hình | Route | Ghi chú |
| --- | --- | --- | --- |
| `home.png` | Trang chủ khách hàng | `/` | Form tìm chuyến, khuyến mãi, điểm đến gợi ý |
| `flights.png` | Kết quả tìm chuyến | `/chuyen-bay?from=SGN&to=HAN&depart=20/11/2026&passengers=1&cabin=economy&trip=oneway` | Dải giá 7 ngày, bộ lọc, sắp xếp |
| `seat-map.png` | Sơ đồ ghế | `/chuyen-bay/{flightId}/so-do-ghe` | Ghế trống/đã bán/đang giữ |
| `lookup.png` | Tra cứu đặt chỗ | `/tra-cuu` | Form PNR + email liên hệ |
| `login.png` | Đăng nhập | `/dang-nhap` | Dùng chung cho ba vai trò |
| `manager-dashboard.png` | Tổng quan vận hành | `/quan-ly` | KPI chuyến hôm nay, cảnh báo, việc cần xử lý |
| `manager-flights.png` | Lịch bay | `/quan-ly/lich-bay` | Lọc theo ngày, sửa giờ/giá, huỷ chuyến |
| `manager-bookings.png` | Đặt chỗ | `/quan-ly/dat-ve` | Tìm PNR, xác nhận chuyển khoản |
| `manager-change-refund.png` | Đổi/hoàn | `/quan-ly/doi-hoan` | Bảng yêu cầu, bộ lọc trạng thái |
| `manager-reports.png` | Báo cáo kinh doanh | `/quan-ly/bao-cao` | KPI doanh thu, biểu đồ, xuất CSV |
| `admin-dashboard.png` | Tổng quan hệ thống | `/admin` | KPI tài khoản/chuyến/doanh thu, cảnh báo |
| `admin-accounts.png` | Tài khoản | `/admin/tai-khoan` | Danh sách, trạng thái, khoá/mở khoá |
| `admin-settings.png` | Cấu hình nghiệp vụ | `/admin/cau-hinh` | Phí dịch vụ, thuế, phí đổi/hoàn, hotline |
| `admin-audit.png` | Nhật ký thao tác | `/admin/nhat-ky` | Lọc theo người thực hiện/loại thao tác |
| `customer-tickets.png` | Vé của tôi | `/ve-cua-toi` | Tab trạng thái, chi tiết vé, tải PDF |

## Chụp lại ảnh

Ảnh hiện tại được tạo bằng **Playwright-core** điều khiển Chrome hệ thống, không thêm dependency
vào repo. Cách làm lại:

```powershell
# 1. Dữ liệu sạch để ảnh khớp tài liệu
pwsh ./scripts/reset-demo.ps1 -Force
Start-Sleep -Seconds 20    # chờ seed + health xanh

# 2. Cài Playwright-core tạm ngoài repo
$dir = Join-Path $env:TEMP "opencode\shots"
New-Item -ItemType Directory -Force $dir | Out-Null
Set-Location $dir
npm.cmd init -y
npm.cmd install playwright-core@1 --no-audit --no-fund
```

Script chụp cần: đăng nhập lần lượt `manager`, `admin`, `minhanh`; trước mỗi lần đăng nhập gọi
`/dang-xuat` để tránh bị chuyển hướng khi đã có phiên. Ảnh dài (bảng nhiều dòng) dùng `fullPage: true`,
riêng `home`, `seat-map`, `lookup`, `manager-flights`, `manager-bookings`, `admin-audit` chụp
viewport 1440×1000 để ảnh gọn.

Mẫu tối thiểu:

```js
const { chromium } = require('playwright-core');
const browser = await chromium.launch({
  executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
  headless: true
});
const page = await (await browser.newContext({ viewport: { width: 1440, height: 1000 }, locale: 'vi-VN' })).newPage();
await page.goto('http://127.0.0.1:8080/', { waitUntil: 'networkidle' });
await page.screenshot({ path: 'home.png', fullPage: false });
```

Lưu ý:

- Đăng nhập 3 vai trò liên tiếp có thể chạm giới hạn **5 lần/phút/IP**; chờ 60 giây nếu gặp `429`.
- Tắt animation trước khi chụp (`*{animation:none!important;transition:none!important}`) để ảnh không nhoè.
- PNR trong ảnh thay đổi sau mỗi lần reset; không hardcode PNR vào tài liệu.
- Nếu giao diện đổi đáng kể, chụp lại toàn bộ bộ ảnh để README và báo cáo không lệch.
