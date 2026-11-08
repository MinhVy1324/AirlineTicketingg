# Cài đặt trên Windows 10/11 (nhánh `NgDang`)

## 1. Yêu cầu

- Windows 10/11 64-bit
- Quyền cài phần mềm
- Kết nối internet (lần đầu tải image/packages)

## 2. Cài công cụ

### Cách nhanh (winget, PowerShell)

```powershell
winget install --id Git.Git -e
winget install --id Docker.DockerDesktop -e
# Chỉ cần nếu chạy/debug local bằng .NET (không bắt buộc cho Docker-only):
winget install --id Microsoft.DotNet.SDK.10 -e
```

Sau khi cài Docker Desktop: mở Docker Desktop, chờ engine chạy (icon ổn định), dùng **Linux containers**. Nếu Docker yêu cầu WSL 2, làm theo hướng dẫn của Docker Desktop rồi khởi động lại nếu cần.

Kiểm tra:

```powershell
git --version
docker --version
docker compose version
docker info
dotnet --version   # nếu đã cài SDK
```

`docker info` phải hiển thị thông tin server. Nếu báo không kết nối engine, mở Docker Desktop và chờ nó sẵn sàng.

## 3. Clone project

```powershell
git clone <URL_REPO> AirlineTicketingg
cd AirlineTicketingg
git checkout NgDang
```

> Thay `<URL_REPO>` bằng URL Git thực tế của nhóm.

## 4. Tạo `.env`

```powershell
Copy-Item .env.example .env
notepad .env
```

Sửa tối thiểu:

```dotenv
POSTGRES_PASSWORD=<đặt-mật-khẩu-mạnh-riêng>
POSTGRES_PORT=5432
ASPNETCORE_PORT=8080
APPLY_DATABASE_MIGRATIONS=true
```

Nếu cổng `5432` hoặc `8080` đang bị chiếm, đổi `POSTGRES_PORT`/`ASPNETCORE_PORT`. **Không commit `.env`**.

## 5. Phương án A — Chạy bằng Docker (khuyến nghị)

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f web
```

- Web: http://localhost:8080 (hoặc cổng `ASPNETCORE_PORT`)
- Health DB: http://localhost:8080/health/database

Service `web` chờ `db` healthy. Migration được áp dụng tự động khi `APPLY_DATABASE_MIGRATIONS=true`.

### Nạp dữ liệu mẫu

Script seed nằm ở `docs/seed/seed-catalog-flights.sql`. Cách đơn giản nhất (không cần cài `psql` trên Windows):

```powershell
Get-Content .env | ForEach-Object {
  if ($_ -match '^\s*([^#=]+)=(.*)$') { Set-Item -Path "Env:$($matches[1].Trim())" -Value $matches[2].Trim() }
}
Get-Content docs/seed/seed-catalog-flights.sql | docker compose exec -T db psql -U $env:POSTGRES_USER -d $env:POSTGRES_DB -v ON_ERROR_STOP=1
```

Nếu có `psql` (PostgreSQL client) trên máy:

```powershell
$env:PGPASSWORD = $env:POSTGRES_PASSWORD
psql -h 127.0.0.1 -p $env:POSTGRES_PORT -U $env:POSTGRES_USER -d $env:POSTGRES_DB -v ON_ERROR_STOP=1 -f docs/seed/seed-catalog-flights.sql
```

Script có `ON CONFLICT DO NOTHING` nên chạy lại nhiều lần an toàn.

### Kiểm tra sau seed

- Tìm chuyến bay: http://localhost:8080/chuyen-bay?from=SGN&to=HAN&depart=20/11/2026&passengers=1&cabin=Phổ%20thông&trip=oneway
- Tra cứu đặt chỗ mẫu (nếu seed tạo): dùng PNR/email theo dữ liệu seed trong `docs/seed/seed-catalog-flights.sql`.

## 6. Phương án B — Local .NET + PostgreSQL trong Docker

Phù hợp khi cần debug/breakpoint.

1. Cài .NET 10 SDK.
2. Chỉ chạy DB:

```powershell
docker compose up -d db
docker compose ps
```

3. Nạp seed (như mục 5).
4. Cấu hình connection string cho app chạy local. Cách an toàn là dùng biến môi trường/session, **không hardcode mật khẩu vào file commit**:

```powershell
Get-Content .env | ForEach-Object {
  if ($_ -match '^\s*([^#=]+)=(.*)$') { Set-Item -Path "Env:$($matches[1].Trim())" -Value $matches[2].Trim() }
}
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=$env:POSTGRES_PORT;Database=$env:POSTGRES_DB;Username=$env:POSTGRES_USER;Password=$env:POSTGRES_PASSWORD"
dotnet run --project AirlineTicketing.Web
```

Nếu `.env` dùng `POSTGRES_PORT=5432` nhưng container map cổng host khác, dùng đúng cổng host. Kiểm tra: `docker compose ps`.

5. Mở URL mà `dotnet run` in ra (thường `http://localhost:5xxx` hoặc profile launchSettings).

