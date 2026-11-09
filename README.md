# AirlineTicketing

Hệ thống bán vé máy bay (ASP.NET Core MVC + PostgreSQL) gồm ba khu vực: khách hàng, admin và manager.

## Cài đặt

| Hệ điều hành | Hướng dẫn |
|---|---|
| Tổng quan, tài khoản demo, biến môi trường | [docs/INSTALL.md](docs/INSTALL.md) |
| Windows 10/11 | [docs/INSTALL-WINDOWS.md](docs/INSTALL-WINDOWS.md) |
| macOS (Apple Silicon & Intel) | [docs/INSTALL-MACOS.md](docs/INSTALL-MACOS.md) |
| Chi tiết Docker Compose | [docs/DOCKER-SETUP.md](docs/DOCKER-SETUP.md) |

## Chạy môi trường phát triển bằng Docker

Xem [hướng dẫn Docker, PostgreSQL và cách clone/chạy project](docs/DOCKER-SETUP.md).

```powershell
Copy-Item .env.example .env   # thay POSTGRES_PASSWORD
docker compose up --build -d
```

| Địa chỉ | Khu vực |
|---|---|
| http://127.0.0.1/ | Admin (`/` → `/admin`) |
| http://127.0.0.2/ | Manager (`/` → `/quan-ly`) |
| http://127.0.0.3/ | Khách hàng |
| http://127.0.0.1:8080/ | Tất cả (đầy đủ) |

> **Windows/macOS + Docker Desktop:** nếu `127.0.0.2`/`127.0.0.3` không truy cập được, đặt `WEB_PORT_MANAGER=8082`, `WEB_PORT_USER=8083` trong `.env` và dùng `http://127.0.0.1:8082/`, `http://127.0.0.1:8083/` (hoặc thêm hostname vào file `hosts`).

## Thiết kế dữ liệu và DTO

Xem [ERD, ràng buộc dữ liệu và hợp đồng DTO](docs/DATABASE-DESIGN.md).

## Phân tích yêu cầu

Xem [tài liệu phân tích yêu cầu và mô hình hệ thống](docs/PHAN-TICH-YEU-CAU-VA-USE-CASE.md).
