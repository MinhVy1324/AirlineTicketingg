# Dữ liệu mẫu danh mục & lịch bay

`seed-catalog-flights.sql` nạp dữ liệu nền cho phần phụ trách của Đăng (sân bay, tàu bay,
tuyến bay, chuyến bay) và một ít vé mẫu để kiểm tra tồn chỗ / sơ đồ ghế.

Script **chạy lại nhiều lần được** (`ON CONFLICT DO NOTHING`) và không sửa migration hay
`AppDbContext`.

## Cách chạy

```bash
# 1. Khởi động CSDL
docker compose up -d db

# 2. Áp migration (nếu CSDL còn trống)
set -a; . ./.env; set +a
export ConnectionStrings__DefaultConnection="Host=localhost;Port=$POSTGRES_PORT;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD"
dotnet ef database update --project AirlineTicketing.DAL --startup-project AirlineTicketing.Web

# 3. Nạp dữ liệu mẫu
PGPASSWORD="$POSTGRES_PASSWORD" psql -h 127.0.0.1 -p "$POSTGRES_PORT" \
  -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 \
  -f docs/seed/seed-catalog-flights.sql
```

## Nội dung

| Bảng | Số dòng | Ghi chú |
| --- | --- | --- |
| `SanBays` | 8 | SGN, HAN, DAD, PQC, CXR, HUI, VCA, DLI |
| `MayBays` | 5 | A321, A320, B789, A350, AT76 |
| `TuyenBays` | 9 | Các cặp tuyến nội địa hai chiều chính |
| `ChuyenBays` | 21 | 19–21/11/2026, giờ lưu UTC |
| `Ves` | 10 | Vé `DaPhatHanh` + `TamGiu` để thử tồn chỗ |

Chuyến `VN210` ngày 20/11/2026 (SGN → HAN) có 3 vé đã phát hành và 1 vé đang giữ — dùng để
kiểm tra tìm kiếm theo số hành khách và sơ đồ ghế `/chuyen-bay/{id}/so-do-ghe`.

## Chạy web ở máy local

`appsettings.json` dùng chung đang trỏ cổng `5432`, trong khi container của nhóm map ra
`POSTGRES_PORT` (mặc định trong `.env` là `5433`). Khi chạy trực tiếp bằng `dotnet run`, ghi đè
chuỗi kết nối bằng biến môi trường thay vì sửa file dùng chung:

```bash
set -a; . ./.env; set +a
export ConnectionStrings__DefaultConnection="Host=localhost;Port=$POSTGRES_PORT;Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD"
dotnet run --project AirlineTicketing.Web
```
