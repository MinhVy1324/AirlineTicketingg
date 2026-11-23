# Kiểm tra và vận hành bản demo

## Email vé và tài khoản

Email được xếp vào PostgreSQL. Tác vụ nền kiểm tra hàng đợi mỗi 5 giây theo cấu hình mặc định. Sau lỗi tạm thời, khoảng chờ giữa các lần thử là 1, 5 rồi 15 phút, tối đa 4 lượt gửi. Thư chứa liên kết được mã hóa ở trạng thái chờ và bị hủy khi quá hạn. Yêu cầu gửi lại vé đang chờ được gộp vào lệnh hiện có.

- Dùng `Email__Enabled=false` để ghi thư `.eml` vào `App_Data/outbox`. Chờ thư xuất hiện rồi mở liên kết hoặc PDF đính kèm.
- Khi dùng SMTP, trạng thái thành công cho biết máy chủ thư đã chấp nhận thư. Nếu kết nối đứt ngay sau khi máy chủ đã nhận nhưng trước khi ứng dụng ghi trạng thái, lần thử lại có thể tạo thư trùng. Mỗi lệnh có một Message-ID ổn định để đối chiếu.
- `EmailDeliveryJobs` lưu lần thử, hạn và trạng thái. Đặt `Email__RunWorker=false` cho bản phục hồi thử để tránh gửi lại thư của dữ liệu cũ.

## Kiểm thử tải cục bộ

Chọn tuyến và ngày còn chuyến. Script chạy các yêu cầu đọc đến trang chủ, tìm chuyến, tra cứu và kiểm tra kết nối DB. Nó trả mã lỗi nếu có yêu cầu thất bại hoặc kết quả tìm kiếm không hợp lệ.

```powershell
node ./scripts/load-smoke.mjs http://127.0.0.1:8080 '/chuyen-bay?from=SGN&to=HAN&depart=2026-11-22' 240 20
```

Hai số cuối là số yêu cầu và số yêu cầu đồng thời. Dùng ngày đang còn mở bán trong dữ liệu demo của bạn.

## Sao lưu và diễn tập khôi phục PostgreSQL trên Windows

Chạy với `pg_dump` và `pg_restore` của PostgreSQL. Script đọc cấu hình từ `.env` và ghi file dump vào thư mục được chỉ định.

```powershell
./scripts/backup-postgres.ps1 -OutputDirectory 'D:\AirlineBackups' -RestoreDrill
```

Bỏ `-RestoreDrill` khi chỉ cần sao lưu. Diễn tập tạo một database riêng có tên bắt đầu bằng `airline_restore_` và đối chiếu số bản ghi. Thực hiện vào lúc hệ thống yên tải để số liệu không đổi trong lúc so sánh.

Để thử bản đã phục hồi, mở một tiến trình web nội bộ ở cổng khác và trỏ kết nối đến database có tên trong kết quả. Tắt worker email và VNPAY cho tiến trình diễn tập. Kiểm tra `/health/database`, đăng nhập với tài khoản demo, tìm lịch bay rồi đặt vé và tải PDF.

File dump chứa dữ liệu khách hàng nên để ở thư mục hạn chế truy cập. Sao lưu riêng thư mục khóa Data Protection (mặc định `App_Data/keys`) và cấu hình triển khai trong kho bảo mật của bạn. Cần khôi phục đúng bộ khóa để đọc thư đang chờ và duy trì các cookie cùng liên kết đã cấp.
