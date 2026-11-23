# Kịch bản demo AirlineTicketing (15 phút)

Kịch bản này dùng cho buổi báo cáo/bảo vệ: mỗi bước gồm **thao tác**, **kết quả mong đợi** và
**lời thoại gợi ý**. Chuẩn bị môi trường theo [checklist](DEMO-CHECKLIST.md#5-chuẩn-bị-môi-trường-trước-buổi-demo-15-phút).

## 0. Chuẩn bị (trước khi bấm giờ)

| Việc cần làm | Lệnh / thao tác |
| --- | --- |
| Khởi động stack | `docker compose up -d` (lần đầu: `--build`) |
| Kiểm tra sức khoẻ | `curl http://127.0.0.1:8080/health/database` |
| Làm sạch dữ liệu (tuỳ chọn) | `pwsh ./scripts/reset-demo.ps1` |
| Smoke test toàn hệ thống | `pwsh ./scripts/smoke.ps1` |
| Mở 4 cửa sổ ẩn danh | Khách vãng lai, `manager`, `admin`, `minhanh` |
| Cỡ chữ trình duyệt | 100% (Ctrl+0), độ phân giải ≥ 1440×900 |

Mẹo: đặt sẵn 4 tab đã đăng nhập từ trước để buổi demo không phụ thuộc giới hạn 5 lần đăng nhập/phút.

## 1. Mở đầu (1 phút)

**Lời thoại:**

> AirlineTicketing là hệ thống bán vé máy bay xây dựng bằng ASP.NET Core MVC và PostgreSQL.
> Hệ thống phục vụ ba nhóm người dùng: khách hàng đặt vé và tra cứu, nhân viên vận hành quản lý
> lịch bay/đặt chỗ/đổi hoàn, quản trị viên quản lý tài khoản/danh mục/cấu hình. Toàn bộ chạy trong
> Docker Compose, có migration, dữ liệu seed, test tích hợp và CI.

**Chiếu nhanh:** sơ đồ kiến trúc trong [README](../README.md#kiến-trúc) và badge CI xanh.

## 2. Khách vãng lai đặt vé (5 phút)

### 2.1 Tìm chuyến

1. Mở `http://127.0.0.1:8080/` (khu khách hàng).
2. Nhập `SGN` → `HAN`, để trống ngày để hệ thống chọn ngày gần nhất, chọn **1 hành khách**,
   hạng **Phổ thông**, bấm **Tìm chuyến bay**.

**Kết quả mong đợi:** danh sách chuyến `TP. Hồ Chí Minh (SGN) → Hà Nội (HAN)`; mỗi thẻ có giờ,
số hiệu, giá, số ghế còn lại; dải giá 7 ngày phía trên.

**Lời thoại:** "Hệ thống chỉ trả chuyến đang mở bán và còn đủ chỗ. Giá đã gồm thuế, phí."

3. Đổi **Sắp xếp** sang "Giá thấp nhất", sau đó "Bay nhanh nhất".
4. Mở bộ lọc, chọn khoảng giá/giờ để thấy danh sách thu hẹp.

### 2.2 Chọn ghế và nhập hành khách

5. Bấm **Chọn chuyến** trên một chuyến bất kỳ.
6. Trong sơ đồ ghế, chỉ chọn ghế **màu xanh (Trống)** — ví dụ `12A`.
7. Nhập hành khách mẫu:

| Trường | Giá trị |
| --- | --- |
| Họ | `Nguyễn` |
| Tên đệm và tên | `Văn Demo` |
| Giới tính | `Nam` |
| Ngày sinh | `01/01/1990` |
| Quốc tịch | `Việt Nam` |
| CCCD / hộ chiếu | `079090001234` |
| Email | `demo@example.com` |
| Số điện thoại | `0901234567` |

8. Chọn hành lý ký gửi (hoặc để mặc định), bấm **Tiếp tục**.

**Kết quả mong đợi:** ghế đã chọn hiển thị đúng; tổng tiền cập nhật theo ghế/hành lý.

### 2.3 Thanh toán và nhận vé

9. Ở trang thanh toán, chọn **Thẻ mô phỏng** và giữ mẫu **Visa •••• 4242**. Xác nhận điều khoản rồi bấm **Thanh toán mô phỏng**.
10. Ghi lại **mã đặt chỗ (PNR)** trên trang thành công.

**Kết quả mong đợi:** trang thành công hiển thị PNR, trạng thái vé, nút tải PDF; email vé được ghi
vào outbox (hoặc gửi SMTP nếu cấu hình).

11. Bấm **Tải vé PDF**, mở file để thấy QR, thông tin chuyến và hành khách bằng tiếng Việt.

### 2.4 Tra cứu và bảo vệ dữ liệu

12. Mở tab mới `/tra-cuu`, nhập PNR vừa tạo + email `demo@example.com`.
13. Thử lại với email sai.

**Kết quả mong đợi:** đúng thông tin thì thấy chuyến, ghế, trạng thái, số tiền; sai thì chỉ nhận
thông báo chung, không tiết lộ PNR có tồn tại hay không.

14. Ở cửa sổ ẩn danh, mở `http://127.0.0.1:8080/ve/{PNR}/pdf` khi chưa đăng nhập.

**Kết quả mong đợi:** `404` — vé chỉ tải được bởi chủ vé hoặc nhân viên có quyền.

## 3. Khách hàng có tài khoản (2 phút)

### 3.1 Vé của tôi

1. Đăng nhập `minhanh` / `123456` tại `/dang-nhap`.
2. Mở `/ve-cua-toi`.

**Kết quả mong đợi:** danh sách vé của đúng khách hàng, có tab trạng thái, chi tiết chuyến/ghế/giá.

3. Mở một vé, bấm **Tải vé PDF** và **Gửi lại email**.

**Lời thoại:** "Gửi lại email bị giới hạn 3 lần/5 phút mỗi IP để tránh lạm dụng."

### 3.2 Gửi yêu cầu đổi/hoàn

4. Trong chi tiết vé, bấm **Yêu cầu hoàn vé**.
5. Chọn lý do `Thay đổi kế hoạch cá nhân`, tick cả hai xác nhận phí/tài khoản, gửi yêu cầu.
6. Mở **Theo dõi yêu cầu đã gửi** để thấy trạng thái `Chờ duyệt`.

**Kết quả mong đợi:** yêu cầu xuất hiện với mã `RF-...`, số tiền hoàn dự kiến và lý do.

> Nếu muốn demo đổi chuyến: chọn **Yêu cầu đổi vé**, chọn ngày/chuyến mới, tick xác nhận và gửi.

## 4. Nhân viên vận hành — Manager (4 phút)

### 4.1 Tổng quan

1. Đăng nhập `manager` / `123456` → `/quan-ly`.

**Kết quả mong đợi:** KPI chuyến hôm nay, đặt chỗ chờ thanh toán, doanh thu, cảnh báo ghế sắp hết;
bảng chuyến và danh sách việc cần xử lý.

**Lời thoại:** "Dashboard lấy số liệu thật từ PostgreSQL, không phải số tĩnh."

### 4.2 Lịch bay

2. Mở `/quan-ly/lich-bay`, chọn ngày mai.
3. Thử **đổi giá** một chuyến (ví dụ +100.000 ₫), sau đó mở lại bộ lọc để thấy giá mới.
4. Thử thêm chuyến với giờ không hợp lệ để thấy thông báo lỗi được kiểm soát.

> Không cần huỷ chuyến thật trong buổi demo; nếu huỷ, chọn chuyến ít vé và nêu rõ đây là thao tác mẫu.

### 4.3 Xác nhận thanh toán chuyển khoản

5. Quay lại khu khách, đặt thêm một vé với phương thức **VietQR demo**.
6. Ở trang QR, ghi lại PNR và số tiền rồi bấm **Báo đã chuyển khoản demo**.
7. Vào `/quan-ly/thanh-toan-vnpay` bằng tài khoản Manager. Tìm PNR trong **Khoản thu**, nhập mã chứng từ mô phỏng và số tiền khớp khoản thu rồi bấm **Ghi nhận khoản thu**.

**Kết quả mong đợi:** booking chuyển `Đã xác nhận`, vé được phát hành, email vé được gửi; lịch sử
đặt chỗ ghi người xác nhận.

### 4.4 Duyệt yêu cầu hoàn

8. Mở `/quan-ly/doi-hoan`, lọc trạng thái **Chờ duyệt**, mở yêu cầu `RF-...` vừa tạo.
9. Bấm **Duyệt**, nhập lý do, tick xác nhận.

**Kết quả mong đợi:** yêu cầu chuyển `Đã duyệt`, vé/booking cập nhật theo chính sách, có lịch sử.

### 4.5 Báo cáo doanh thu

10. Mở `/quan-ly/bao-cao`, chọn kỳ tháng hiện tại, hãng `Tất cả`, tuyến `Tất cả`.
11. Bấm **Xuất báo cáo** để tải CSV, mở bằng Excel.

**Kết quả mong đợi:** KPI doanh thu gộp, tiền hoàn, doanh thu thuần; bảng theo tuyến/hãng; CSV UTF-8
có cùng số liệu với màn hình.

**Lời thoại:** "Báo cáo chỉ tính giao dịch thành công; hoàn tiền trừ khỏi doanh thu; booking chờ/huỷ
không được cộng vào tiền thực thu."

## 5. Quản trị viên — Admin (2 phút)

1. Đăng nhập `admin` / `123456` → `/admin`.

2. `/admin/tai-khoan`: mở tài khoản `nam.user`, bấm **Mở khoá** (hoặc khoá rồi mở lại).

**Kết quả mong đợi:** trạng thái đổi ngay; thử đăng nhập tài khoản bị khoá sẽ bị chặn kèm thông báo.

3. `/admin/danh-muc`: chuyển tab Sân bay / Hãng bay / Tuyến bay / Tàu bay; sửa trạng thái khai thác
   của một bản ghi.

**Lời thoại:** "Dữ liệu đang được chuyến bay tham chiếu không bị xoá cứng, chỉ ngừng khai thác."

4. `/admin/cau-hinh`: đổi `Phí dịch vụ` hoặc `Thuế`, lưu; quay lại luồng đặt vé để thấy giá áp dụng mới.

5. `/admin/nhat-ky`: lọc theo người thực hiện `admin`, xem các thao tác vừa làm.

**Kết quả mong đợi:** nhật ký có thời gian, người thực hiện, hành động, đối tượng, kết quả.

## 6. Kỹ thuật và kết luận (1 phút)

1. Terminal: `docker compose ps` — 4 container web + PostgreSQL healthy.
2. `curl http://127.0.0.1:8080/health/database` — `status: connected`.
3. `pwsh ./scripts/smoke.ps1` — kiểm tra health, 3 vai trò, CSV, PDF.
4. Mở GitHub Actions — 2 job CI xanh (build/unit test và PostgreSQL integration test).
5. Nhắc lại giới hạn demo: thanh toán mô phỏng, email outbox thay SMS, 01 hành khách/đơn,
   chưa chặn trùng lịch tàu bay — xem [mục 8](DEMO-CHECKLIST.md#8-giới-hạn-đã-biết-của-bản-demo).

**Lời thoại kết:**

> Hệ thống đã khép kín luồng từ tìm chuyến, giữ chỗ, thanh toán, phát hành vé, tra cứu, đổi/hoàn đến
> báo cáo doanh thu và quản trị. Phần mô phỏng được tách rõ để có thể thay bằng cổng thanh toán/GDS
> thật khi triển khai production.

## 7. Phương án dự phòng

| Tình huống | Phương án |
| --- | --- |
| Hết thời gian | Bỏ phần 4.2 (lịch bay) và 5.3–5.4 (danh mục/cấu hình), giữ đặt vé → PDF → báo cáo |
| Luồng đặt vé lỗi mạng | Dùng PNR dự phòng lấy từ [checklist mục 7](DEMO-CHECKLIST.md#7-dữ-liệu-dự-phòng-khi-demo) |
| Không gửi được email | Mở outbox: `docker compose cp web:/app/App_Data/outbox/. ./outbox/` |
| CSV không mở được | Mở trực tiếp bằng Excel; kiểm tra file có BOM UTF-8 (3 byte đầu `EF BB BF`) |
| Dữ liệu rối sau khi thử | `pwsh ./scripts/reset-demo.ps1` rồi chờ health xanh |

## 8. Bản rút gọn 7 phút

1. Trang chủ → tìm `SGN → HAN` → chọn chuyến → chọn ghế → nhập khách → thanh toán thẻ → tải PDF. (3')
2. `/tra-cuu` với PNR + email. (1')
3. `manager` → `/quan-ly/bao-cao` → xuất CSV. (1')
4. `admin` → `/admin/nhat-ky` xem log. (1')
5. CI + `scripts/smoke.ps1`. (1')

## 9. Câu hỏi thường gặp khi bảo vệ

| Câu hỏi | Trả lời ngắn |
| --- | --- |
| Thanh toán thật chưa? | Chưa — cổng được mô phỏng, chỉ kiểm tra định dạng và không lưu số thẻ. Luồng chuyển khoản có bước nhân viên đối soát. |
| Vì sao chưa có SMS? | Nằm ngoài phạm vi demo; hệ thống đã có email vé PDF và outbox để kiểm chứng. |
| Chống đặt trùng ghế thế nào? | Unique index `(ChuyenBayId, SoGhe)` + kiểm tra lại trong transaction khi thanh toán. |
| Chống sửa dữ liệu người khác? | Cookie auth theo vai trò, ownership check khi tải PDF/xem vé, test tích hợp phân quyền. |
| Phiên đăng nhập có mất khi rebuild? | Không — Data Protection key ring nằm trong volume `dataprotection`. |
| Báo cáo có tin được không? | Số liệu tổng hợp từ bảng thanh toán/hoàn tiền thật trong PostgreSQL; có test và xuất CSV đối chiếu. |
