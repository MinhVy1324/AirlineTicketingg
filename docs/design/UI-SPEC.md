# UI Spec — AeroViet (trích xuất từ Figma)

**Nguồn:** Figma file `DOwevFwRhpk4m0eSG93SVA` ("Untitled"), page `Page 1`, 34 frame, mỗi frame rộng 1440px.
**Cách trích xuất:** Figma REST API (`/v1/files/{key}?geometry=paths`) → JSON 9.3 MB → script PowerShell duyệt cây node, xuất ra text tree + bảng màu + font. Token/JSON nằm trong `%TEMP%\opencode\figma` (không commit). Token API nằm ở `docs/design/.figma-token` (đã gitignore).

> Đây là bản đặc tả để dựng view Razor, không phải bản sao pixel-perfect của file thiết kế.
> Toàn bộ nội dung trong Figma là **dữ liệu mẫu** (có ghi chú "Dữ liệu minh họa", "Giao diện minh họa").

---

## 1. Design tokens

### 1.1 Màu

| Token | Hex | Dùng cho |
|---|---|---|
| `--av-primary` | `#1655A1` | Nút chính, link, viền focus, badge info |
| `--av-primary-soft` | `#EDF4FD` | Nền badge info, nền item đang chọn, alert info |
| `--av-navy` | `#102E50` | Nền header vé, sidebar admin, logo text, hero login |
| `--av-navy-2` | `#24496F` | Nền phụ trong sidebar |
| `--av-text` | `#172D46` | Text chính |
| `--av-text-muted` | `#64758A` | Text phụ, label, chú thích |
| `--av-text-disabled` | `#A2B2C4` | Placeholder, ghế đã bán, đường bay |
| `--av-border` | `#DFE6EE` | Viền card, input, divider |
| `--av-border-strong` | `#B1C3D6` | Viền nhấn (dùng ít) |
| `--av-surface` | `#FFFFFF` | Nền card/nav |
| `--av-page` | `#F4F7FB` | Nền trang |
| `--av-success` | `#087F83` | Trạng thái thành công, icon máy bay |
| `--av-success-soft` | `#E7F5F2` | Nền badge thành công |
| `--av-warning` | `#A56612` | Cảnh báo, chờ thanh toán |
| `--av-warning-soft` | `#FFF5E3` | Nền alert cảnh báo |
| `--av-danger` | `#BB4545` | Lỗi, hủy |
| `--av-danger-soft` | `#FCEEEE` | Nền alert lỗi |

Tần suất sử dụng (số node) cho thấy 4 màu lõi là `#64758A` (966), `#172D46` (923), `#FFFFFF` (651), `#1655A1` (535).

### 1.2 Typography

Font duy nhất: **Inter** (400 / 600 / 700). Không dùng font hệ thống khác.

| Token | Size / line-height | Weight | Dùng cho |
|---|---|---|---|
| `--av-fs-display` | 48 / 72 | 700 | Hero đăng nhập |
| `--av-fs-h1` | 30 / 45 | 700 | Tiêu đề trang khách hàng |
| `--av-fs-h2` | 29 / 1.3 | 700 | Tiêu đề trang admin/manager |
| `--av-fs-h3` | 26 / 39 | 700 | Giờ bay lớn |
| `--av-fs-h4` | 18 / 27 | 400 | Tiêu đề khối/card |
| `--av-fs-body` | 14 / 21 | 400 | Nội dung chính |
| `--av-fs-body-sm` | 13 / 20 | 400 | Nội dung phụ, ô bảng |
| `--av-fs-caption` | 12 / 18 | 400 | Chú thích, badge |
| `--av-fs-micro` | 11 / 16 | 400 | Nhãn nhỏ, footer |
| `--av-fs-button` | 14 / 21 | 600 | Nhãn nút |
| `--av-ls-brand` | -0.8px | 400 @25px | Chữ "AeroViet" |

Các size lẻ xuất hiện trong file (19/20/21/22/23/24/25/27/32/34/36) là biến thể của cùng thang; khi dựng nên gom về thang trên.

### 1.3 Bo góc

`5px` badge + ghế · `8px` input, nút, alert · `10px` logo mark · `12px` card · `20px` modal · `100px` avatar/pill.

### 1.4 Shadow

Chỉ có **một** loại trong toàn file, dùng cho modal:
`box-shadow: 0 12px 40px rgba(16, 46, 80, .20)`

### 1.5 Khoảng cách

