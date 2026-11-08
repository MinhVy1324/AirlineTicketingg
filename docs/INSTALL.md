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
| http://localhost:8080 | Đầy đủ | Trang chủ khách hàng |
| http://localhost:8081 | Khách hàng | Trang chủ khách hàng |
| http://localhost:8082 | Admin | `/admin` |
| http://localhost:8083 | Manager | `/quan-ly` |

Kiểm tra nhanh: `http://localhost:8080/health/database` phải trả `status: connected`.

## Tài khoản demo

| Vai trò | Tên đăng nhập | Mật khẩu | Vào thẳng |
|---|---|---|---|
| Admin | `admin` | `Admin@123` | `/admin` |
| Manager | `manager` | `Manager@123` | `/quan-ly` |
| Khách hàng | `minhanh` | `Customer@123` | `/ve-cua-toi` |

Đổi mật khẩu/ thêm tài khoản trong trang **Admin → Tài khoản** sau khi đăng nhập. Đây là tài khoản seed cho môi trường phát triển, **không dùng cho production**.

## Biến môi trường

Sao chép `.env.example` thành `.env` rồi sửa tối thiểu `POSTGRES_PASSWORD`:

```dotenv
POSTGRES_DB=airline_ticketing
POSTGRES_USER=airline_app
POSTGRES_PASSWORD=<mật-khẩu-riêng>
POSTGRES_PORT=5432

ASPNETCORE_PORT=8080
WEB_PORT_USER=8081
WEB_PORT_ADMIN=8082
WEB_PORT_MANAGER=8083
ASPNETCORE_ENVIRONMENT=Development
```

`.env` đã nằm trong `.gitignore`; không commit hoặc gửi mật khẩu qua chat.

## Lệnh thường dùng

```bash
docker compose up --build -d      # build + chạy toàn bộ
docker compose ps                 # xem trạng thái
docker compose logs -f web-user   # log container khách hàng
docker compose down               # dừng, giữ dữ liệu
docker compose down -v            # dừng và xoá dữ liệu PostgreSQL
docker compose up -d --build web-admin   # chỉ build/chạy lại admin
```

## Tài liệu liên quan

- [Thiết kế cơ sở dữ liệu và DTO](DATABASE-DESIGN.md)
- [Phân tích yêu cầu và use case](PHAN-TICH-YEU-CAU-VA-USE-CASE.md)
- [Quy trình làm việc nhóm](TEAM-WORKFLOW.md)
- [Thiết kế UI và route](design/UI-SPEC.md)
