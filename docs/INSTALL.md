# Cài đặt AirlineTicketing (nhánh `NgDang`)

Tài liệu này hướng dẫn thành viên mới clone project và chạy hệ thống trên Windows hoặc macOS.

## Trạng thái nhánh

Nhánh `NgDang` hiện dùng kiến trúc BUS/DAL/DTO cho nghiệp vụ chuyến bay/danh mục (commit `140883f`) và PostgreSQL + EF Core migrations. Dữ liệu mẫu nghiệp vụ (sân bay, tàu bay, tuyến bay, chuyến bay, một vài vé/đặt chỗ) được nạp bằng script SQL trong `docs/seed/`.

> Lưu ý: một số màn hình admin/manager/đặt vé/vé của tôi vẫn đang được hoàn thiện để chuyển hoàn toàn sang dữ liệu thật. Tra cứu đặt chỗ (`/tra-cuu`), tìm chuyến bay (`/chuyen-bay`) và quản lý danh mục/lịch bay đã đọc từ cơ sở dữ liệu.

## Chọn cách chạy

| Cách | Phù hợp khi | Cần cài |
|---|---|---|
| **Docker (khuyến nghị)** | Muốn chạy nhanh, ít lệch môi trường | Docker Desktop, Git |
| **Local + PostgreSQL trong Docker** | Cần debug .NET, hot reload | .NET 10 SDK, Docker Desktop, Git |
| **Local hoàn toàn** | Đã có PostgreSQL 16 | .NET 10 SDK, PostgreSQL 16, Git |

Hướng dẫn chi tiết:
- [Windows 10/11](INSTALL-WINDOWS.md)
- [macOS (Apple Silicon & Intel)](INSTALL-MACOS.md)
- [Chi tiết Docker Compose](DOCKER-SETUP.md)

## Chạy nhanh bằng Docker

```bash
cp .env.example .env
# Sửa POSTGRES_PASSWORD trong .env

docker compose up --build -d
docker compose ps
```

Mở: http://localhost:8080 (cổng `ASPNETCORE_PORT` trong `.env`, mặc định 8080).

Kiểm tra DB: http://localhost:8080/health/database → `{"status":"connected","database":"PostgreSQL"}`.

## Nạp dữ liệu mẫu (quan trọng)

Sau khi DB/container đã chạy và migration đã áp dụng, nạp dữ liệu seed để có sân bay, tuyến bay, chuyến bay và mẫu tra cứu:

```bash
# Linux/macOS/Git Bash (đọc biến từ .env)
set -a; . ./.env; set +a
PGPASSWORD="$POSTGRES_PASSWORD" psql -h 127.0.0.1 -p "${POSTGRES_PORT:-5432}" \
  -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
  -f docs/seed/seed-catalog-flights.sql
```

Windows PowerShell (ví dụ):

```powershell
Get-Content .env | ForEach-Object { if ($_ -match '^\s*([^#=]+)=(.*)$') { Set-Item -Path "Env:$($matches[1].Trim())" -Value $matches[2].Trim() } }
$env:PGPASSWORD = $env:POSTGRES_PASSWORD
psql -h 127.0.0.1 -p $env:POSTGRES_PORT -U $env:POSTGRES_USER -d $env:POSTGRES_DB -v ON_ERROR_STOP=1 -f docs/seed/seed-catalog-flights.sql
```

Nếu máy chưa có `psql`, có thể chạy qua container `db` (điều chỉnh user/db theo `.env`):

```bash
docker compose exec -T db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 < docs/seed/seed-catalog-flights.sql
```

Xem thêm: [docs/seed/README.md](seed/README.md).

## Biến môi trường

Sao chép `.env.example` thành `.env` và đổi tối thiểu `POSTGRES_PASSWORD`. **Không commit `.env`**.

| Biến | Ý nghĩa | Mặc định mẫu |
|---|---|---|
| `POSTGRES_DB` | Tên database | `airline_ticketing` |
| `POSTGRES_USER` | User PostgreSQL | `airline_app` |
| `POSTGRES_PASSWORD` | Mật khẩu DB (bắt buộc đổi) | *(đặt riêng)* |
| `POSTGRES_PORT` | Cổng PostgreSQL trên host | `5432` (đổi nếu bận) |
| `ASPNETCORE_PORT` | Cổng web trên host | `8080` |
| `ASPNETCORE_ENVIRONMENT` | Môi trường ASP.NET | `Development` |
| `APPLY_DATABASE_MIGRATIONS` | Tự áp dụng EF migrations khi web khởi động | `true` |

## Lệnh thường dùng

```bash
docker compose up --build -d
docker compose ps
docker compose logs -f web
docker compose logs -f db
docker compose down
docker compose down -v            # xoá cả dữ liệu PostgreSQL
docker compose up -d --build web  # rebuild web
```

## Tài liệu liên quan

- [DOCKER-SETUP.md](DOCKER-SETUP.md)
- [Seed dữ liệu](seed/README.md)
- [Thiết kế CSDL/DTO](DATABASE-DESIGN.md)
- [Phân tích yêu cầu](PHAN-TICH-YEU-CAU-VA-USE-CASE.md)
- [UI spec](design/UI-SPEC.md)