Trang dùng padding ngang **64px**, container **1312px**. Gap phổ biến: 6, 8, 10, 12, 16, 18, 20, 24, 32.
- Card padding: 24px (khách hàng), 24px (admin).
- Khoảng cách giữa các khối dọc: 20–24px.

### 1.6 Shell

| Shell | Kích thước |
|---|---|
| Navbar khách hàng | cao 88px, nền trắng, viền dưới 1px `--av-border`, padding ngang 64px |
| Footer khách hàng | cao 66px, padding 24px 64px, chữ 12px muted |
| Sidebar admin/manager | rộng 224px, nền navy, full height |
| Toolbar nội bộ | cao 80px, nền trắng, breadcrumb 13px |
| Vùng làm việc nội bộ | rộng 1216px, padding ngang 32px |
| Cột nội dung + cột phụ | 908px + 24px gap + 380px |
| Modal xác nhận | rộng 720px, padding 28px, radius 20px, shadow ở trên |

### 1.7 Icon

Bộ icon dạng stroke 1.5–2px, hình học tương thích **Lucide**: `plane`, `globe`, `chevron-down`, `chevron-right`, `arrow-left`, `arrow-right`, `calendar-days`, `search`, `info`, `triangle-alert`, `clock-3`, `circle-check`, `refresh-cw`, `lock-keyhole`, `credit-card`, `user`, `x`.
→ Dùng inline SVG (không thêm thư viện JS).

---

## 2. Kiến trúc màn hình (34 frame)

### 2.1 Khách hàng / công khai

| # | Frame | Route đề xuất | View | Ghi chú |
|---|---|---|---|---|
| 01 | Đăng nhập | `/dang-nhap` | `Account/Login` | Layout 2 cột: hero 704px navy + form 736px |
| 02 | Trang chủ & tìm chuyến | `/` | `Home/Index` | Hero + form tìm (một chiều/khứ hồi), ưu đãi, tuyến phổ biến |
| 03 | Kết quả tìm chuyến | `/chuyen-bay` | `Flights/Index` | Filter trái + danh sách card + sắp xếp |
| 04 | Hành khách & dịch vụ | `/dat-ve/hanh-khach` | `Booking/Passengers` | Stepper 4 bước, form hành khách, liên hệ, hành lý, sơ đồ ghế |
| 05 | Kiểm tra & thanh toán | `/dat-ve/thanh-toan` | `Booking/Checkout` | Alert giữ chỗ, tóm tắt, phương thức thanh toán, điều kiện vé |
| 06 | Đặt vé thành công & vé điện tử | `/dat-ve/thanh-cong` | `Booking/Success` | Vé điện tử header navy + PNR, hướng dẫn |
| 07 | Vé của tôi | `/ve-cua-toi` | `Tickets/Index` | Tabs trạng thái, tìm kiếm, master-detail 420 + 868 |
| 19 | Không tìm thấy chuyến | `/chuyen-bay` (state rỗng) | `Flights/Index` | Cùng view, nhánh empty |
| 20 | Chuyến vừa hết chỗ | `/dat-ve/het-cho` | `Booking/Unavailable` | So sánh chuyến đã chọn & chuyến thay thế |
| 21 | Kiểm tra thông tin hành khách | `/dat-ve/hanh-khach/kiem-tra` | `Booking/Passengers` (trạng thái lỗi) | Bước xác nhận trước thanh toán |
| 22 | Thanh toán thất bại | `/dat-ve/that-bai` | `Booking/PaymentFailed` | Không thu tiền, nút thử lại |
| 23 | Hết hạn giữ chỗ | `/dat-ve/het-han` | `Booking/Expired` | Đặt chỗ hết hạn |
| 24 | Đã thanh toán, đang xuất vé | `/dat-ve/dang-xuat-ve` | `Booking/Processing` | Nhánh trung gian trước 06 |
| 25 | Chuyến bay đổi giờ | `/ve-cua-toi/chuyen-doi-gio` | `Tickets/ScheduleChanged` | Thông báo + lựa chọn |
| 26 | Chuyến bay bị hủy | `/ve-cua-toi/chuyen-huy` | `Tickets/Cancelled` | Thông báo + phương án |
| 27 | Tra cứu đặt chỗ (chưa đăng nhập) | `/tra-cuu` | `Lookup/Index` | Navbar rút gọn + nút Đăng nhập |
| 28 | Gửi yêu cầu đổi vé | `/ve-cua-toi/yeu-cau-doi` | `Requests/Change` | Chọn chuyến mới + tính phí |
| 29 | Gửi yêu cầu hoàn vé | `/ve-cua-toi/yeu-cau-hoan` | `Requests/Refund` | Tính hoàn tiền dự kiến |
| 30 | Theo dõi yêu cầu đổi/hoàn | `/ve-cua-toi/yeu-cau` | `Requests/Index` | Danh sách + chi tiết yêu cầu |

