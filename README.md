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
| http://localhost:8080 | Tất cả (đầy đủ) |
| http://localhost:8081 | Khách hàng |
| http://localhost:8082 | Admin (`/` → `/admin`) |
| http://localhost:8083 | Manager (`/` → `/quan-ly`) |

## Thiết kế dữ liệu và DTO

Xem [ERD, ràng buộc dữ liệu và hợp đồng DTO](docs/DATABASE-DESIGN.md).

## Phân tích yêu cầu

Xem [tài liệu phân tích yêu cầu và mô hình hệ thống](docs/PHAN-TICH-YEU-CAU-VA-USE-CASE.md).
