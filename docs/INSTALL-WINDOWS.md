# Cài đặt trên Windows

Áp dụng cho Windows 10 22H2/Windows 11, PowerShell 7 hoặc Windows PowerShell 5.1. Khuyến nghị dùng **Docker Desktop**.

> Đang dùng macOS? Xem [INSTALL-MACOS.md](INSTALL-MACOS.md).

## 1. Cài công cụ

Mở PowerShell **với quyền Administrator** và chạy:

```powershell
winget install --id Git.Git -e
winget install --id Docker.DockerDesktop -e
```

Nếu định chạy local (không chỉ Docker), cài thêm .NET 10 SDK:

```powershell
winget install --id Microsoft.DotNet.SDK.10 -e
```

Đóng và mở lại PowerShell sau khi cài để nhận `PATH` mới.

### Bật WSL 2 (backend mặc định của Docker Desktop)

```powershell
wsl --install
wsl --set-default-version 2
```

Khởi động lại máy nếu được yêu cầu. Mở **Docker Desktop**, chờ engine chuyển sang trạng thái *Running* và giữ **Linux containers**.

### Kiểm tra

```powershell
git --version
docker --version
docker compose version
docker info --format "{{.ServerVersion}}"
dotnet --version        # chỉ cần nếu chạy local
```

`docker info` phải in ra phiên bản Server; nếu báo lỗi kết nối, mở Docker Desktop và chờ khởi động xong.

## 2. Lấy mã nguồn

```powershell
git clone <repository-url> AirlineTicketing
Set-Location AirlineTicketing
```

Nếu dùng SSH: `git clone git@github.com:<org>/<repo>.git`.

## 3. Tạo file cấu hình

```powershell
Copy-Item .env.example .env
notepad .env
```

Sửa `POSTGRES_PASSWORD` thành mật khẩu riêng (tối thiểu 12 ký tự, không dùng mật khẩu mẫu). Các cổng mặc định `8080–8083`, `5432`; đổi nếu bị trùng.

## 4. Chạy bằng Docker (khuyến nghị)

```powershell
docker compose config --quiet
docker compose up --build -d
docker compose ps
```

Lần đầu sẽ build image và tải PostgreSQL/.NET image, mất vài phút. Trạng thái đúng:

```text
airline-ticketing-db-1        Up (healthy)
airline-ticketing-migrate-1   Exited (0)
airline-web-all               Up
airline-web-user              Up
airline-web-admin             Up
airline-web-manager           Up
```

Kiểm tra website và database:

