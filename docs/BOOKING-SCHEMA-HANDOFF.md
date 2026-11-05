# Booking/ticket schema handoff

Phạm vi này do nhánh đặt vé khai báo model/configuration. Người tích hợp migration cần tạo migration sau khi merge các thay đổi schema của các thành viên khác.

## Thay đổi cần có trong migration

- `DatVes.HetHanGiuCho`: `timestamptz`, bắt buộc.
- Index `DatVes(TrangThai, HetHanGiuCho)` để tác vụ hết hạn quét nhanh.
- `Ves.NgayPhatHanh`: `timestamptz`, nullable.
- `Ves.NgayHuy`: `timestamptz`, nullable.
- `Ves.LyDoHuy`: `varchar(300)`, nullable.
- Bảng `LichSuDatVes` theo `LichSuDatVeConfiguration`:
  - khóa chính `bigint`;
  - FK bắt buộc đến `DatVes`, FK nullable đến `Ves`, đều `Restrict`;
  - trạng thái trước/sau, hành động, lý do, người thực hiện và thời điểm;
  - `DuLieuBoSung` kiểu `jsonb`;
  - index `(DatVeId, ThoiDiem)`.

Unique index có điều kiện hiện tại trên `Ves(ChuyenBayId, SoGhe)` phải được giữ nguyên. BUS nhận diện constraint theo tên `IX_Ves_ChuyenBayId_SoGhe` để ánh xạ lỗi cạnh tranh thành `GHE_VUA_DUOC_GIU`.

## Lưu ý dữ liệu cũ

Vì `HetHanGiuCho` là cột bắt buộc, migration cần backfill trước khi đặt `NOT NULL`. Giá trị đề xuất:

- booking `ChoThanhToan`: `NgayDat + interval '15 minutes'`;
- booking ở trạng thái khác: `NgayDat`.

Không dùng `DateTimeOffset.UtcNow` làm default lâu dài cho cột này; hạn giữ phải được BUS tính từ cấu hình tại thời điểm tạo booking.