## 7. Phương án C — Local hoàn toàn (PostgreSQL cài trên Windows)

1. Cài PostgreSQL 16 và .NET 10 SDK.
2. Tạo database/user tương ứng `.env` (hoặc dùng giá trị riêng).
3. Đặt `ConnectionStrings__DefaultConnection` trỏ tới PostgreSQL local.
4. Áp migration:

```powershell
dotnet ef database update --project AirlineTicketing.DAL --startup-project AirlineTicketing.Web
```

5. Nạp `docs/seed/seed-catalog-flights.sql` bằng `psql`/pgAdmin.
6. `dotnet run --project AirlineTicketing.Web`.

## 8. Migration & schema

- Docker/compose: `Database__ApplyMigrationsOnStartup=true` → web tự `MigrateAsync()` khi khởi động.
- CLI one-shot (hữu ích khi tách service migrate): `dotnet run --project AirlineTicketing.Web -- --migrate`.
- EF CLI thủ công:

```powershell
dotnet tool install --global dotnet-ef   # nếu chưa có
dotnet ef database update --project AirlineTicketing.DAL --startup-project AirlineTicketing.Web
```

## 9. Xử lý sự cố

| Triệu chứng | Kiểm tra | Cách xử lý |
|---|---|---|
| `docker` không kết nối engine | `docker info` | Mở Docker Desktop, chờ running; kiểm tra WSL 2/Linux containers |
| Port `5432`/`8080` bận | `docker compose ps`, `netstat -ano | findstr :8080` | Đổi port trong `.env`, chạy lại `docker compose up -d` |
| `db` unhealthy | `docker compose logs db` | Kiểm tra `POSTGRES_PASSWORD/USER/DB` trong `.env`; xóa volume nếu DB init sai: `docker compose down -v` (mất dữ liệu) |
| Web lỗi kết nối DB | `http://localhost:8080/health/database`, logs web | Kiểm tra connection string/biến env; đảm bảo `db` healthy |
| Không có chuyến bay | trang `/chuyen-bay` trống | Nạp `docs/seed/seed-catalog-flights.sql` |
| Build lỗi file bị khóa | tiến trình `AirlineTicketing.Web` đang chạy | Tắt app đang chạy (`Stop-Process -Name AirlineTicketing.Web -ErrorAction SilentlyContinue`) rồi build lại |
| Lỗi ký tự tiếng Việt trong log/console | PowerShell encoding | Dùng Windows Terminal/PowerShell 7 hoặc `chcp 65001`; file nguồn trong repo là UTF-8 |

## 10. Lệnh hữu ích

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f web
docker compose down
docker compose down -v
dotnet build AirlineTicketing.sln
dotnet test AirlineTicketing.sln
```