```powershell
Invoke-RestMethod http://localhost:8080/health/database
(Invoke-WebRequest http://localhost:8082/ -MaximumRedirection 0 -ErrorAction SilentlyContinue).StatusCode
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

```powershell
docker compose down        # giữ dữ liệu PostgreSQL
docker compose down -v     # xoá luôn dữ liệu local
```

## Phương án B: Local + PostgreSQL trong Docker

Phù hợp khi sửa code và cần debug. Chỉ chạy database bằng Docker:

```powershell
docker compose up -d db
docker compose ps db
```

Nạp biến môi trường từ `.env` vào phiên PowerShell hiện tại rồi chạy web:

```powershell
$envValues = Get-Content .env | ConvertFrom-StringData
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=$($envValues.POSTGRES_PORT);Database=$($envValues.POSTGRES_DB);Username=$($envValues.POSTGRES_USER);Password=$($envValues.POSTGRES_PASSWORD)"
$env:Database__ApplyMigrationsOnStartup = "true"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5264"
dotnet run --project AirlineTicketing.Web --no-launch-profile
```

Mở http://localhost:5264. Biến môi trường chỉ có hiệu lực trong cửa sổ PowerShell đó.

### Visual Studio 2022 / VS Code

- **Visual Studio 2022 (17.12+)**: mở `AirlineTicketing.sln`, chọn project `AirlineTicketing.Web`, thêm User Secrets hoặc biến môi trường như trên, nhấn F5.
- **VS Code**: cài extension *C# Dev Kit*, chọn cấu hình `http` trong `launchSettings.json`, đặt connection string trong `appsettings.Development.json` (file này đã bị gitignore? **không** — tuyệt đối không commit mật khẩu; dùng User Secrets).

Nạp User Secrets (không lưu mật khẩu trong repo):

```powershell
dotnet user-secrets init --project AirlineTicketing.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=airline_ticketing;Username=airline_app;Password=<mat-khau>" --project AirlineTicketing.Web
```

## Phương án C: Local hoàn toàn

1. Cài PostgreSQL 16: `winget install --id PostgreSQL.PostgreSQL.16 -e`.
2. Trong **pgAdmin** hoặc `psql`, tạo database và user khớp `.env`:

   ```sql
   CREATE USER airline_app WITH PASSWORD '<mat-khau>';
   CREATE DATABASE airline_ticketing OWNER airline_app;
   ```

3. Chạy các lệnh như phương án B với `Host=localhost`.

## 5. Build, migration và kiểm thử

```powershell
dotnet restore AirlineTicketing.sln
dotnet build AirlineTicketing.sln -c Release
dotnet test AirlineTicketing.Tests/AirlineTicketing.Tests.csproj
```

Áp dụng migration thủ công (khi không dùng Docker):

```powershell
dotnet ef database update `
  --project AirlineTicketing.DAL/AirlineTicketing.DAL.csproj `
  --startup-project AirlineTicketing.Web/AirlineTicketing.Web.csproj
```

Tạo migration mới sau khi đổi entity:

```powershell
dotnet ef migrations add <TenMigration> `
  --project AirlineTicketing.DAL/AirlineTicketing.DAL.csproj `
  --startup-project AirlineTicketing.Web/AirlineTicketing.Web.csproj `
  --output-dir Migrations
```

## 6. Xử lý sự cố

| Hiện tượng | Cách xử lý |
|---|---|
| `docker info` báo không kết nối engine | Mở Docker Desktop, chờ *Engine running*; kiểm tra WSL 2 bằng `wsl --status` |
| `port is already allocated` | Đổi `ASPNETCORE_PORT`, `WEB_PORT_*` hoặc `POSTGRES_PORT` trong `.env`, rồi `docker compose up -d` |
| `migrate` exit khác 0 | `docker compose logs migrate`; thường do sai mật khẩu trong `.env` hoặc volume cũ |
| Đổi `POSTGRES_PASSWORD` nhưng không đăng nhập được | Volume cũ giữ mật khẩu cũ; giữ mật khẩu cũ hoặc `docker compose down -v` nếu chấp nhận mất dữ liệu |
| `MSB3026 ... file is locked by .NET Host` | Dừng tiến trình web đang chạy (`Get-Process AirlineTicketing.Web \| Stop-Process -Force`) rồi build lại |
| Trang trắng/404 sau khi sửa `.cshtml` | View được compile lúc build; dừng web và `dotnet build` lại |
| Lỗi `Invoke-WebRequest -SkipHttpErrorCheck` | Windows PowerShell 5.1 không hỗ trợ; dùng `curl.exe -s -i` |
| Cảnh báo `LF will be replaced by CRLF` | Bình thường trên Windows; không đổi cấu hình git nếu repo đã có `.gitattributes` |
| Cổng 5432 bận vì PostgreSQL cài sẵn | Đổi `POSTGRES_PORT` trong `.env` hoặc tắt service PostgreSQL của Windows |

Xem log chi tiết:

```powershell
docker compose logs --tail 200 web-user web-admin web-manager db migrate
```

## 7. Quy trình cập nhật code

```powershell
git pull
docker compose up --build -d
docker compose ps
```

Nếu có migration mới, service `migrate` tự áp dụng trước khi các container web khởi động. Không xoá volume để "sửa lỗi schema" nếu cần giữ dữ liệu.
