# AirlineTicketing

## Chạy môi trường phát triển bằng Docker

Xem [hướng dẫn Docker, PostgreSQL và cách clone/chạy project](docs/DOCKER-SETUP.md).

## Thiết kế dữ liệu và DTO

Xem [ERD, ràng buộc dữ liệu và hợp đồng DTO](docs/DATABASE-DESIGN.md).

<<<<<<< Updated upstream
## Phân tích yêu cầu
=======
- **Luồng nghiệp vụ khép kín:** tìm chuyến → chọn ghế → nhập hành khách → giữ chỗ có hạn → thanh toán
  (thẻ mô phỏng hoặc chuyển khoản) → phát hành vé PDF có QR → tra cứu → đổi/hoàn → báo cáo doanh thu.
- **Ba khu vực tách biệt:** mỗi khu có giao diện, quyền và trang mặc định riêng; có thể chạy 4 instance
  (tất cả, admin, manager, khách hàng) trên cùng một image.
- **Dữ liệu thật:** PostgreSQL + EF Core migrations; hơn 4.000 chuyến, 12 sân bay, 28 tuyến, booking ở
  nhiều trạng thái, thanh toán và yêu cầu đổi/hoàn.
- **Bộ lọc tìm chuyến:** chọn nhiều hãng, khung giờ UTC+7 và tổng giá đã gồm thuế/phí; lịch giá theo ngày
  và số chuyến cập nhật theo bộ lọc, giữ lựa chọn khi đổi ngày hoặc sắp xếp.
- **An toàn cơ bản:** cookie auth theo vai trò, ownership check khi xem/tải vé, chống CSRF, rate limit đăng
  nhập và gửi lại email, không lưu số thẻ, không tự xác nhận chuyển khoản.
- **Vận hành demo:** Data Protection key ring nằm trong volume nên rebuild không mất phiên; job nền giải
  phóng ghế hết hạn; email vé ghi ra outbox khi chưa cấu hình SMTP.
- **Kiểm chứng tự động:** build + unit test và test tích hợp PostgreSQL trên CI, kèm `smoke.ps1` kiểm tra
  health, 3 vai trò, CSV báo cáo và phân quyền PDF.
>>>>>>> Stashed changes

Xem [tài liệu phân tích yêu cầu và mô hình hệ thống](docs/PHAN-TICH-YEU-CAU-VA-USE-CASE.md).

## Tài khoản và nghiệp vụ tài chính

Hệ thống có đăng ký/đăng nhập, phân quyền Admin/Nhân viên/Khách hàng, quản lý tài khoản, giao dịch thanh toán giả lập trong môi trường Development, hoàn tiền nội bộ và báo cáo doanh thu thuần. Cấu hình mật khẩu seed quản trị theo [hướng dẫn Docker](docs/DOCKER-SETUP.md); không có tài khoản quản trị hoặc mật khẩu mẫu cố định trong mã nguồn.

## Chạy kiểm thử tích hợp PostgreSQL

Các kiểm thử tích hợp dùng PostgreSQL thật và tự tạo/xóa schema riêng theo từng test. Hãy đặt `AIRLINETICKETING_TEST_CONNECTION` trỏ tới một database kiểm thử mà tài khoản có quyền tạo schema, rồi chạy:

```powershell
$env:AIRLINETICKETING_TEST_CONNECTION = "Host=localhost;Port=5432;Database=airline_ticketing_test;Username=airline_app;Password=<mật-khẩu>"
dotnet test AirlineTicketing.sln
```

<<<<<<< Updated upstream
Nếu biến kết nối chưa được đặt, các kiểm thử PostgreSQL sẽ được đánh dấu bỏ qua.
=======
Kiểm tra: `http://127.0.0.1:8080/health/database` trả `status: connected`.

| Địa chỉ | Khu vực | Mở `/` sẽ tới |
| --- | --- | --- |
| http://127.0.0.1:8080 | Đầy đủ | Trang chủ khách hàng |
| http://127.0.0.3 | Khách hàng | Trang chủ khách hàng |
| http://127.0.0.1 | Admin | `/admin` |
| http://127.0.0.2 | Manager | `/quan-ly` |

