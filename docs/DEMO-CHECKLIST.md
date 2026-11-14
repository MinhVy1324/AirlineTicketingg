# Checklist nghiệm thu demo và báo cáo

Tài liệu này chốt **phạm vi "đạt 100% mức demo/báo cáo"** của AirlineTicketing: mỗi yêu cầu chức năng
được map tới màn hình/route thật trong hệ thống, dữ liệu seed đi kèm và cách kiểm chứng trong buổi demo.
Dùng kèm [kịch bản demo](DEMO-SCRIPT.md) và [phân tích yêu cầu](PHAN-TICH-YEU-CAU-VA-USE-CASE.md).

## 1. Định nghĩa "đạt mức demo/báo cáo"

Một hạng mục được coi là đạt khi:

1. Có màn hình hoặc endpoint thật, thao tác được trên trình duyệt sau `docker compose up -d`.
2. Có dữ liệu seed hoặc dữ liệu tạo mới trong buổi demo để chứng minh luồng chạy.
3. Có kiểm chứng tự động tối thiểu (test tích hợp hoặc `scripts/smoke.ps1`).
4. Giới hạn của bản demo được ghi rõ, không che giấu bằng dữ liệu giả.

Không nằm trong phạm vi mức demo: cổng thanh toán thật, gửi SMS, tích hợp GDS/NDC, HA, chịu tải production.
Các phần này nằm ở mục [giới hạn đã biết](#8-giới-hạn-đã-biết-của-bản-demo).

## 2. Vai trò, tài khoản và khu vực

| Vai trò | Đăng nhập | Mật khẩu | Trang mặc định | Quyền chính |
| --- | --- | --- | --- | --- |
| Admin | `admin` | `123456` | `/admin` | Tài khoản, vai trò, danh mục, cấu hình, nhật ký |
| Manager | `manager` | `123456` | `/quan-ly` | Tổng quan, lịch bay, đặt chỗ, đổi/hoàn, báo cáo |
| Khách hàng | `minhanh` | `123456` | `/ve-cua-toi` | Vé của tôi, yêu cầu đổi/hoàn, tải PDF, gửi lại email |
| Khách vãng lai | — | — | `/` | Tìm chuyến, đặt vé, tra cứu PNR |

Tài khoản phụ để minh hoạ trạng thái khoá: `nam.user` / `123456` (bị khoá) và các manager
`manager.ha`, `manager.minh`. Toàn bộ tài khoản seed dùng mật khẩu `123456`.

## 3. Ma trận yêu cầu chức năng

Mức đáp ứng: **Đạt** = đủ để demo; **Đạt (mô phỏng)** = luồng thật, phần ngoài hệ thống được mô phỏng;
**Đạt một phần** = có chức năng nhưng giới hạn đã ghi rõ.

| Mã | Màn hình / route | Cách kiểm chứng | Mức |
| --- | --- | --- | --- |
| FR-01 Tìm chuyến, lọc/sắp xếp | `/`, `/chuyen-bay` | Nhập `SGN → HAN`, ngày `20/11/2026`, 1 khách, Phổ thông; đổi sắp xếp "Giá thấp nhất"/"Bay nhanh nhất"; xem dải giá 7 ngày | Đạt |
| FR-02 Chỉ bán chuyến mở bán, kiểm tra lại tồn chỗ/giá | `/chuyen-bay`, `/chuyen-bay/{id}/so-do-ghe`, `/dat-ve/hanh-khach` | Sơ đồ ghế đánh dấu ghế đã bán/đang giữ; chọn ghế trống; thanh toán lại kiểm tra ghế trong transaction | Đạt |
| FR-03 Người liên hệ và hành khách, xác thực | `/dat-ve/hanh-khach` | Gửi form thiếu trường bắt buộc; nhập đủ họ tên, ngày sinh, quốc tịch, CCCD/hộ chiếu, email, SĐT | Đạt một phần (01 hành khách/đơn) |
| FR-04 Mã đặt chỗ, giữ chỗ có hạn, tổng tiền | `/dat-ve/thanh-toan`, `/dat-ve/chuyen-khoan/{pnr}` | Mã PNR dạng 6 ký tự không tuần tự; trang thanh toán hiển thị hạn giữ chỗ và tổng tiền | Đạt |
| FR-05 Thanh toán, xác nhận, phát hành vé | `/dat-ve/thanh-toan`, `/dat-ve/chuyen-khoan/{pnr}`, `/dat-ve/thanh-cong` | Thẻ mô phỏng: nhập `4242 4242 4242 4242`, `12/28`, CVV `123`; chuyển khoản: khách báo đã chuyển, manager xác nhận ở `/quan-ly/dat-ve` | Đạt (mô phỏng) |
| FR-06 Tra cứu đặt chỗ, che dữ liệu | `/tra-cuu` | Nhập PNR + đúng email liên hệ; thử sai email để thấy thông báo chung, không lộ dữ liệu | Đạt |
| FR-07 Điều kiện vé, phí đổi/hoàn | `/ve-cua-toi/yeu-cau-doi`, `/ve-cua-toi/yeu-cau-hoan` | Mở yêu cầu đổi/hoàn cho PNR đã xác nhận; xem phí và số tiền dự kiến hoàn trước khi gửi | Đạt |
| FR-08 Ghi nhận yêu cầu, người thao tác, lý do | `/quan-ly/doi-hoan`, `/admin/nhat-ky` | Duyệt/từ chối yêu cầu; mở nhật ký thấy hành động, người thực hiện, kết quả | Đạt |
| FR-09 Quản lý sân bay/hãng/tuyến/máy bay | `/admin/danh-muc` | Chuyển tab Sân bay / Hãng bay / Tuyến bay / Tàu bay; sửa trạng thái khai thác thay vì xoá cứng | Đạt |
| FR-10 Lập và quản lý chuyến bay | `/quan-ly/lich-bay` | Thêm chuyến, sửa giờ/giá, đổi giá, huỷ chuyến; thử giờ không hợp lệ để thấy thông báo | Đạt một phần (chưa chặn trùng lịch máy bay) |
| FR-11 Tài khoản, vai trò, tham số, audit | `/admin/tai-khoan`, `/admin/vai-tro`, `/admin/cau-hinh`, `/admin/nhat-ky` | Tạo/khoá/mở tài khoản; xem ma trận quyền; sửa phí dịch vụ/thuế; kiểm tra nhật ký | Đạt một phần (03 vai trò cố định) |
| FR-12 Báo cáo doanh thu, xuất file | `/quan-ly/bao-cao`, `/quan-ly/bao-cao/xuat` | Lọc kỳ/hãng/tuyến; xem doanh thu gộp, hoàn tiền, doanh thu thuần; tải CSV mở bằng Excel | Đạt |
| FR-13 Thông báo xác nhận/thất bại/hết hạn | `/dat-ve/thanh-cong`, `/ve/{pnr}/pdf`, `/ve-cua-toi` | Sau thanh toán thẻ, vé PDF được gửi vào outbox; tải PDF và dùng "Gửi lại email" | Đạt một phần (email outbox, không SMS) |

## 4. Ma trận use case

| Use case | Route chính | Dữ liệu demo |
| --- | --- | --- |
| UC-01 Tra cứu chuyến | `/` → `/chuyen-bay` | Hơn 4.000 chuyến từ 24/10/2026 đến 31/12/2026, 28 tuyến (14 cặp hai chiều) |
| UC-02 Tạo đặt chỗ, giữ chỗ | `/chuyen-bay/{id}/so-do-ghe` → `/dat-ve/hanh-khach` | Ghế trống trên sơ đồ; hạn giữ chỗ theo `booking.hold_minutes` |
| UC-03 Thanh toán, phát hành vé | `/dat-ve/thanh-toan` → `/dat-ve/thanh-cong` | Thẻ mô phỏng, email liên hệ để nhận vé |
| UC-04 Tra cứu booking | `/tra-cuu` | PNR mới tạo trong buổi demo |
| UC-05 Huỷ/hoàn tiền | `/ve-cua-toi/yeu-cau-hoan` → `/quan-ly/doi-hoan` | 8 yêu cầu đổi/hoàn seed ở nhiều trạng thái |
| UC-06 Đổi chuyến | `/ve-cua-toi/yeu-cau-doi` → `/quan-ly/doi-hoan` | Vé đã xác nhận còn hiệu lực |
| UC-07 Sân bay | `/admin/danh-muc` | 12 sân bay |
| UC-08 Máy bay | `/admin/danh-muc?tab=tau-bay` | 8 tàu bay |
| UC-09 Tuyến bay | `/admin/danh-muc?tab=tuyen-bay` | 28 tuyến hai chiều |
| UC-10 Chuyến bay | `/quan-ly/lich-bay` | Lịch bay theo ngày, nhiều trạng thái |
| UC-11 Đối soát thanh toán | `/quan-ly/dat-ve` (xác nhận chuyển khoản), `/quan-ly/bao-cao` | Booking chờ thanh toán và giao dịch đã xác nhận |
| UC-12 Báo cáo doanh thu | `/quan-ly/bao-cao` | Booking nhiều trạng thái, thanh toán/hoàn tiền seed |

## 5. Chuẩn bị môi trường (trước buổi demo 15 phút)

- [ ] `docker compose ps` có `db` healthy và 4 container web `Up`.
- [ ] `http://127.0.0.1:8080/health/database` trả `status: connected`.
- [ ] `pwsh ./scripts/smoke.ps1` chạy hết xanh (health, 3 vai trò, báo cáo CSV, PDF).
- [ ] Nếu dữ liệu đã bị thao tác nhiều: `pwsh ./scripts/reset-demo.ps1` rồi chờ health xanh lại.
- [ ] Đăng xuất toàn bộ phiên cũ trên trình duyệt demo; dùng cửa sổ ẩn danh mới.
- [ ] Mở sẵn 4 tab: khách vãng lai, `manager`, `admin`, `minhanh`.
- [ ] Kiểm tra email outbox: `docker compose cp web:/app/App_Data/outbox/. ./outbox/` (nếu cần minh hoạ).
- [ ] Kiểm tra tải CSV báo cáo và PDF vé trước khi lên báo cáo (tránh lỗi font/quyền tại chỗ).
- [ ] Chuẩn bị sẵn PNR dự phòng từ dữ liệu seed (xem mục 7) trong trường hợp luồng tạo mới gặp sự cố.

## 6. Checklist nghiệm thu theo vai trò

### 6.1 Khách vãng lai

- [ ] Trang chủ hiển thị form tìm chuyến, gợi ý điểm đến và khuyến mãi.
- [ ] Tìm được chuyến `SGN → HAN` ngày mai; thấy giá, giờ, số hiệu, số ghế còn lại.
- [ ] Đổi sắp xếp và bộ lọc; danh sách cập nhật đúng.
- [ ] Mở sơ đồ ghế, ghế đã bán/đang giữ không chọn được.
- [ ] Nhập thiếu thông tin hành khách bị chặn ngay trên form.
- [ ] Chọn ghế trống, sang trang thanh toán, thấy tổng tiền và hạn giữ chỗ.
- [ ] Thanh toán thẻ mô phỏng thành công, nhận PNR và tải được vé PDF.
- [ ] Tra cứu PNR bằng đúng email liên hệ; sai email nhận thông báo chung.
- [ ] Chưa đăng nhập không tải được PDF của người khác (404).

### 6.2 Khách hàng (`minhanh`)

- [ ] `/ve-cua-toi` hiển thị danh sách vé seed của chính mình.
- [ ] Mở chi tiết vé, tải PDF, dùng "Gửi lại email".
- [ ] Gửi yêu cầu đổi chuyến và yêu cầu hoàn tiền; thấy phí/hoàn dự kiến.
- [ ] Không vào được `/quan-ly` và `/admin` (bị chuyển về trang từ chối).

### 6.3 Manager (`manager`)

- [ ] `/quan-ly` hiển thị KPI vận hành, cảnh báo và bảng chuyến hôm nay.
- [ ] `/quan-ly/lich-bay`: lọc theo ngày, thêm/sửa chuyến, đổi giá, huỷ chuyến.
- [ ] `/quan-ly/dat-ve`: tìm PNR, xác nhận thanh toán chuyển khoản, xem lịch sử.
- [ ] `/quan-ly/doi-hoan`: duyệt/từ chối yêu cầu kèm lý do; trạng thái đổi ngay.
- [ ] `/quan-ly/bao-cao`: lọc kỳ/hãng/tuyến; KPI gộp/hoàn/thuần khớp dữ liệu; xuất CSV.

### 6.4 Admin (`admin`)

- [ ] `/admin` hiển thị KPI hệ thống và cảnh báo.
- [ ] `/admin/tai-khoan`: tạo, sửa, khoá/mở tài khoản; thử đăng nhập tài khoản bị khoá.
- [ ] `/admin/vai-tro`: xem ma trận quyền 03 vai trò.
- [ ] `/admin/danh-muc`: sửa sân bay/tàu bay/tuyến; không xoá cứng dữ liệu đang tham chiếu.
- [ ] `/admin/cau-hinh`: đổi phí dịch vụ/thuế/hotline; giá trị áp dụng ở lần đặt vé tiếp theo.
- [ ] `/admin/nhat-ky`: lọc theo người thực hiện/loại thao tác, thấy đăng nhập và thao tác quản trị.

### 6.5 Phi chức năng và kỹ thuật

- [ ] `docker compose up --build -d` chạy được từ máy sạch theo README.
- [ ] Dữ liệu seed tự nạp khi CSDL trống; chạy lại không nhân đôi dữ liệu.
- [ ] Phiên đăng nhập sống qua rebuild container (volume `dataprotection`).
- [ ] CI xanh: build + unit test + test tích hợp PostgreSQL.
- [ ] Trang lỗi không lộ stack trace; route quản trị yêu cầu đúng vai trò.
- [ ] Giao diện tiếng Việt, dùng được ở độ rộng 390 px (điện thoại) và 1440 px (máy chiếu).

## 7. Dữ liệu dự phòng khi demo

Không hardcode PNR vào tài liệu vì dữ liệu có thể reset. Lấy nhanh một PNR đã xác nhận:

```powershell
'select "MaDatVe" from "DatVes" where "TrangThai" = ''DaXacNhan'' order by "NgayDat" desc limit 3;' |
  docker compose exec -T db psql -U airline_app -d airline_ticketing -t -A
```

Lấy PNR thuộc tài khoản `minhanh`:

```powershell
'select d."MaDatVe" from "DatVes" d join "KhachHangs" k on k."Id" = d."KhachHangId" where k."Email" = ''minhanh@example.com'' and d."TrangThai" = ''DaXacNhan'' limit 3;' |
  docker compose exec -T db psql -U airline_app -d airline_ticketing -t -A
```

Email liên hệ của PNR (dùng cho `/tra-cuu`):

```powershell
'select d."MaDatVe", k."Email" from "DatVes" d join "KhachHangs" k on k."Id" = d."KhachHangId" where d."TrangThai" = ''DaXacNhan'' order by d."NgayDat" desc limit 3;' |
  docker compose exec -T db psql -U airline_app -d airline_ticketing -t -A
```

## 8. Giới hạn đã biết của bản demo

Các giới hạn dưới đây là **chủ đích của phạm vi demo**, cần nói rõ khi báo cáo:

1. **Thanh toán mô phỏng.** Không kết nối cổng thật; thẻ chỉ được kiểm tra định dạng, không lưu số thẻ.
   Chuyển khoản dùng QR VietQR minh hoạ và nhân viên xác nhận thủ công, hệ thống không tự xác nhận.
2. **Một hành khách mỗi đơn.** Form đặt vé phục vụ 01 hành khách/đơn; đặt nhóm thực hiện nhiều đơn liên tiếp.
   Bộ lọc tìm chuyến vẫn nhận số khách để kiểm tra tồn chỗ.
3. **Email thay cho SMS.** Vé điện tử gửi qua SMTP nếu cấu hình, mặc định ghi `.eml` vào `App_Data/outbox`.
   Không có SMS, push hay tích hợp GDS/NDC.
4. **Báo cáo theo ngày phát hành.** Báo cáo dòng tiền dùng ngày giao dịch/ngày phát hành vé, không phải ngày bay;
   chưa có báo cáo riêng theo ngày bay.
5. **Phân quyền cố định 03 vai trò.** Ma trận quyền được hiển thị và ghi nhật ký, chưa hỗ trợ tạo vai trò động.
6. **Lịch máy bay chưa chặn trùng giờ.** Tạo/sửa chuyến kiểm tra dữ liệu, thời gian và trạng thái;
   chưa kiểm tra xung đột sử dụng cùng một tàu bay.
7. **Không có HA, hàng đợi phân tán hay chịu tải production.** Phù hợp demo nội bộ và báo cáo môn học.

## 9. Xử lý sự cố nhanh trong buổi demo

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
| --- | --- | --- |
| Trang trắng hoặc 502 | Container web chưa healthy | `docker compose ps`, `docker compose logs --tail=80 web` |
| Không đăng nhập được | Đã hết hạn mức 5 lần/phút theo IP | Chờ 60 giây hoặc dùng `127.0.0.1:8080` thay vì đổi IP |
| Không có chuyến để demo | Lọc sai ngày/sân bay | Dùng `SGN → HAN`, ngày trong khoảng 16/11–31/12/2026 |
| Không tải được PDF | Sai quyền hoặc PNR chưa xác nhận | Dùng PNR `DaXacNhan`; đăng nhập manager/admin hoặc đúng chủ vé |
| CSV mở bị lỗi font | Mở bằng trình soạn thảo không đọc UTF-8 | Mở bằng Excel; file có BOM UTF-8 |
| Dữ liệu lộn xộn sau khi thử | Nhiều booking/ghế đã thao tác | Chạy `pwsh ./scripts/reset-demo.ps1` trước buổi demo |

## 10. Liên kết

- [Kịch bản demo 15 phút](DEMO-SCRIPT.md)
- [README tổng quan](../README.md)
- [Cài đặt và tài khoản demo](INSTALL.md)
- [Docker Compose và vận hành](DOCKER-SETUP.md)
- [Phân tích yêu cầu, use case và traceability](PHAN-TICH-YEU-CAU-VA-USE-CASE.md)
