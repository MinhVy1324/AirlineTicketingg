# Cài đặt trên macOS

Áp dụng cho macOS 13 Ventura trở lên, cả Apple Silicon (M1–M4) và Intel. Khuyến nghị dùng **Docker Desktop**.

> Đang dùng Windows? Xem [INSTALL-WINDOWS.md](INSTALL-WINDOWS.md).

## 1. Cài công cụ

### Cách nhanh với Homebrew

Nếu chưa có Homebrew:

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

Sau đó:

```bash
brew install git
brew install --cask docker
```

Mở **Docker.app** từ Applications, chờ icon cá voi trên menu bar chuyển sang *Docker Desktop is running*.

Nếu định chạy local (không chỉ Docker), cài .NET 10 SDK:

```bash
brew install --cask dotnet-sdk
```

Hoặc tải bản cài chính thức tại <https://dotnet.microsoft.com/download/dotnet/10.0> (chọn đúng kiến trúc Arm64 hoặc x64).

### Kiểm tra

```bash
git --version
docker --version
docker compose version
docker info --format '{{.ServerVersion}}'
dotnet --version        # chỉ cần nếu chạy local
```

`docker info` phải in ra phiên bản Server. Nếu báo `Cannot connect to the Docker daemon`, mở Docker.app và chờ khởi động xong.

> **Apple Silicon:** các image `postgres:16-alpine`, `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0` đều có bản arm64, không cần bật Rosetta. Nếu gặp image chỉ có amd64, thêm tạm `platform: linux/amd64` cho service đó (chậm hơn).

## 2. Lấy mã nguồn

```bash
git clone <repository-url> AirlineTicketing
cd AirlineTicketing
```

Nếu dùng SSH: `git clone git@github.com:<org>/<repo>.git`.

## 3. Tạo file cấu hình

```bash
cp .env.example .env
nano .env        # hoặc mở bằng VS Code: code .env
```

Sửa `POSTGRES_PASSWORD` thành mật khẩu riêng. Cổng mặc định `8080–8083`, `5432`; đổi nếu bị trùng (`lsof -nP -iTCP:8080 -sTCP:LISTEN`).

## 4. Chạy bằng Docker (khuyến nghị)

```bash
docker compose config --quiet
docker compose up --build -d
docker compose ps
```

Lần đầu build có thể mất vài phút. Trạng thái đúng:

```text
airline-ticketing-db-1        Up (healthy)
airline-ticketing-migrate-1   Exited (0)
airline-web-all               Up
airline-web-user              Up
airline-web-admin             Up
airline-web-manager           Up
```

Kiểm tra:

```bash
curl -s http://localhost:8080/health/database
# {"status":"connected","database":"PostgreSQL"}
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8082/
# 302 (chuyển tới /admin)
```

Mở trình duyệt:

| Địa chỉ | Khu vực |
|---|---|
| http://localhost:8080 | Đầy đủ |
| http://localhost:8081 | Khách hàng |
| http://localhost:8082 | Admin (`/` tự chuyển tới `/admin`) |
| http://localhost:8083 | Manager (`/` tự chuyển tới `/quan-ly`) |

