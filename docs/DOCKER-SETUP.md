# Docker và triển khai môi trường phát triển

Hướng dẫn này áp dụng cho Windows, Docker Desktop, Docker Compose v2, ASP.NET Core/.NET 10 và PostgreSQL 16. Docker Compose chạy website và cơ sở dữ liệu trong cùng một network riêng; website kết nối PostgreSQL qua hostname `db`. Máy host truy cập website qua `http://localhost:8080` và PostgreSQL qua `localhost:5432`.

## 1. Môi trường cần có — DOCKER-01

1. Cài Docker Desktop cho Windows theo [hướng dẫn chính thức](https://docs.docker.com/desktop/setup/install/windows-install/). Backend WSL 2 là lựa chọn mặc định phù hợp cho đa số máy Windows; bật virtualization/WSL 2 nếu trình cài đặt yêu cầu.
2. Mở Docker Desktop, chờ trạng thái engine chạy và chọn Linux containers.
3. Mở PowerShell ở thư mục project, kiểm tra:

```powershell
docker --version
docker compose version
docker info
```

Hai lệnh đầu xác nhận CLI/Compose đã cài. `docker info` phải hiển thị cả thông tin Client và Server; nếu báo không kết nối được engine, hãy mở Docker Desktop và chờ khởi động xong.

## 2. Biến môi trường và thông tin nhạy cảm — DOCKER-03

Khi clone project lần đầu, tạo file cấu hình local:

```powershell
Copy-Item .env.example .env
notepad .env
```

Project chỉ dùng **một file `.env` local** làm nguồn cấu hình: Docker Compose tự đọc file này để thay biến trong `compose.yaml`, và service `web` cũng nạp chính file đó qua `env_file`. `.env.example` chỉ là mẫu để thành viên tạo `.env` khi clone; không cần tạo thêm file cấu hình khác. Khi chạy ASP.NET trực tiếp bằng `dotnet run` ngoài Docker, Compose không tham gia nên dùng đoạn PowerShell ở mục 4 để nạp cùng `.env` vào process.

Thay `POSTGRES_PASSWORD` trong `.env` bằng mật khẩu ngẫu nhiên riêng cho máy/môi trường đó. Không commit `.env`; `.gitignore` đã bỏ qua file này và cho phép commit `.env.example`. Không sử dụng mật khẩu mẫu cho môi trường triển khai thật.

| Biến | Mục đích | Mặc định mẫu |
|---|---|---|
| `POSTGRES_DB` | Tên database được tạo trong PostgreSQL | `airline_ticketing` |
| `POSTGRES_USER` | User ứng dụng của PostgreSQL | `airline_app` |
| `POSTGRES_PASSWORD` | Mật khẩu local; phải thay trước khi chạy | Placeholder trong `.env.example` |
| `POSTGRES_PORT` | Cổng PostgreSQL trên host | `5432` |
| `ASPNETCORE_PORT` | Cổng website trên host | `8080` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment | `Development` |
| `APPLY_DATABASE_MIGRATIONS` | Tự áp dụng EF Core migrations khi website khởi động | `true` cho Docker local |

Nếu cổng 5432 hoặc 8080 đã được dùng, đổi giá trị tương ứng trong `.env`. Bên trong Compose, connection string luôn dùng `Host=db;Port=5432`; từ ứng dụng chạy trực tiếp trên Windows, dùng `Host=localhost` và cổng host trong `.env`.

## 3. Chạy PostgreSQL — DOCKER-02, DOCKER-03

`compose.yaml` định nghĩa PostgreSQL 16 Alpine, health check `pg_isready`, ánh xạ cổng chỉ trên loopback `127.0.0.1`, và named volume `postgres_data` lưu dữ liệu ngoài vòng đời container.

```powershell
docker compose config --quiet
docker compose up -d db
docker compose ps
docker compose logs -f db
```

Chỉ chuyển sang bước tiếp theo khi service `db` có trạng thái `healthy`. Kiểm tra trực tiếp:

```powershell
docker compose exec db sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

## 4. Kết nối ASP.NET Core đến PostgreSQL — DOCKER-04

Compose truyền `ConnectionStrings__DefaultConnection` vào container website. ASP.NET Core ánh xạ biến `__` thành cấu hình `ConnectionStrings:DefaultConnection`; container web kết nối tới hostname `db`, không dùng `localhost` (vì `localhost` bên trong web container trỏ về chính web container).

`appsettings.json` không chứa user/password. `Program.cs` yêu cầu connection string từ cấu hình và cung cấp endpoint kiểm tra DB:

```text
GET /health/database
```

Khi cần chạy website trực tiếp từ Visual Studio/PowerShell trong khi PostgreSQL vẫn chạy trong Docker, lấy thông tin `.env` và cấu hình host connection như sau:

```powershell
$envValues = Get-Content .env | ConvertFrom-StringData
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=$($envValues.POSTGRES_PORT);Database=$($envValues.POSTGRES_DB);Username=$($envValues.POSTGRES_USER);Password=$($envValues.POSTGRES_PASSWORD)"
$env:Database__ApplyMigrationsOnStartup = 'true'
dotnet run --project AirlineTicketing.Web/AirlineTicketing.Web.csproj
```

Biến môi trường chỉ tồn tại trong cửa sổ PowerShell đó. Không đưa connection string có mật khẩu vào source control, log hoặc ảnh chụp màn hình.

## 5. Build Docker image cho website — DOCKER-05

`AirlineTicketing.Web/Dockerfile` dùng build stage `dotnet/sdk:10.0` và runtime stage `dotnet/aspnet:10.0`. Docker build context ở thư mục gốc solution để các `ProjectReference` đến BUS, DAL, DTO và Domain được restore/publish cùng website. `.dockerignore` loại `bin/`, `obj/`, `.git`, `.env` và tài liệu khỏi build context.

Build riêng website:

```powershell
docker compose build web
```

## 6. Chạy website và PostgreSQL cùng nhau — DOCKER-06

Từ thư mục có `compose.yaml`:

```powershell
docker compose up --build -d
docker compose ps
docker compose logs -f web
```

Mở `http://localhost:8080`. Kiểm tra kết nối C# → PostgreSQL:

```powershell
Invoke-RestMethod http://localhost:8080/health/database
```

Kết quả thành công có dạng `status: connected`, `database: PostgreSQL`. Compose đợi `db` đạt `healthy` trước khi tạo service `web`; nếu ứng dụng không khởi động, đọc log bằng `docker compose logs --tail 100 web db`.

## 7. Khởi động lại và bảo toàn dữ liệu — DOCKER-07

PostgreSQL chỉ đọc `POSTGRES_DB`, `POSTGRES_USER` và `POSTGRES_PASSWORD` để khởi tạo khi volume còn trống. Nếu đã có volume từ lần chạy trước, đổi các giá trị này trong `.env` không tự đổi tài khoản/mật khẩu trong database; hãy giữ thông tin khớp với volume hiện có hoặc chủ động đổi mật khẩu bên trong PostgreSQL trước. Không xóa volume để xử lý lệch cấu hình nếu cần giữ dữ liệu.

Kiểm tra restart:

```powershell
docker compose restart db
docker compose ps
Invoke-RestMethod http://localhost:8080/health/database
```

Named volume `postgres_data` tồn tại độc lập với container. Vì vậy các lệnh sau không xóa database:

```powershell
docker compose down
docker compose up -d
```

Không thêm `-v` vào lệnh `docker compose down` nếu cần giữ dữ liệu. `docker compose down -v` xóa volume và toàn bộ dữ liệu PostgreSQL của môi trường local.

## 8. Hướng dẫn clone và chạy cho thành viên — DOCKER-08

```powershell
git clone <repository-url>
Set-Location AirlineTicketing
Copy-Item .env.example .env
notepad .env
docker compose config --quiet
docker compose up --build -d
docker compose ps
Invoke-RestMethod http://localhost:8080/health/database
```

Thành viên cần thay mật khẩu mẫu trong `.env` trước khi khởi động. Khi cần xem log, chạy `docker compose logs -f web db`; để dừng container mà giữ dữ liệu, chạy `docker compose down`.

## 9. EF Core migrations

Migrations nằm trong `AirlineTicketing.DAL/Migrations`. Compose development đặt `APPLY_DATABASE_MIGRATIONS=true` để website áp dụng migrations khi khởi động sau khi PostgreSQL healthy. Sau khi thay đổi entity/configuration, tạo migration mới:

```powershell
dotnet ef migrations add <MigrationName> `
  --project AirlineTicketing.DAL/AirlineTicketing.DAL.csproj `
  --startup-project AirlineTicketing.Web/AirlineTicketing.Web.csproj `
  --output-dir Migrations
```

Commit migration cùng thay đổi model. Ở production, đặt `APPLY_DATABASE_MIGRATIONS=false` và chạy migration qua bước triển khai/DBA được kiểm soát thay vì tự động đổi schema khi ứng dụng khởi động.

## 10. Lệnh thường dùng

| Mục đích | Lệnh |
|---|---|
| Xem trạng thái | `docker compose ps` |
| Theo dõi log | `docker compose logs -f web db` |
| Dừng, giữ dữ liệu | `docker compose down` |
| Khởi động lại | `docker compose up -d` |
| Build lại website | `docker compose up --build -d web` |
| Xóa dữ liệu local có chủ đích | `docker compose down -v` |

`compose.yaml` giới hạn các port publish trên `127.0.0.1`; cấu hình này dành cho phát triển trên cùng máy. Khi triển khai server, cần cấu hình reverse proxy/TLS, firewall, secret manager và chính sách sao lưu phù hợp.