> **Windows/macOS + Docker Desktop:** nếu `127.0.0.2`/`127.0.0.3` không truy cập được, đặt
> `WEB_PORT_MANAGER=8082`, `WEB_PORT_USER=8083` trong `.env` rồi dùng `http://127.0.0.1:8082/`,
> `http://127.0.0.1:8083/`. Chi tiết: [docs/INSTALL-WINDOWS.md](docs/INSTALL-WINDOWS.md).

### Lệnh thường dùng

```bash
docker compose ps                  # trạng thái dịch vụ
docker compose logs -f web         # log instance đầy đủ
docker compose down                # dừng, giữ dữ liệu
docker compose down -v             # dừng và xoá dữ liệu
pwsh ./scripts/smoke.ps1           # smoke test toàn hệ thống
pwsh ./scripts/reset-demo.ps1      # reset dữ liệu demo (có xác nhận)
```

## Kiểm thử và CI

```bash
# Có PostgreSQL test DB: chạy cả unit test và test tích hợp
AIRLINE_TEST_CONNECTION="Host=localhost;Port=5432;Database=airline_ticketing_test;Username=airline_app;Password=..." \
  dotnet test AirlineTicketing.Tests/AirlineTicketing.Tests.csproj

# Không có biến môi trường: chạy unit test, test tích hợp tự bỏ qua
dotnet test AirlineTicketing.Tests/AirlineTicketing.Tests.csproj
```

CI gồm 2 job: **build + unit test** và **integration test với PostgreSQL 16**. Xem
[`.github/workflows/ci.yml`](.github/workflows/ci.yml).

## Cấu trúc repository

```text
AirlineTicketing.Web/       # MVC, views, middleware, job nền, PDF/email
AirlineTicketing.BUS/       # nghiệp vụ
AirlineTicketing.DAL/       # EF Core, migrations
AirlineTicketing.Domain/    # entity
AirlineTicketing.DTO/       # DTO
AirlineTicketing.Tests/     # unit + integration test
docs/                       # phân tích, thiết kế, cài đặt, demo
scripts/                    # smoke test, reset dữ liệu demo
compose.yaml                # PostgreSQL + migrate + 4 web instance
```

## Tài liệu

| Tài liệu | Nội dung |
| --- | --- |
| [Kịch bản demo 15 phút](docs/DEMO-SCRIPT.md) | Thao tác, kết quả mong đợi, lời thoại, phương án dự phòng |
| [Checklist nghiệm thu](docs/DEMO-CHECKLIST.md) | Map FR/UC → màn hình, checklist theo vai trò, giới hạn demo |
| [Lộ trình nâng cấp](docs/UPGRADE-ROADMAP.md) | Các bước cải tiến, hành vi đã sửa và phần còn lại |
| [Cài đặt](docs/INSTALL.md) | Chọn cách chạy, tài khoản demo, biến môi trường |
| [Docker & vận hành](docs/DOCKER-SETUP.md) | Compose, volume, health, xử lý sự cố |
| [Thiết kế cơ sở dữ liệu](docs/DATABASE-DESIGN.md) | ERD, ràng buộc, DTO |
| [Phân tích yêu cầu & use case](docs/PHAN-TICH-YEU-CAU-VA-USE-CASE.md) | FR-01…FR-13, UC-01…UC-12, traceability |
| [Quy trình làm việc nhóm](docs/TEAM-WORKFLOW.md) | Branch, PR, review, CI |
| [Ảnh chụp màn hình](docs/screenshots/) | Trang chủ, tìm chuyến, dashboard, báo cáo, quản trị |

## Giới hạn của bản demo

Bản này phục vụ **demo/báo cáo** với thẻ mô phỏng nội bộ và tùy chọn [VNPAY Sandbox](docs/VNPAY-SANDBOX.md),
email mặc định ghi ra outbox (có thể cấu hình SMTP), mỗi đơn tối đa 9 khách và hỗ trợ khứ hồi. Hạng Thương gia dùng chung sơ đồ ghế theo giá demo; đổi riêng khách/chặng chưa hỗ trợ. Báo cáo theo ngày giao dịch/phát hành vé, phân quyền cố định 03 vai trò; lịch tàu bay chưa tính thời gian quay đầu. Chi tiết và cách trình bày khi bảo vệ:
[Checklist — Giới hạn đã biết](docs/DEMO-CHECKLIST.md#8-giới-hạn-đã-biết-của-bản-demo).
>>>>>>> Stashed changes
