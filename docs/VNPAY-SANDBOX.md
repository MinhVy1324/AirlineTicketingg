# VNPAY Sandbox

VNPAY Sandbox dùng giao thức 2.1.0. Đăng ký điểm thanh toán sandbox và lấy mã merchant cùng khóa ký từ VNPAY. [Tài liệu PAY](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html) · [Tài liệu truy vấn và hoàn tiền](https://sandbox.vnpayment.vn/apis/docs/truy-van-hoan-tien/querydr%26refund.html)

## Cấu hình

Đặt các biến sau trong tệp `.env` được Git bỏ qua khi chạy Compose hoặc trong môi trường của tiến trình web khi chạy trực tiếp. Giữ khóa ký trong môi trường triển khai.

| Biến | Giá trị |
| --- | --- |
| `Vnpay__Enabled` | `true` khi đã sẵn sàng thử với merchant |
| `Vnpay__MerchantCode` | `vnp_TmnCode` của merchant sandbox |
| `Vnpay__HashSecret` | Khóa ký của merchant sandbox |
| `Vnpay__PublicBaseUrl` | Gốc HTTPS truy cập được từ VNPAY và trình duyệt khách, ví dụ `https://sandbox.example.test` |
| `Vnpay__ServerIp` | IP máy chủ gửi yêu cầu truy vấn và hoàn tiền |

Áp dụng các migration mới nhất, bao gồm `PaymentReconciliation`, trước khi mở web. Với Compose, dịch vụ `migrate` thực hiện bước này.

Đăng ký URL IPN với VNPAY là `<Vnpay__PublicBaseUrl>/dat-ve/vnpay/ipn`. Hệ thống gửi URL quay về `<Vnpay__PublicBaseUrl>/dat-ve/vnpay/ket-qua` khi tạo giao dịch. IPN cần HTTPS công khai. Nếu dùng máy demo qua proxy, giữ khu quản lý ở mạng nội bộ hoặc hạn chế truy cập và đổi mật khẩu mẫu trước khi mở địa chỉ công khai.

## Thử giao dịch

1. Giữ chỗ, kiểm tra giá rồi chọn **VNPAY Sandbox** ở checkout. Dùng dữ liệu thử do sandbox cấp.
2. Sau khi quay về, kiểm tra trang trạng thái. Chỉ IPN đã xác minh hoặc truy vấn đã xác minh từ màn hình nhân viên mới ghi nhận thanh toán và phát hành vé.
3. Trong **Đối soát thanh toán** tại `/quan-ly/thanh-toan-vnpay`, chọn **Truy vấn khoản thu VNPAY**. Khoản thu về muộn khi đơn hết điều kiện phát hành được liên kết với lệnh hoàn toàn bộ.
4. Với vé đã phát hành, duyệt yêu cầu tại **Xử lý đổi / hoàn** rồi mở **Lệnh hoàn** để gửi số tiền đã duyệt. Phản hồi có chữ ký hợp lệ và mã thành công cuối sẽ hoàn tất lệnh.
5. Khi lệnh chờ đối soát, tra cứu trên cổng merchant theo mã lệnh và giao dịch thu gốc. Sau khi có kết quả cuối, chọn **Ghi nhận kết quả hoàn** rồi nhập mã chứng từ, số tiền và mã giao dịch hoàn do VNPAY cấp nếu đã hoàn. Các trạng thái đang xử lý vẫn được giữ chờ. Sau khi VNPAY xác nhận từ chối, nhân viên có thể lập lần gửi mới với mã mới và tra lại chứng từ của lần trước. Truy vấn khoản thu chỉ xác minh giao dịch thu gốc.

## Đối soát VietQR demo

Chọn kênh **VietQR demo** trong trang đối soát. Nhập mã trên sao kê mô phỏng và đúng số tiền của khoản thu. Đơn hợp lệ được phát hành vé. Khoản thu về sau khi đơn hết điều kiện phát hành sinh lệnh hoàn toàn bộ. Hồ sơ hoàn có thể được ghi nhận bằng chứng từ chuyển trả mô phỏng. Mỗi chứng từ chỉ được dùng một lần trong cùng kênh. Lịch sử lưu mã lệnh, số tiền, người ghi nhận và thời điểm.

Khi chưa cấu hình merchant, checkout tiếp tục dùng thẻ mô phỏng. Kiểm thử tự động dùng PostgreSQL riêng và phản hồi VNPAY giả lập có ký. Bài thử với máy chủ của cổng cần mã merchant, khóa ký và URL IPN đã đăng ký.