Đăng nhập bằng tài khoản ở [INSTALL.md](INSTALL.md#tài-khoản-demo).

Dừng hệ thống:

```bash
docker compose down        # giữ dữ liệu PostgreSQL
docker compose down -v     # xoá luôn dữ liệu local
```

## Phương án B: Local + PostgreSQL trong Docker

Phù hợp khi sửa code, cần breakpoint hoặc hot reload. Chỉ chạy database bằng Docker:

```bash
docker compose up -d db
docker compose ps db
```

Nạp biến môi trường từ `.env` vào shell hiện tại rồi chạy web:

```bash
set -a
source .env
set +a

export ConnectionStrings__DefaultConnection="Host=localhost;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
export Database__ApplyMigrationsOnStartup=true
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5264

dotnet run --project AirlineTicketing.Web --no-launch-profile
```

Mở http://localhost:5264. Biến môi trường chỉ tồn tại trong cửa sổ Terminal đó.

### Visual Studio Code / Rider

- **VS Code**: cài extension *C# Dev Kit*, chọn cấu hình `http` trong `launchSettings.json`. Dùng User Secrets cho connection string:

  ```bash
  dotnet user-secrets init --project AirlineTicketing.Web
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=airline_ticketing;Username=airline_app;Password=<mat-khau>" --project AirlineTicketing.Web
  ```

- **JetBrains Rider**: mở `AirlineTicketing.sln`, đặt biến môi trường trong Run Configuration, nhấn Run.

Không đưa mật khẩu vào `appsettings.Development.json` hay commit lên git.

## Phương án C: Local hoàn toàn

1. Cài PostgreSQL 16: `brew install postgresql@16 && brew services start postgresql@16`.
2. Tạo database và user khớp `.env`:

   ```bash
   psql postgres -c "CREATE USER airline_app WITH PASSWORD '<mat-khau>';"
   psql postgres -c "CREATE DATABASE airline_ticketing OWNER airline_app;"
   ```

3. Chạy như phương án B với `Host=localhost`.

## 5. Build, migration và kiểm thử

```bash
dotnet restore AirlineTicketing.sln
dotnet build AirlineTicketing.sln -c Release
dotnet test AirlineTicketing.Tests/AirlineTicketing.Tests.csproj
```

Áp dụng migration thủ công:

```bash
dotnet ef database update \
  --project AirlineTicketing.DAL/AirlineTicketing.DAL.csproj \
  --startup-project AirlineTicketing.Web/AirlineTicketing.Web.csproj
```

Tạo migration mới:

```bash
dotnet ef migrations add <TenMigration> \
  --project AirlineTicketing.DAL/AirlineTicketing.DAL.csproj \
  --startup-project AirlineTicketing.Web/AirlineTicketing.Web.csproj \
  --output-dir Migrations
```

Nếu chưa có `dotnet ef`: `dotnet tool install --global dotnet-ef`.

## 6. Xử lý sự cố

| Hiện tượng | Cách xử lý |
|---|---|
| `Cannot connect to the Docker daemon` | Mở Docker.app, chờ engine chạy; kiểm tra `docker context ls` |
| `port is already allocated` | `lsof -nP -iTCP:8080 -sTCP:LISTEN` để tìm tiến trình, hoặc đổi `ASPNETCORE_PORT`/`WEB_PORT_*` trong `.env` |
| `migrate` exit khác 0 | `docker compose logs migrate`; thường do sai mật khẩu trong `.env` hoặc volume cũ |
| Đổi mật khẩu Postgres không có hiệu lực | Volume cũ giữ mật khẩu cũ; giữ mật khẩu cũ hoặc `docker compose down -v` nếu chấp nhận mất dữ liệu |
| `MSB3026 ... file is locked` | Dừng tiến trình web đang chạy (`pkill -f AirlineTicketing.Web`) rồi build lại |
| `dotnet: command not found` | Kiểm tra `dotnet --list-sdks`; thêm `/usr/local/share/dotnet` hoặc `~/.dotnet` vào `PATH` |
| `zsh: bad assignment` khi `source .env` | `.env` không được có khoảng trắng quanh `=` và không dùng dấu ngoặc kép kiểu PowerShell |
| Cảnh báo quyền file khi mount volume | Docker Desktop → Settings → Resources → File Sharing, thêm thư mục project |
| Trình duyệt tự chuyển HTTPS / cảnh báo certificate | Dùng đúng `http://localhost:<port>`; project chạy HTTP trong Docker |
| Trang 404 sau khi sửa `.cshtml` | View compile lúc build; dừng web và `dotnet build` lại |

Xem log chi tiết:

```bash
docker compose logs --tail 200 web-user web-admin web-manager db migrate
```

## 7. Quy trình cập nhật code

```bash
git pull
docker compose up --build -d
docker compose ps
```

Service `migrate` tự áp dụng migration mới trước khi các container web khởi động. Không xoá volume nếu cần giữ dữ liệu.
