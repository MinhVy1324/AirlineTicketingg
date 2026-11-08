# AirlineTicketing

Hệ thống bán vé máy bay (ASP.NET Core MVC + PostgreSQL + EF Core) — nhánh `NgDang`.

## Cài đặt (Windows & macOS)

| Tài liệu | Nội dung |
|---|---|
| [docs/INSTALL.md](docs/INSTALL.md) | Tổng quan, chọn cách chạy, nạp seed, biến môi trường |
| [docs/INSTALL-WINDOWS.md](docs/INSTALL-WINDOWS.md) | Cài đặt chi tiết trên Windows 10/11 |
| [docs/INSTALL-MACOS.md](docs/INSTALL-MACOS.md) | Cài đặt chi tiết trên macOS (Apple Silicon & Intel) |
| [docs/DOCKER-SETUP.md](docs/DOCKER-SETUP.md) | Chi tiết Docker Compose/PostgreSQL/migrations |

## Chạy nhanh (Docker)

```bash
cp .env.example .env   # đặt POSTGRES_PASSWORD
docker compose up --build -d
# nạp dữ liệu mẫu (xem docs/INSTALL.md hoặc docs/seed/README.md)
```

Web: http://localhost:8080 · Health: http://localhost:8080/health/database

## Tài liệu khác

- [ERD, ràng buộc dữ liệu và hợp đồng DTO](docs/DATABASE-DESIGN.md)
- [Phân tích yêu cầu và mô hình hệ thống](docs/PHAN-TICH-YEU-CAU-VA-USE-CASE.md)
- [Seed dữ liệu mẫu](docs/seed/README.md)
- [Thiết kế UI](docs/design/UI-SPEC.md)

