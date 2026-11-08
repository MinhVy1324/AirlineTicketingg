# Cài đặt trên macOS (Apple Silicon & Intel) — nhánh `NgDang`

## 1. Yêu cầu

- macOS (Apple Silicon/M1-M4 hoặc Intel)
- Quyền cài phần mềm
- Kết nối internet lần đầu

## 2. Cài công cụ

### Homebrew (khuyến nghị)

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

Làm theo hướng dẫn thêm `brew` vào `PATH` (Homebrew sẽ in lệnh cụ thể, khác nhau giữa Apple Silicon `/opt/homebrew` và Intel `/usr/local`).

### Git, Docker, .NET SDK

```bash
brew install git
brew install --cask docker
# Chỉ cần nếu debug/chạy local .NET:
brew install --cask dotnet-sdk
```

Mở **Docker Desktop** từ Applications, chờ engine chạy. Kiểm tra:

```bash
git --version
docker --version
docker compose version
docker info
dotnet --version   # nếu cài SDK
```

> `docker info` phải có thông tin Server. Nếu lỗi, mở Docker Desktop và chờ trạng thái running.

## 3. Clone project

```bash
git clone <URL_REPO> AirlineTicketingg
cd AirlineTicketingg
git checkout NgDang
```

## 4. Tạo `.env`

```bash
cp .env.example .env
open -e .env   # hoặc nano/vim/code
```

Sửa tối thiểu:

```dotenv
POSTGRES_PASSWORD=<đặt-mật-khẩu-mạnh-riêng>
POSTGRES_PORT=5432
ASPNETCORE_PORT=8080
APPLY_DATABASE_MIGRATIONS=true
```

Nếu cổng bận, đổi `POSTGRES_PORT`/`ASPNETCORE_PORT`. **Không commit `.env`**.

## 5. Phương án A — Docker (khuyến nghị)

```bash
docker compose up --build -d
docker compose ps
docker compose logs -f web
```

- Web: http://localhost:8080 (hoặc `ASPNETCORE_PORT`)
- Health: http://localhost:8080/health/database

### Nạp dữ liệu mẫu

```bash
set -a; . ./.env; set +a
docker compose exec -T db psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 < docs/seed/seed-catalog-flights.sql
```

Hoặc dùng `psql` cài local (`brew install libpq` rồi thêm PATH, hoặc `brew install postgresql@16`):

```bash
set -a; . ./.env; set +a
PGPASSWORD="$POSTGRES_PASSWORD" psql -h 127.0.0.1 -p "${POSTGRES_PORT:-5432}" \
  -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
  -f docs/seed/seed-catalog-flights.sql
```

Script an toàn khi chạy lại (`ON CONFLICT DO NOTHING`).

### Kiểm tra

- http://localhost:8080/chuyen-bay?from=SGN&to=HAN&depart=20/11/2026&passengers=1&cabin=Phổ%20thông&trip=oneway
- Xem PNR/email mẫu trong `docs/seed/seed-catalog-flights.sql` để test `/tra-cuu`.

## 6. Phương án B — Local .NET + PostgreSQL trong Docker

```bash
docker compose up -d db
set -a; . ./.env; set +a
# nạp seed (xem mục 5)
export ConnectionStrings__DefaultConnection="Host=localhost;Port=${POSTGRES_PORT:-5432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
dotnet run --project AirlineTicketing.Web
```

Mở URL `dotnet run` in ra. Dùng đúng cổng host của `db` (`docker compose ps`).

## 7. Phương án C — Local hoàn toàn

```bash
brew install postgresql@16
brew services start postgresql@16
```

Tạo DB/user phù hợp, đặt `ConnectionStrings__DefaultConnection`, áp migration và seed:

```bash
dotnet ef database update --project AirlineTicketing.DAL --startup-project AirlineTicketing.Web
psql -h localhost -U <user> -d <db> -v ON_ERROR_STOP=1 -f docs/seed/seed-catalog-flights.sql
dotnet run --project AirlineTicketing.Web
```

## 8. Migration & schema

- Tự động khi `APPLY_DATABASE_MIGRATIONS=true` (compose mặc định bật).
- One-shot: `dotnet run --project AirlineTicketing.Web -- --migrate`
- EF CLI:

```bash
dotnet tool install --global dotnet-ef
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef database update --project AirlineTicketing.DAL --startup-project AirlineTicketing.Web
```

## 9. Xử lý sự cố

| Triệu chứng | Kiểm tra | Cách xử lý |
|---|---|---|
| `Cannot connect to the Docker daemon` | `docker info` | Mở Docker Desktop, chờ running |
| Port bận | `lsof -iTCP:5432 -sTCP:LISTEN` / `:8080` | Đổi port trong `.env`, `docker compose up -d` |
| `db` unhealthy | `docker compose logs db` | Sai password/user/db init; `docker compose down -v` nếu cần reset (mất dữ liệu) |
| Web không kết nối DB | `/health/database`, `docker compose logs web` | Kiểm tra env/connection string; `db` phải healthy |
| Không có chuyến bay | `/chuyen-bay` trống | Nạp seed SQL |
| `dotnet-ef` không tìm thấy | `dotnet tool list -g` | Cài global tool và thêm `$HOME/.dotnet/tools` vào PATH |
| Apple Silicon: image/build chậm/lỗi | Docker Desktop settings | Đảm bảo dùng image hỗ trợ arm64 (postgres:16-alpine OK); thử `docker compose build --no-cache web` nếu cache lỗi |

## 10. Lệnh hữu ích

```bash
docker compose up --build -d
docker compose ps
docker compose logs -f web
docker compose down
docker compose down -v
dotnet build AirlineTicketing.sln
dotnet test AirlineTicketing.sln
```
