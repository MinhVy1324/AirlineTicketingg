# Lộ trình nâng cấp đồ án AirlineTicketing

Phạm vi được chọn: hoàn thiện đồ án/demo trước, sau đó tích hợp cổng thanh toán sandbox.
Ngày cập nhật: 20/11/2026.

## Các bước

| Bước | Nội dung | Trạng thái |
| --- | --- | --- |
| 1 | Dữ liệu hành khách, giá, hành lý, giữ ghế, thanh toán lặp, quyền sở hữu đặt chỗ, thống nhất đường đặt/đổi/hủy | Đã triển khai; có kiểm thử hồi quy HTTP và PostgreSQL |
| 2 | Bộ lọc hãng/giờ/giá có xử lý thật; giữ bộ lọc khi đổi ngày/sắp xếp; số lượng theo dữ liệu | Đã triển khai; có kiểm thử HTTP với PostgreSQL |
| 3 | Khứ hồi; đặt nhiều hành khách; phân loại người lớn/trẻ em/em bé; hạng vé được truyền xuyên suốt | Đã triển khai cho demo; có migration và kiểm thử PostgreSQL |
| 4 | Đăng ký và khôi phục mật khẩu, các trang điều kiện, bảo mật, đổi/hoàn và mã ưu đãi dùng được | Đã triển khai cho demo và kiểm thử với PostgreSQL |
| 5 | VNPAY Sandbox, xác minh chữ ký và số tiền, IPN chống trùng, đối soát và hoàn tiền | Đã triển khai và kiểm thử nội bộ. Cần cấu hình merchant và địa chỉ HTTPS để thử với VNPAY |
| 6 | Điện thoại, bàn phím, khả năng tiếp cận, gửi lại email, kiểm thử tải và diễn tập khôi phục | Đã triển khai và kiểm chứng cục bộ |
| 7 | Hoàn tất đổi và hoàn vé theo yêu cầu của khách | Đã triển khai cho thanh toán mô phỏng |
| 8 | Xử lý đặt chỗ bị ảnh hưởng khi hãng hủy hoặc đổi lịch bay | Đã triển khai cho demo |
| 9 | Lọc vé và yêu cầu đổi/hoàn, điều hướng bàn phím và lịch demo theo ngày chạy | Đã triển khai cho demo |

## Bước 1: hành vi sau thay đổi

- Máy chủ kiểm tra họ/tên, ngày sinh, độ tuổi, quốc tịch, giấy tờ, email và điện thoại. Bước 1 ban đầu phục vụ một hành khách từ 12 tuổi; bước 3 mở rộng theo nhóm tuổi.
- Không tự điền hồ sơ cá nhân mẫu. Khi sai dữ liệu, biểu mẫu giữ lại thông tin và chỉ rõ trường cần sửa.
- Biểu mẫu chỉ gửi mã gói hành lý `none`, `extra10`, `extra20`. Giá và số kg được tra tại `BaggageCatalog`; không nhận số tiền hành lý từ trình duyệt.
- Giá kết quả tìm kiếm và lịch giá theo ngày bao gồm thuế/phí bắt buộc. Phụ thu ghế và hành lý là lựa chọn riêng.
- Sau khi xác nhận thông tin hợp lệ, hệ thống tạo đặt chỗ chờ thanh toán và vé tạm giữ trong transaction. Các phiên khác thấy ghế đang giữ ngay tại bước thanh toán.
- Giá và hạn giữ được lưu trong đặt chỗ. Tải lại trang hoặc gửi lại cùng biểu mẫu không gia hạn hay tạo đơn mới. Đổi ghế/gói dịch vụ thay thế chỗ cũ trong cùng transaction và giữ nguyên hạn còn lại.
- Thanh toán dùng lại đặt chỗ đã giữ. Gửi lặp không tạo thêm giao dịch hay phát hành thêm vé. Đơn quá hạn bị chặn; job nền trả ghế.
- Thanh toán thẻ hiện là **mô phỏng nội bộ**, chỉ nhận `4242424242424242` hoặc `5555555555554444`. Chưa có kết nối VNPAY/MoMo sandbox và không dùng thẻ thật.
- Chuyển khoản vẫn chờ nhân viên xác nhận. Khách báo đã chuyển không tự phát hành vé.
- Xem xác nhận, QR chuyển khoản, tải PDF và báo chuyển khoản yêu cầu phiên đặt chỗ hoặc tài khoản có quyền; biết PNR không đủ để xem đơn.
- Không sửa hồ sơ hành khách đã tồn tại khi đơn mới gửi cùng giấy tờ nhưng khác họ tên/ngày sinh.
- URL tạo đặt chỗ cũ chuyển về biểu mẫu hiện tại; URL đổi/hủy cũ chuyển sang quy trình yêu cầu. Phần nghiệp vụ đổi giá vé giữ nguyên thuế/phí/dịch vụ đã mua.
- Yêu cầu đổi/hoàn cần vé đã xác nhận và gửi trước giờ khởi hành hơn 3 tiếng, thống nhất với điều kiện trên giao diện.

