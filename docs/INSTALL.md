# Cài đặt AirlineTicketing

Tài liệu dành cho thành viên mới clone project và chạy được hệ thống trên máy cá nhân.

## Chọn cách chạy

| Cách | Phù hợp khi | Cần cài | Hướng dẫn |
|---|---|---|---|
| **Docker (khuyến nghị)** | Muốn chạy giống môi trường chung, không cần cài .NET/PostgreSQL | Docker Desktop, Git | [Windows](INSTALL-WINDOWS.md) · [macOS](INSTALL-MACOS.md) |
| **Local + PostgreSQL trong Docker** | Đang phát triển code, cần debug/breakpoint, hot reload | .NET 10 SDK, Docker Desktop, Git | [Windows](INSTALL-WINDOWS.md#phương-án-b-local--postgresql-trong-docker) · [macOS](INSTALL-MACOS.md#phương-án-b-local--postgresql-trong-docker) |
| **Local hoàn toàn** | Không dùng Docker, đã có PostgreSQL 16 | .NET 10 SDK, PostgreSQL 16, Git | [Windows](INSTALL-WINDOWS.md#phương-án-c-local-hoàn-toàn) · [macOS](INSTALL-MACOS.md#phương-án-c-local-hoàn-toàn) |

## Địa chỉ sau khi chạy (Docker)

| Địa chỉ | Khu vực | Mở `/` sẽ tới |
|---|---|---|
| http://127.0.0.1:8080 | Đầy đủ | Trang chủ khách hàng |
| http://127.0.0.3 | Khách hàng | Trang chủ khách hàng |
| http://127.0.0.1 | Admin | `/admin` |
| http://127.0.0.2 | Manager | `/quan-ly` |

Kiểm tra nhanh: `http://127.0.0.1:8080/health/database` phải trả `status: connected`.

## Tài khoản demo

| Vai trò | Tên đăng nhập | Mật khẩu | Vào thẳng |
|---|---|---|---|
| Admin | `admin` | `123456` | `/admin` |
| Manager | `manager` | `123456` | `/quan-ly` |
| Khách hàng | `minhanh` | `123456` | `/ve-cua-toi` |

Đổi mật khẩu/ thêm tài khoản trong trang **Admin → Tài khoản** sau khi đăng nhập. Đây là tài khoản seed cho môi trường phát triển, **không dùng cho production**.

## Biến môi trường

Sao chép `.env.example` thành `.env` rồi sửa tối thiểu `POSTGRES_PASSWORD`:

```dotenv
POSTGRES_DB=airline_ticketing
POSTGRES_USER=airline_app
POSTGRES_PASSWORD=<mật-khẩu-riêng>
POSTGRES_PORT=5432

# Container đầy đủ; ba khu vực admin/manager/khách hàng dùng cổng 80
# trên ba địa chỉ loopback 127.0.0.1 / .2 / .3 (xem .env.example).
ASPNETCORE_PORT=8080
WEB_PORT_USER=80
WEB_PORT_ADMIN=80
WEB_PORT_MANAGER=80
ASPNETCORE_ENVIRONMENT=Development
```

`.env` đã nằm trong `.gitignore`; không commit hoặc gửi mật khẩu qua chat.

Compose tự truyền `ConnectionStrings__DefaultConnection` vào container (ghép từ `POSTGRES_*` ở trên). Khi chạy `dotnet run` ngoài Docker, nạp biến này hoặc `POSTGRES_PASSWORD` trước khi chạy — `appsettings.json` không lưu mật khẩu. Chạy sau HTTPS thật thì đặt `Auth__CookieSecurePolicy=Always` để cookie chỉ gửi qua kết nối bảo mật.

## Vé điện tử, email và cấu hình hệ thống

- **Gửi email:** mặc định `Email:Enabled=false` nên hệ thống ghi email `.eml` (kèm PDF vé) vào `App_Data/outbox` trong container thay vì gửi thật. Xem hộp thư demo bằng `docker compose cp web:/app/App_Data/outbox/. ./outbox/`. Muốn gửi SMTP thật, đặt `Email__Enabled=true`, `Email__Host`, `Email__Port`, `Email__UserName`, `Email__Password` (hoặc section `Email` trong `appsettings.Development.json` local — không commit mật khẩu).
- **Tải vé:** khách vừa đặt vé có thể tải ngay tại `/ve/{PNR}/pdf`; chủ vé đã đăng nhập xem trong **Vé của tôi**; Admin/Manager tải được mọi vé đã xác nhận. Nút "Gửi lại email" giới hạn 3 lần/5 phút mỗi IP (`Auth:ResendPermitLimit`, `Auth:ResendWindowSeconds`).
- **Thời hạn giữ chỗ:** job nền quét mỗi `Booking:ExpirySweepSeconds` giây (mặc định 60, kẹp 15–3600) và giải phóng các đặt chỗ quá `booking.hold_minutes`.
- **Cấu hình nghiệp vụ:** phí dịch vụ, thuế, phí đổi/hoàn, hotline và email hỗ trợ đọc từ bảng `CauHinhHeThongs`; Admin sửa tại **Admin → Cấu hình**. Giá trị mặc định trùng seed trong `ChinhSachDatVe`.
- **Font vé PDF:** Be Vietnam Pro nằm trong `AirlineTicketing.Web/wwwroot/fonts/ticket/` (giấy phép OFL). Không xóa thư mục này nếu muốn PDF hiển thị đúng tiếng Việt.
- **Phiên đăng nhập:** khóa Data Protection lưu ở `App_Data/keys`; trong Docker, compose gắn volume `dataprotection` vào đường dẫn này nên rebuild container không đăng xuất người dùng. Đổi đường dẫn bằng `DataProtection__KeysPath`; xóa volume `dataprotection` khi muốn reset toàn bộ phiên.

## Lệnh thường dùng

```bash
docker compose up --build -d      # build + chạy toàn bộ
docker compose ps                 # xem trạng thái
docker compose logs -f web-user   # log container khách hàng
docker compose down               # dừng, giữ dữ liệu
docker compose down -v            # dừng và xoá dữ liệu PostgreSQL
docker compose up -d --build web-admin   # chỉ build/chạy lại admin
pwsh ./scripts/smoke.ps1          # health + đăng nhập manager + tải PDF
```

## Tài liệu liên quan

- [Thiết kế cơ sở dữ liệu và DTO](DATABASE-DESIGN.md)
- [Phân tích yêu cầu và use case](PHAN-TICH-YEU-CAU-VA-USE-CASE.md)
- [Quy trình làm việc nhóm](TEAM-WORKFLOW.md)
- [Thiết kế UI và route](design/UI-SPEC.md)