### 2.2 Admin — khu vực `/admin`

| # | Frame | Route | View |
|---|---|---|---|
| 08 | Tổng quan hệ thống | `/admin` | `Dashboard/Index` |
| 09 | Quản lý tài khoản | `/admin/tai-khoan` | `Accounts/Index` |
| 10 | Vai trò & phân quyền | `/admin/vai-tro` | `Roles/Index` |
| 11 | Danh mục hệ thống | `/admin/danh-muc` | `Catalog/Index` |
| 12 | Cấu hình nghiệp vụ | `/admin/cau-hinh` | `Configuration/Index` |
| 13 | Nhật ký hoạt động | `/admin/nhat-ky` | `Audit/Index` |
| 31 | Xác nhận khóa tài khoản | `/admin/tai-khoan` (dialog) | Partial `_LockAccountDialog` |

### 2.3 Manager — khu vực `/quan-ly`

| # | Frame | Route | View |
|---|---|---|---|
| 14 | Tổng quan vận hành | `/quan-ly` | `Dashboard/Index` |
| 15 | Lịch bay & tồn chỗ | `/quan-ly/lich-bay` | `Schedules/Index` |
| 16 | Quản lý đặt vé | `/quan-ly/dat-ve` | `Bookings/Index` |
| 17 | Xử lý đổi / hoàn vé | `/quan-ly/doi-hoan` | `ChangeRefunds/Index` |
| 18 | Báo cáo kinh doanh | `/quan-ly/bao-cao` | `Reports/Index` |
| 32 | Xác nhận hủy chuyến | `/quan-ly/lich-bay` (dialog) | `_CancelFlightDialog` |
| 33 | Xác nhận thay đổi giá | `/quan-ly/lich-bay` (dialog) | `_ChangePriceDialog` |
| 34 | Xác nhận duyệt hoàn vé | `/quan-ly/doi-hoan` (dialog) | `_ApproveRefundDialog` |

**Thực tế cần dựng:** 27 view + 4 dialog (các frame 19–27, 31–34 là state/dialog của trang đã có).

### 2.4 Bố cục lặp lại

**Khách hàng**
```
Navbar 88px
  container 1312 (padding 64)
    [Stepper 64px]           ← chỉ trong luồng đặt vé (04, 05, 06, 21–24)
    PageHeader (title 30/700 + subtitle 14 muted)
    [Alert]
    2 cột: 908 (nội dung) | 380 (tóm tắt)   ← 04, 05, 06
    hoặc: 420 (danh sách) | 868 (chi tiết)  ← 07
Footer 66px
```

**Nội bộ (admin/manager)**
```
Sidebar 224px (logo, "KHÔNG GIAN LÀM VIỆC", tên khu vực, menu 13px, box phạm vi quyền, hỗ trợ)
Vùng làm việc 1216px
  Toolbar 80px (breadcrumb 13px | avatar + tên + chức danh)
  Nội dung: title 29/700 + subtitle 14 → card KPI → bảng → panel chi tiết bên phải
```

**Modal xác nhận (31–34):** overlay tối, card 720px, tiêu đề 20/700, các dòng `label — value` 13/14px, khối alert, ô nhập lý do, footer 2 nút (Hủy outline + nút hành động primary/danger).

---

## 3. Thành phần dùng lại (component inventory)