Không thay đổi cấu trúc bảng; bước này không cần migration mới. Mã đặt chỗ mới gồm 8 ký tự ngẫu nhiên; mã cũ vẫn tra cứu được.

## Bước 2: tìm chuyến và bộ lọc

- Chọn nhiều hãng và nhiều khung giờ: các lựa chọn trong cùng nhóm kết hợp theo “hoặc”; hãng, giờ và khoảng giá kết hợp theo “và”. Không chọn trong một nhóm nghĩa là không giới hạn nhóm đó.
- Tên/mã hãng lấy từ danh mục hãng bay gắn với chuyến, không suy đoán bằng tiền tố số hiệu.
- Bốn khung giờ theo UTC+7 phủ đủ ngày: 00:00–04:59, 05:00–11:59, 12:00–17:59, 18:00–23:59.
- Giá từ/đến là tổng tiền mỗi khách đã gồm thuế và phí bắt buộc, chưa gồm dịch vụ tùy chọn; bao gồm cả hai đầu mút. Để trống là không giới hạn; giá 0 vẫn là một giới hạn thật.
- Máy chủ từ chối khoảng giá đảo ngược, số âm, giá sai định dạng/vượt giới hạn và mã khung giờ/hãng không hợp lệ; không âm thầm trả kết quả ngoài bộ lọc.
- Số chuyến cạnh mỗi hãng/khung giờ tính theo các điều kiện khác đang chọn. Tiêu đề hiển thị số chuyến sau lọc so với số chuyến còn chỗ trên tuyến/ngày/số khách hiện tại.
- Lịch giá 7 ngày áp dụng cùng bộ lọc và số hành khách, thay vì luôn lấy giá cho một người. Ngày không có chuyến khớp bộ lọc không hiển thị giá giả.
- Giữ toàn bộ lựa chọn trong liên kết chọn ngày, tuần trước/sau và biểu mẫu sắp xếp. URL hỗ trợ nhiều giá trị `airlines` và `times`, nên có thể tải lại hoặc chia sẻ.
- “Đặt lại” và “Bỏ tất cả bộ lọc” chỉ xóa hãng/khung giờ/khoảng giá, giữ nguyên tuyến, ngày, số khách, hạng vé và cách sắp xếp.
- Khi chuyển sang ngày không có hãng đã chọn, lựa chọn đó vẫn được hiển thị với số chuyến 0 để khách có thể bỏ chọn.
- Có nút áp dụng dùng được khi tắt JavaScript. Danh sách được hiển thị đầy đủ khi không có JavaScript; có JavaScript thì dùng nút xem thêm. Trên màn hình nhỏ, bộ lọc thu gọn và tự mở khi có lỗi nhập liệu.

Ví dụ URL: `/chuyen-bay?from=SGN&to=HAN&depart=21/11/2026&passengers=1&airlines=VN&airlines=VJ&times=morning&maxPrice=3000000&sort=price`.

Bước 2 chỉ bổ sung hợp đồng tìm kiếm, không đổi cấu trúc bảng và không cần migration mới. Khứ hồi, đặt nhóm và hạng vé vẫn thuộc bước 3.

