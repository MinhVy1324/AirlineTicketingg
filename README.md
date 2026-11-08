# AirlineTicketing

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