| Component | Xuất hiện ở |
|---|---|
| `_Navbar` (brand, menu, VI·VND, avatar) | 02–07, 19–30 |
| `_NavbarCompact` (brand + nút Đăng nhập) | 27 |
| `_Footer` | 02–07, 19–30 |
| `_AdminSidebar`, `_ManagerSidebar` | 08–13, 31 / 14–18, 32–34 |
| `_Stepper` (4 bước) | 04, 05, 06, 21–24 |
| `_FlightCard` | 03, 19, 20 |
| `_FilterPanel` | 03, 19 |
| `_PassengerForm` (3 cột) | 04, 21 |
| `_SeatMap` (7 cột A B C _ D E F) | 04 |
| `_OrderSummary` / `_PriceBreakdown` | 04, 05, 06, 07 |
| `_Alert` (info / warning / danger / success) | 04–07, 19–34 |
| `_Badge` (info / success / warning / danger) | toàn bộ |
| `_TicketCard` (header navy + PNR) | 06, 07 |
| `_FlightTimeline` (giờ – sân bay – thời lượng) | 04–07, 14–18 |
| `_StatCard` (label + số 30/700 + delta) | 08, 14, 18 |
| `_DataTable` (header 12px muted, row 13px, divider) | 08–18 |
| `_Pagination` (Trước · 1 · Sau) | 09, 13, 15, 16, 17, 18 |
| `_ConfirmDialog` (720px) | 31–34 |
| `_SearchToolbar` (input + select + nút) | 03, 07, 09, 13, 15, 16 |
| `_EmptyState` | 19 |
| `_ChartBars` (biểu đồ cột tĩnh) | 08, 18 |

---

## 4. Cấu trúc thư mục Razor đề xuất

```
AirlineTicketing.Web/
  Controllers/
    HomeController.cs
    AccountController.cs
    FlightsController.cs
    BookingController.cs
    TicketsController.cs
    RequestsController.cs
    LookupController.cs
  Areas/
    Admin/
      Controllers/
        DashboardController.cs
        AccountsController.cs
        RolesController.cs
        CatalogController.cs
        ConfigurationController.cs
        AuditController.cs
      Views/
        Shared/_AdminLayout.cshtml
        Dashboard/Index.cshtml
        Accounts/Index.cshtml + _LockAccountDialog.cshtml
        Roles/Index.cshtml
        Catalog/Index.cshtml
        Configuration/Index.cshtml
        Audit/Index.cshtml
    Manager/
      Controllers/
        DashboardController.cs
        SchedulesController.cs
        BookingsController.cs
        ChangeRefundsController.cs
        ReportsController.cs
      Views/
        Shared/_ManagerLayout.cshtml
        Dashboard/Index.cshtml
        Schedules/Index.cshtml + _CancelFlightDialog.cshtml + _ChangePriceDialog.cshtml
        Bookings/Index.cshtml
        ChangeRefunds/Index.cshtml + _ApproveRefundDialog.cshtml
        Reports/Index.cshtml
  Views/
    Shared/_Layout.cshtml            (khách hàng)
    Shared/_AuthLayout.cshtml        (đăng nhập)
    Shared/_Navbar.cshtml, _NavbarCompact.cshtml, _Footer.cshtml
    Shared/Components/…              (partial ở mục 3)
    Home/Index.cshtml
    Account/Login.cshtml
    Flights/Index.cshtml
    Booking/{Passengers,Checkout,Success,Review,Unavailable,PaymentFailed,Expired,Processing}.cshtml
    Tickets/{Index,ScheduleChanged,Cancelled}.cshtml
    Requests/{Index,Change,Refund}.cshtml
    Lookup/Index.cshtml
  ViewModels/
    (mỗi trang một VM, map từ DTO)
  wwwroot/
    css/tokens.css, base.css, components.css, customer.css, admin.css
    js/booking.js (stepper, chọn ghế, tính tiền), lookup.js, dialogs.js
    img/ (logo mark, hero)
```

Ghi chú kỹ thuật:
- `Views/_ViewImports.cshtml` cần thêm `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` (đã có mặc định) và using cho ViewModels.
- Layout nội bộ dùng `Areas/.../Views/_ViewStart.cshtml` để trỏ `_AdminLayout` / `_ManagerLayout`.
- Loại bỏ Bootstrap khỏi layout (giữ file trong `wwwroot/lib` cũng được, nhưng không tham chiếu) để tránh xung đột style.

---

## 5. Khoảng trống dữ liệu (design ↔ domain)

Thiết kế có nhiều thứ **chưa có trong Domain/DTO**. Cần chốt hướng xử lý trước khi dựng view:

| Design cần | Hiện có | Đề xuất |
|---|---|---|
| Hạng vé hiển thị (Phổ thông Tiêu chuẩn / Thương gia) | `Ve.HangVe` (string) | Giữ string + bảng nhãn tĩnh |
| Hành lý (12kg xách tay, 23kg ký gửi, mua thêm) | chưa có | Thêm field vào `Ve` hoặc lưu trong `HangVe`/ghi chú; giai đoạn UI dùng hằng số |
| Sơ đồ ghế & trạng thái ghế | chỉ có `Ve.SoGhe` + `SoGheTrong` | Sinh sơ đồ từ `MayBay.SoGhe` (demo) |
| Phí dịch vụ, thuế/phí sân bay | chưa có | Hằng số cấu hình demo (`70.000 ₫`, `380.000 ₫`) |
| Giữ chỗ đến (hold expired) | `DatVe.TrangThai = HetHan` | Suy ra từ `NgayDat + 15 phút` khi demo |
| Yêu cầu đổi/hoàn (RF-/CH-) | `DoiVeDTO`, `HuyVeDTO`, `HoanTien` | Chưa có entity "YeuCau"; dùng `HoanTien` + `HuyVeDTO` hoặc bổ sung entity |
| Nhật ký hoạt động (LOG-) | chưa có | Bảng demo tĩnh giai đoạn UI |
| Phân quyền theo vai trò (Admin/Manager/Khách) | chưa có `TaiKhoan`/Identity | Giai đoạn UI dùng dữ liệu tĩnh; tích hợp sau |
| KPI, biểu đồ | `ThongKeDoanhThuDTO` (một phần) | Tính từ DTO; phần còn lại demo |

---

## 6. Lộ trình triển khai

| Phase | Nội dung | Frame |
|---|---|---|
| **P0 — Nền tảng** | `tokens.css` + `base.css` + `components.css`; `_Layout` khách hàng, `_AuthLayout`, `_AdminLayout`, `_ManagerLayout`; icon SVG; trang Đăng nhập | 01 |
| **P1 — Luồng đặt vé** | Trang chủ & tìm chuyến → kết quả → hành khách/ghế → thanh toán → thành công | 02, 03, 04, 05, 06 |
| **P2 — Vé & tra cứu** | Vé của tôi (master-detail), tra cứu PNR, các nhánh lỗi/hết chỗ/hết hạn/đổi giờ/hủy | 07, 19–27 |
| **P3 — Đổi/hoàn** | Gửi yêu cầu đổi, hoàn, theo dõi yêu cầu | 28, 29, 30 |
| **P4 — Admin** | Tổng quan, tài khoản (+dialog khóa), vai trò, danh mục, cấu hình, nhật ký | 08–13, 31 |
| **P5 — Manager** | Tổng quan, lịch bay (+2 dialog), đặt vé, đổi/hoàn (+dialog duyệt), báo cáo | 14–18, 32–34 |

Mỗi phase: dựng view + CSS tĩnh trước (dùng dữ liệu mẫu đúng như Figma), sau đó nối ViewModel → BUS → DAL.

**Trạng thái:** P0–P5 đã dựng xong bằng dữ liệu mẫu tĩnh (`Demo/DemoCatalog*.cs`); build 0 warning / 0 error; toàn bộ route trả 200, form POST trả 302.

Ghi chú triển khai (khác bản đề xuất ở mục 4):
- Dùng controller phẳng `AdminController`, `ManagerController` với route attribute (`/admin/...`, `/quan-ly/...`) + `Views/Admin`, `Views/Manager` và `_ViewStart.cshtml` riêng — không dùng Areas.
- Dialog 31–34 render ngay trong view khi có query flag:
  `/admin/tai-khoan/khoa`, `/quan-ly/lich-bay?hop-thoai=huy-chuyen|doi-gia`, `/quan-ly/doi-hoan?hop-thoai=duyet-hoan`.
- Tab dùng query: `/admin/danh-muc?tab=hang-bay|tuyen-bay|tau-bay|hang-ghe`, `/quan-ly/doi-hoan?tab=cho-duyet|da-duyet|hoan-tat`.
- Không commit token Figma: file `docs/design/.figma-token` đã nằm trong `.gitignore`.

---

## 7. Việc cần làm thêm

- [x] Export asset: ảnh hero đăng nhập và ảnh 3 điểm đến (`wwwroot/img`); logo mark dùng icon SVG `plane`.
- [x] Chốt font: tự host `Inter` (woff2) trong `wwwroot/fonts` thay vì Google Fonts (build offline).
- [x] Chốt có dùng Bootstrap hay không: bỏ Bootstrap, dùng CSS thuần (`tokens/base/components/customer/admin`).
- [x] Chốt dữ liệu demo tĩnh hay nối BUS ngay từ P1: dựng UI tĩnh trước.
- [ ] Nối ViewModel → BUS → DAL cho từng luồng; thay dữ liệu mẫu trong `DemoCatalog`.
- [ ] Đăng nhập/đăng xuất thật + phân quyền Admin/Manager.
- [ ] Kiểm thử (unit/integration) cho BUS và các route chính.