## Bước 3: khứ hồi, nhóm hành khách và hạng vé

- Tìm một chiều hoặc khứ hồi. Chọn chiều đi rồi chọn chiều về trên tuyến ngược, ngày về không trước ngày đi và giờ về sau khi chiều đi hạ cánh. Giữ ngày, nhóm khách, hạng vé và bộ lọc khi chuyển bước/sắp xếp.
- Một mã đặt chỗ cho 1–9 người, gồm ít nhất một người lớn; số em bé không vượt số người lớn. Tồn chỗ chỉ tính người lớn và trẻ em có ghế.
- Tuổi tính theo từng ngày bay: người lớn từ 12 tuổi; trẻ em từ 2 đến dưới 12; em bé từ 7 ngày đến dưới 2 tuổi. Khi sinh nhật làm thay đổi nhóm tuổi giữa hai chiều, cần chọn lại nhóm khách phù hợp; không tự đổi giá hoặc cấp ghế.
- Mỗi em bé đi cùng một người lớn riêng từ 18 tuổi và không chiếm ghế. Nhóm có trẻ em cần người lớn từ 18 tuổi. Người lớn bắt buộc giấy tờ; trẻ em/em bé có thể để trống giấy tờ trong demo.
- Giá cơ bản demo: Phổ thông ×1; Thương gia ×1,8; trẻ em 75%; em bé 10%. Thuế/phí dịch vụ áp dụng mỗi khách có ghế, mỗi chặng. Em bé không mua hành lý/ghế riêng. Đây là quy tắc demo, không phải biểu giá của hãng bay.
- **Thương gia hiện là hạng giá dùng chung sơ đồ ghế**, chưa có khoang/tồn chỗ riêng theo cấu hình tàu bay. Hạng vé được lưu đến vé điện tử.
- Chọn ghế và hành lý riêng cho mỗi người trên mỗi chặng; liên hệ nhận vé dùng chung. Giá dịch vụ tra trên máy chủ, không nhận số tiền từ biểu mẫu.
- Giữ cả nhóm và hai chiều trong một transaction: một ghế không hợp lệ thì không tạo đặt chỗ dở dang. Gửi lại biểu mẫu giữ nguyên đơn/hạn giữ; thay đổi đơn thay thế cả nhóm với hạn còn lại.
- Thanh toán một lần cho cả hành trình. Gửi lặp không tạo thêm giao dịch; mỗi người có mã vé riêng ở mỗi chặng. Nhiều em bé không bị tranh chấp chỉ mục ghế trống.
- Trang kiểm tra, xác nhận, Vé của tôi, tra cứu, chuyển khoản, PDF và nội dung email thể hiện đủ các chặng/hành khách. Tra cứu công khai vẫn che thông tin cá nhân.
- Báo cáo tiền thu/hoàn phân bổ theo giá lưu ở từng chặng, giữ nguyên tổng sau làm tròn; không dồn cả vé khứ hồi vào tuyến đầu.
- Đổi trực tuyến đơn nhóm/khứ hồi/Thương gia chưa nằm trong bước này: giao diện yêu cầu liên hệ hỗ trợ. Yêu cầu hoàn demo áp dụng **toàn bộ đặt chỗ**, trừ một phí hoàn/đặt chỗ và phí dịch vụ/hành lý không hoàn; chưa hỗ trợ tách khách/tách chặng để đổi hoặc hoàn riêng.
- Thanh toán vẫn là mô phỏng nội bộ; cổng sandbox bên ngoài thuộc bước 5.

Migration mới: 20261120092414_JourneyBookings bổ sung loại khách, người đi cùng và thành phần giá ở từng vé, tên liên hệ, đồng thời loại ghế trống của em bé khỏi chỉ mục ghế đang sử dụng. Migration giữ nguyên dữ liệu đặt chỗ và vé cũ. Đã sao lưu database demo trước khi áp dụng.

