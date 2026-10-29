# Hướng dẫn làm việc nhóm và chạy dự án

Tài liệu này thống nhất cách chia việc, quản lý mã nguồn, migration và môi trường Docker để ba thành viên có thể làm song song mà ít sửa trùng file.

## 1. Môi trường chung của dự án

- Backend: ASP.NET Core MVC trên .NET 10.
- Database: PostgreSQL 16 chạy trong Docker Compose.
- Compose có hai service: db và web.
- Website trong container kết nối tới PostgreSQL bằng host db, port 5432.
- Thành viên truy cập website tại http://localhost:8080; PostgreSQL được publish ở 127.0.0.1:5432.
- Khi khởi động web, ứng dụng chạy EF Core migrations nếu APPLY_DATABASE_MIGRATIONS=true.

Mỗi thành viên chạy database riêng trên máy của mình. Không cùng lúc sửa dữ liệu trong volume của máy người khác. Thay đổi cấu trúc database phải được đưa vào migration và commit cùng mã nguồn.

## 2. Phân công cho ba thành viên

Thay “Thành viên 1/2/3” bằng tên thật trong nhóm. Mỗi người sở hữu các file trong phần việc của mình và báo trước khi cần sửa file dùng chung.

| Người phụ trách | Phạm vi và file chính |
|---|---|
| **Thành viên 1 — Sân bay và chuyến bay** | Entity SanBay, MayBay, TuyenBay, ChuyenBay và cấu hình riêng cho các entity này; DTO tương ứng; ChuyenBayBUS.cs; controller, ViewModel và Views cho quản lý/tra cứu chuyến bay. |
| **Thành viên 2 — Đặt chỗ và vé** | Entity KhachHang, HanhKhach, DatVe, Ve và cấu hình riêng; DTO khách hàng/hành khách/đặt vé/vé/đổi-hủy; DatVeBUS.cs, VeBUS.cs; controller, ViewModel và Views cho đặt, tra cứu, đổi, hủy vé. |
| **Thành viên 3 — Tài khoản, tài chính và tích hợp** | Entity/DTO thanh toán và hoàn tiền; TaiKhoanBUS.cs, ThongKeBUS.cs; controller, ViewModel và Views cho tài khoản, thanh toán, hoàn tiền, thống kê. Là đầu mối tích hợp các thay đổi cần file dùng chung. |

### File dùng chung — chỉ một người tích hợp

Thành viên 3 là người tích hợp chính các file sau:

- AirlineTicketing.DAL/AppDbContext.cs
- AirlineTicketing.DAL/Migrations/*
- AirlineTicketing.Web/Program.cs
- compose.yaml, AirlineTicketing.Web/Dockerfile, .env.example, .gitignore

Thành viên 1 và 2 không tự tạo migration hoặc cùng sửa AppDbContext.cs. Khi thêm entity, gửi cho người tích hợp: tên entity, các trường và kiểu dữ liệu, nullable/default, khóa ngoại, index/unique/check constraint, và cấu hình EF tương ứng. Mỗi người vẫn có thể tạo file entity configuration riêng trong AirlineTicketing.DAL/Configurations; tránh cùng sửa một file cấu hình.

Nếu một thay đổi cần sửa file ngoài phạm vi sở hữu, báo trước trong nhóm và để người đang sở hữu file tích hợp thay đổi đó. Không tạo hai controller hoặc hai route cho cùng một luồng nghiệp vụ.

## 3. Quy trình Git để tránh xung đột

1. main luôn ở trạng thái có thể build và chạy.
2. Mỗi task làm trên một nhánh riêng, tạo từ main.
3. Đặt tên nhánh theo chức năng, ví dụ:

   - feature/flight-search
   - feature/flight-admin
   - feature/booking-create
   - feature/ticket-change-cancel
   - feature/payment-report
   - fix/booking-validation

4. Trước khi bắt đầu task mới, cập nhật nhánh từ main:

   ~~~powershell
   git switch main
   git pull
   git switch -c feature/ten-task
   ~~~

5. Commit những thay đổi thuộc một mục tiêu. Tránh format hoặc đổi tên hàng loạt file không liên quan.
6. Push nhánh và tạo Pull Request. Ghi rõ mục tiêu, file chính, migration (nếu có), cách kiểm tra và ảnh giao diện nếu có.
7. Một thành viên khác đọc thay đổi trước khi merge. Sau khi merge, mọi người cập nhật main trước khi bắt đầu task kế tiếp.
8. Không commit thẳng lên main, không force-push nhánh dùng chung.

Commit message nên nêu hành động và phần việc, ví dụ:

- feat(flight): add flight search
- feat(booking): create booking
- fix(ticket): prevent duplicate seat assignment
- db: add refund ledger migration

## 4. Hợp đồng giữa các phần code

Trước khi hai phần phụ thuộc nhau được làm song song, thống nhất ngắn gọn:

- Tên DTO và thuộc tính bắt buộc/tùy chọn.
- Kiểu dữ liệu, đơn vị tiền tệ, múi giờ và cách biểu diễn ngày giờ.
- Trạng thái nghiệp vụ hợp lệ và chuyển trạng thái nào được phép.
- BUS trả kết quả/lỗi theo dạng nào để Controller hiển thị.
- Route và tên action mà giao diện sử dụng.

Quy ước mã nguồn:

- Entity và class dùng PascalCase, tên entity số ít, ví dụ ChuyenBay.
- DTO kết thúc bằng DTO, ví dụ ChuyenBayDTO.
- Async method kết thúc bằng Async.
- Một class chính trong một file; đặt namespace theo project và khu vực nghiệp vụ.
- Không trả trực tiếp entity EF ra View. Dùng DTO/ViewModel phù hợp.
- Không tạo lại DTO hoặc enum đã có; tìm trong AirlineTicketing.DTO và AirlineTicketing.Domain trước.

Nếu phải đổi một hợp đồng đã được thành viên khác sử dụng, báo trong nhóm và cập nhật tất cả nơi gọi trong cùng Pull Request hoặc các Pull Request có thứ tự rõ ràng.

## 5. Quy trình thay đổi database

Chỉ thành viên tích hợp chính tạo và chạy migration. Thực hiện tuần tự:

1. Người làm tính năng nêu thay đổi schema và lý do.
2. Chốt entity, kiểu dữ liệu, nullable/default, khóa ngoại và index với người tích hợp.
3. Merge entity và configuration trước khi scaffold migration.
4. Người tích hợp tạo migration từ thư mục gốc solution:

   ~~~powershell
   dotnet tool install --global dotnet-ef --version 10.0.13
   dotnet ef migrations add TenMigration --project AirlineTicketing.DAL --startup-project AirlineTicketing.Web
   ~~~

   Nếu máy đã cài `dotnet-ef`, bỏ qua lệnh cài đặt. Chỉ chạy cài đặt một lần trên mỗi máy.

5. Đọc lại file migration và AppDbContextModelSnapshot.cs; migration không được xóa dữ liệu ngoài dự kiến.
6. Commit migration cùng thay đổi entity/configuration liên quan.
7. Chạy ứng dụng bằng Compose để web áp dụng migration. Không để nhiều thành viên cùng chạy migration trên một database dùng chung.

Không sửa/xóa migration đã merge để xử lý sai khác. Tạo migration mới để sửa schema. Nếu hai nhánh đều cần migration, merge thay đổi schema trước rồi chỉ người tích hợp scaffold migration sau cùng.

## 6. Chạy website và PostgreSQL bằng Docker

### Lần đầu trên mỗi máy

Từ thư mục gốc solution:

~~~powershell
Copy-Item .env.example .env
~~~

Compose đọc một file `.env` ở thư mục gốc để cấu hình cả PostgreSQL và website. Mỗi thành viên tạo file `.env` cục bộ từ cùng `.env.example`; không commit hoặc gửi file `.env` chứa mật khẩu thật.

Mở .env, đổi POSTGRES_PASSWORD thành mật khẩu riêng của máy. Không gửi mật khẩu qua commit, Pull Request hoặc tài liệu. .env đã được Git ignore; chỉ commit .env.example với giá trị mẫu.

Kiểm tra cấu hình và khởi động:

~~~powershell
docker compose config --quiet
docker compose up -d --build
docker compose ps
~~~

Kết quả cần thấy:

- db ở trạng thái healthy.
- web ở trạng thái running.
- http://localhost:8080/health/database trả về trạng thái kết nối PostgreSQL.

### Các lệnh dùng hằng ngày

~~~powershell
# Xem trạng thái
docker compose ps

# Xem log của website và database
docker compose logs --tail 100 web db

# Rebuild website sau khi đổi code C#
docker compose up -d --build web

# Mở psql bên trong container PostgreSQL
docker compose exec db sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'

# Dừng container, giữ nguyên volume database
docker compose down
~~~

Không dùng docker compose down -v để dừng dự án thông thường; -v xóa volume PostgreSQL. Nếu port 5432 hoặc 8080 đang được dùng, đổi POSTGRES_PORT hoặc ASPNETCORE_PORT trong .env của máy đó, không sửa compose.yaml chỉ để xử lý xung đột port cá nhân.

Trong container web, connection string phải dùng Host=db;Port=5432. Nếu chạy ứng dụng trực tiếp trên máy bằng dotnet run, kết nối PostgreSQL qua Host=localhost và port lấy từ .env. Không dùng localhost để ứng dụng trong container kết nối tới service db.

## 7. Checklist trước Pull Request

- Chỉ sửa file thuộc task hoặc đã báo trước với chủ file.
- DTO, entity, route và trạng thái khớp hợp đồng đã thống nhất.
- Nếu schema đổi: có entity/configuration và migration tương ứng, không chỉnh migration không liên quan.
- docker compose config --quiet thành công.
- docker compose up -d --build hoặc lệnh rebuild phù hợp chạy được.
- Kiểm tra docker compose ps, log cần thiết và /health/database.
- Không commit .env, mật khẩu, file bin/obj, hoặc thay đổi không liên quan.
- Mô tả rõ những phần chưa hoàn thành và bước cần người khác tích hợp.

## 8. Quy tắc khi gặp lỗi hoặc xung đột

- Không xóa volume, không dùng factory reset Docker để xử lý lỗi build thông thường.
- Khi gặp lỗi database, gửi log đã loại bỏ mật khẩu và thông tin bí mật; không gửi toàn bộ connection string.
- Khi có conflict, người sở hữu file quyết định cách kết hợp nội dung; không chọn “accept theirs/ours” hàng loạt.
- Nếu migration conflict, dừng scaffold ở các nhánh tính năng và chuyển quyền xử lý cho người tích hợp.
- Ghi lại nguyên nhân và cách sửa vào Pull Request hoặc issue để người khác không lặp lại lỗi.
