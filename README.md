# AirlineTicketing

## Chạy môi trường phát triển bằng Docker

Xem [hướng dẫn Docker, PostgreSQL và cách clone/chạy project](docs/DOCKER-SETUP.md).

## Thiết kế dữ liệu và DTO

Xem [ERD, ràng buộc dữ liệu và hợp đồng DTO](docs/DATABASE-DESIGN.md).

## Phân tích yêu cầu

Xem [tài liệu phân tích yêu cầu và mô hình hệ thống](docs/PHAN-TICH-YEU-CAU-VA-USE-CASE.md).

## Tài khoản và nghiệp vụ tài chính

Hệ thống có đăng ký/đăng nhập, phân quyền Admin/Nhân viên/Khách hàng, quản lý tài khoản, giao dịch thanh toán giả lập trong môi trường Development, hoàn tiền nội bộ và báo cáo doanh thu thuần. Cấu hình mật khẩu seed quản trị theo [hướng dẫn Docker](docs/DOCKER-SETUP.md); không có tài khoản quản trị hoặc mật khẩu mẫu cố định trong mã nguồn.

## Chạy kiểm thử tích hợp PostgreSQL

Các kiểm thử tích hợp dùng PostgreSQL thật và tự tạo/xóa schema riêng theo từng test. Hãy đặt `AIRLINETICKETING_TEST_CONNECTION` trỏ tới một database kiểm thử mà tài khoản có quyền tạo schema, rồi chạy:

```powershell
$env:AIRLINETICKETING_TEST_CONNECTION = "Host=localhost;Port=5432;Database=airline_ticketing_test;Username=airline_app;Password=<mật-khẩu>"
dotnet test AirlineTicketing.sln
```

Nếu biến kết nối chưa được đặt, các kiểm thử PostgreSQL sẽ được đánh dấu bỏ qua.