Kiểm chứng bước 3: **113/113 test đạt, không bỏ qua**, gồm PostgreSQL thật. Có ca khứ hồi 3 người/6 vé, giá Thương gia và nhóm tuổi, giữ chỗ/giao dịch lặp, rollback khi ghế chiều về trùng, độ tuổi thay đổi ở ngày về, người đi cùng chưa đủ tuổi, một chiều nhiều người, một người Thương gia, sửa dịch vụ/ghế em bé, tuyến sai, PDF và quyền sở hữu. Kiểm tra trực tiếp trên trình duyệt đã tạo đơn demo 6TXAKVR6: 3 người, 2 chặng, 6 vé, tổng 14.454.250 ₫; thanh toán mô phỏng. Biểu mẫu, kiểm tra thanh toán và xác nhận không tràn ngang tại viewport 390 px.

## Bước 4: tài khoản, chính sách và ưu đãi

- Đăng ký khách hàng bằng email. Khách mở liên kết xác nhận rồi mới đặt mật khẩu và tạo tài khoản, sau đó đăng nhập bằng email để xem các đặt chỗ cùng địa chỉ.
- Khôi phục mật khẩu cho tài khoản khách hàng đang hoạt động. Liên kết có hiệu lực 30 phút và bị vô hiệu ngay khi mật khẩu đổi. Các phiên đăng nhập cũ cũng hết hiệu lực sau khi đặt lại. Tài khoản nhân viên được quản trị viên quản lý.
- Các liên kết đăng ký và khôi phục được gửi qua cấu hình email hiện tại. Khi dùng hộp thư demo, quản trị viên buổi trình diễn mở tệp .eml trong App_Data/outbox. Cấu hình Auth__PublicBaseUrl theo địa chỉ web truy cập từ email nếu không dùng địa chỉ mặc định http://127.0.0.1:8080.
- Điều kiện vận chuyển, bảo mật và đổi/hoàn có địa chỉ riêng. Nội dung mô tả quy tắc hiện chạy trong demo. Phí đổi/hoàn hiện hành lấy từ cấu hình nghiệp vụ.
- AERONEW áp dụng ở bước kiểm tra và thanh toán cho đơn từ 3.000.000 ₫ trước ưu đãi. Giảm tối đa 100.000 ₫, giới hạn bằng phí dịch vụ. Thời hạn mặc định là hết 31/12/2026 theo giờ Việt Nam, có thể chỉnh bằng Promotion__EndsAt.
- Mỗi đặt chỗ chỉ áp dụng một lần. Có thể bỏ mã trước khi tạo giao dịch. Nếu chỉnh sửa hành khách hoặc dịch vụ làm sinh đặt chỗ mới, cần áp dụng lại. Khi tổng tiền đổi, hệ thống yêu cầu kiểm tra lại trước khi thanh toán.
- Khi yêu cầu hoàn, phần phí dịch vụ không hoàn là khoản thực trả sau ưu đãi. Migration 20261120131529_BookingPromotions lưu mã và số tiền giảm vào đặt chỗ.

Đã chạy 116/116 kiểm thử thành công với PostgreSQL. Kiểm thử xuyên suốt đọc liên kết từ hộp thư demo, đăng ký, đăng nhập, đặt lại mật khẩu và kiểm tra phiên cũ hết hiệu lực. Các ca thanh toán kiểm tra ngưỡng ưu đãi, gửi lặp, bỏ mã, tổng tiền cũ và thanh toán đơn khứ hồi đã giảm giá.

## Bước 5: VNPAY Sandbox

- Checkout dùng tổng tiền đã chốt, kể cả ưu đãi, để tạo giao dịch VNPAY. Giao dịch đang chờ dùng lại mã tham chiếu khi khách thử tiếp.
- IPN và tác vụ truy vấn của nhân viên chỉ ghi nhận sau khi xác minh chữ ký, điểm thanh toán, mã giao dịch và số tiền. Trang khách quay về chỉ hiển thị trạng thái từ hệ thống.
- Tổng tiền sai và thông báo trùng được xử lý trước khi phát hành. Khoản đã thu khi đơn hết hạn hoặc đã có giao dịch khác được đưa vào danh sách hoàn toàn bộ.
- Nhân viên có thể truy vấn giao dịch và gửi lệnh hoàn được duyệt. Lệnh đã gửi chờ tra soát nếu cổng chưa trả kết quả xác minh được.
- Migration `20261120141539_VnpaySandbox` thêm thông tin đối soát và mã yêu cầu hoàn. Cách bật và thử với merchant nằm trong [hướng dẫn VNPAY Sandbox](VNPAY-SANDBOX.md).

Đã đạt 120/120 kiểm thử PostgreSQL. Các ca VNPAY kiểm tra tiền và chữ ký, xác nhận lặp, phản hồi đến muộn sau khi hết giữ chỗ hoặc sau khi đã thử lại, truy vấn của nhân viên, hoàn một phần và ngăn gửi lại lệnh hoàn với kết quả chưa xác minh.

## Bước 6: khả năng tiếp cận và vận hành

- Menu khách hàng hiện trên màn hình nhỏ. Có liên kết bỏ qua điều hướng, tiêu điểm rõ hơn, thông báo lỗi có thể điều hướng đến trường cần sửa, và sơ đồ ghế hỗ trợ phím mũi tên rồi Enter hoặc phím cách. Menu quản lý hỗ trợ Enter và Escape. Đã chỉnh tương phản và nối dòng dài ở trang thanh toán.
- Email vé được xếp hàng cùng giao dịch phát hành vé. Thư đăng ký và đặt lại mật khẩu cũng được xếp hàng, với nội dung mã hóa khi còn chờ. Worker nhận việc trong PostgreSQL và thử lại sau 1, 5 và 15 phút khi gặp lỗi. Liên kết đã hết hạn bị loại. Khách vẫn tải PDF ngay và có thể yêu cầu gửi lại vé.
- Migration `20261120144551_EmailDeliveryQueue` tạo bảng theo dõi lịch gửi, lần thử, hạn và kết quả.
- Đã chạy 122/122 kiểm thử PostgreSQL. Các ca mới kiểm tra hai worker cùng nhận việc, lỗi tạm thời và lần thử lại, loại thư hết hạn và IPN lặp chỉ xếp một email vé.
- Kiểm tra 27 lượt trang bằng Edge và axe ở các chiều rộng 320, 390 và 1280 px. Các lượt đã chọn không tràn ngang toàn trang và không có lỗi axe trong nhóm quy tắc WCAG được chạy. Đã thao tác bằng bàn phím với menu, tìm khứ hồi, mở bộ lọc, chọn ghế và thanh toán mô phỏng. Đây là phạm vi kiểm tra các luồng chính. [WCAG 2.2](https://www.w3.org/TR/WCAG22/)
- Đo 640 yêu cầu HTTP đọc trên localhost. Lượt 240 yêu cầu với 20 đồng thời đạt p95 206 ms. Lượt 400 yêu cầu với 40 đồng thời đạt p95 142 ms. Cả hai lượt đều 0 lỗi. Số đo dùng cho môi trường demo với dữ liệu hiện tại.
- Sao lưu PostgreSQL rồi khôi phục sang database mới trong 0,36 giây ở bước `pg_restore`. Đối chiếu đúng 39 đặt chỗ, 44 vé, 27 giao dịch và 12 tài khoản. Bản web dùng dữ liệu đã khôi phục đặt thử thành công và tải được PDF.

Xem cách chạy lại tại [Hướng dẫn vận hành](OPERATIONS.md).

## Bước 7: hoàn tất đổi và hoàn vé trong demo

- Đặt vé Phổ thông một chiều cho một người lớn và dùng thẻ mô phỏng. Trong **Vé của tôi**, chọn một chuyến thay thế cùng tuyến. Nhân viên mở **Đổi / hoàn vé** và duyệt để chốt phí. Khách mở yêu cầu và xác nhận khoản thanh toán mô phỏng.
- Khi xác nhận, hệ thống kiểm tra giờ bay và ghế tiêu chuẩn, chuyển ghế, giữ mã đặt chỗ, phát hành số vé mới và xếp hàng gửi email cập nhật. Có thể tải PDF mới ngay. Nếu chuyến mới không còn phù hợp, khách có thể hủy yêu cầu và gửi lại với chuyến khác.
- Chuyến đắt hơn thu thêm phần chênh lệch và phí đổi. Chuyến rẻ hơn thu phí đổi và giữ nguyên giá trị vé đã ghi nhận. Phí đổi đã thu không được hoàn.
- Với đơn đã thanh toán bằng thẻ mô phỏng, khách gửi yêu cầu hoàn, nhân viên duyệt số tiền rồi chọn **Hoàn tất hoàn vé demo**. Hệ thống hoàn toàn bộ đặt chỗ, giải phóng ghế mọi chặng và cập nhật lịch sử. Nếu trước đó có thu thêm khi đổi chuyến, tiền hoàn được phân bổ theo từng giao dịch.
- Đơn nhóm, khứ hồi và Thương gia cần hỗ trợ để đổi chuyến. VNPAY Sandbox dùng màn hình đối soát để hoàn. Dữ liệu seed từ trước có thể minh họa danh sách yêu cầu. Dùng đặt chỗ tạo mới bằng thanh toán mô phỏng để trình diễn đầy đủ.
- Migration `20261120151624_DemoAfterSales` lưu báo giá, chuyến gốc tại thời điểm duyệt, phí đổi đã thu và cho phép liên kết nhiều khoản hoàn của cùng yêu cầu.

Đã chạy 123/123 kiểm thử PostgreSQL. Các ca kiểm tra việc đổi rồi hoàn cùng một đơn, từ chối xác nhận sai giá hoặc sai chủ vé, giữ nguyên vé nếu chuyến thay thế bị hủy, xử lý yêu cầu lặp, hoàn cả hai chặng của một gia đình và kiểm tra lệnh VNPAY vẫn chờ xác nhận từ cổng. Đã áp dụng migration trên dữ liệu demo sau khi sao lưu và đối chiếu số đặt chỗ, vé, giao dịch và yêu cầu.

## Bước 8: chuyến bị hủy hoặc đổi lịch

- Khi nhân viên hủy hoặc đổi lịch chuyến chưa khởi hành, hệ thống ghi lịch cũ và lịch mới trên từng đặt chỗ bị ảnh hưởng. Đơn chưa xác nhận được giải phóng toàn bộ ghế. VNPAY nhận tiền đến muộn đi qua luồng đối soát hiện hành.
- Khách xem chặng bị ảnh hưởng trong **Vé của tôi**. Trang tra cứu đã xác thực và trang thanh toán thành công cũng hiện cảnh báo. PDF và gửi lại vé tạm dừng đến khi xử lý xong các chặng bị ảnh hưởng. Email được xếp hàng trong cùng giao dịch với cập nhật lịch.
- Khách có thể chấp nhận lịch mới nếu thứ tự và thời gian các chặng vẫn hợp lệ. Lịch cập nhật lần nữa thay thế thông báo cũ, nên nút xác nhận của lần cũ hết hiệu lực.
- Nhân viên mở **Biến động chuyến bay**, thống nhất với khách rồi chọn chuyến thay thế cùng tuyến. Hệ thống phân bổ đủ ghế cho cả nhóm ở chặng đó, giữ mã PNR, giá đã thu và vé các chặng còn lại. Phí đổi trong trường hợp này là 0 ₫.
- Nhân viên có thể hoàn toàn bộ đơn thẻ mô phỏng chưa sử dụng chặng nào. Các trường hợp đã sử dụng một phần hành trình hoặc cần xử lý giao dịch ngoài mô phỏng chuyển sang đối soát thủ công. Hộp thư demo ghi thông báo vào outbox.
- Migration `20261120161536_FlightDisruptions` tạo bảng biến động theo đặt chỗ và chặng. Thông báo tự động áp dụng khi lịch được cập nhật qua chức năng quản lý sau migration.

Đã qua 125/125 kiểm thử PostgreSQL. Đã thử hủy và chuyển chuyến bằng tài khoản khách và manager trên Edge với cơ sở dữ liệu riêng, xác minh thông báo trong outbox và tải PDF sau khi cấp vé mới. Đã sao lưu và cập nhật cơ sở dữ liệu demo.

## Bước 9: hoàn thiện trải nghiệm và lịch demo

- **Vé của tôi** tìm trong các đặt chỗ thuộc tài khoản theo mã PNR, mã sân bay hoặc tên điểm đến. Bộ lọc ngày tách hành trình còn chặng sắp bay và hành trình đã qua.
- **Theo dõi yêu cầu** có bộ lọc theo trạng thái, số lượng trên toàn bộ dữ liệu của tài khoản và 30 yêu cầu mỗi trang. Liên kết đến yêu cầu cũ vẫn mở đúng chi tiết. Khi mục đang chọn trống, trang hiện trạng thái trống.
- Menu tài khoản dùng tên đang hiển thị làm tên liên kết. Các vùng cuộn trong báo cáo và danh sách yêu cầu có thể lấy tiêu điểm bằng phím Tab. Lịch bay của quản lý mặc định mở ngày hiện tại theo giờ Việt Nam.
- Lịch demo bổ sung các ngày thiếu trong cửa sổ 45 ngày tính từ ngày hiện tại ở Việt Nam. Tác vụ nền chạy khi khởi động và kiểm tra lại mỗi 12 giờ ở web khách hàng hoặc web tổng hợp. Các ngày đã có lịch được giữ nguyên, kể cả chuyến đã hủy. Chuyến mới được kiểm tra trùng giờ sử dụng máy bay.
- Chế độ demo bật mặc định trong Development. Dùng `Demo__Enabled=false` để tắt tạo dữ liệu mẫu và lịch tự động. Bước này không cần thay đổi schema.

Đã qua 128/128 kiểm thử PostgreSQL. Trên Edge, 15 lượt kiểm tra ở 320, 390 và 1280 px không tràn ngang toàn trang và không phát hiện lỗi trong nhóm quy tắc axe đã chạy. Kiểm tra được lọc bằng bàn phím và cuộn biểu đồ, bảng báo cáo bằng phím mũi tên. Lần cập nhật demo đã thêm 192 chuyến đến ngày 04/01/2027 theo giờ Việt Nam. Chạy lại không sinh trùng chuyến.

## Kiểm chứng các bước trước

Kết quả sau bước 2, ngày 20/11/2026: **89/89 test thành công, không có test bị bỏ qua**, gồm PostgreSQL thật và các kiểm thử bước 1. Bản web đã được khởi động lại. Đã kiểm tra trực tiếp trên trình duyệt: chọn nhiều hãng/khung giờ, áp dụng giới hạn giá, sắp xếp và đổi ngày giữ nguyên lựa chọn; bộ lọc mở/đóng được ở chiều rộng 390 px và không tràn ngang trang.

Chạy bộ test với `AIRLINE_TEST_CONNECTION` trỏ tới PostgreSQL **riêng cho kiểm thử**, có chữ `test` trong tên database:

```powershell
dotnet test AirlineTicketing.Tests/AirlineTicketing.Tests.csproj
```

`BookingFlowRegressionTests` kiểm tra dữ liệu trống, sửa phí hành lý, giữ ghế, tranh chấp đồng thời, chốt giá, chỉnh sửa đơn, hết hạn, thẻ thử, thanh toán lặp, chuyển khoản và quyền sở hữu ở tầng HTTP với PostgreSQL thật. `DatVeBUSTests` kiểm tra không làm mất thuế/phí khi đổi vé; `TicketRulesTests` kiểm tra ranh giới thời hạn đổi/hoàn.

`FlightFilteringHttpTests` kiểm tra lọc kết hợp, tên hãng thực tế, số đếm, sắp xếp, giữ URL/ngày/tuần, giá lịch theo số khách, ngày trống và dữ liệu lọc sai. `FlightFiltersTests` kiểm tra ranh giới giờ UTC+7 và giá không hợp lệ; `FlightSearchUrlTests` kiểm tra nhiều lựa chọn không bị mất khi đổi ngày hoặc đặt lại.

Nếu không cấu hình database kiểm thử, các ca PostgreSQL được bỏ qua; đó không phải xác nhận đã kiểm chứng toàn bộ luồng.
