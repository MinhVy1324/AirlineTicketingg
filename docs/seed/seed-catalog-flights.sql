-- Seed dữ liệu danh mục + lịch bay mẫu cho đồ án AirlineTicketing.
-- Chạy sau khi đã áp migration:
--   psql "host=localhost port=5433 dbname=airline_ticketing user=airline_app" -f docs/seed/seed-catalog-flights.sql
-- Script có thể chạy lại nhiều lần (ON CONFLICT DO NOTHING).

BEGIN;

-- ------------------------------------------------------------------ sân bay
INSERT INTO "SanBays" ("MaSanBay", "TenSanBay", "ThanhPho", "QuocGia", "DangHoatDong") VALUES
    ('SGN', 'Tân Sơn Nhất', 'TP. Hồ Chí Minh', 'Việt Nam', TRUE),
    ('HAN', 'Nội Bài', 'Hà Nội', 'Việt Nam', TRUE),
    ('DAD', 'Đà Nẵng', 'Đà Nẵng', 'Việt Nam', TRUE),
    ('PQC', 'Phú Quốc', 'Phú Quốc', 'Việt Nam', TRUE),
    ('CXR', 'Cam Ranh', 'Khánh Hòa', 'Việt Nam', TRUE),
    ('HUI', 'Phú Bài', 'Huế', 'Việt Nam', TRUE),
    ('VCA', 'Cần Thơ', 'Cần Thơ', 'Việt Nam', TRUE),
    ('DLI', 'Liên Khương', 'Đà Lạt', 'Việt Nam', TRUE)
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------------ tàu bay
INSERT INTO "MayBays" ("MaMayBay", "TenMayBay", "HangSanXuat", "SoGhe", "DangHoatDong") VALUES
    ('A321', 'Airbus A321', 'Airbus', 203, TRUE),
    ('A320', 'Airbus A320', 'Airbus', 180, TRUE),
    ('B789', 'Boeing 787-9', 'Boeing', 274, TRUE),
    ('A350', 'Airbus A350-900', 'Airbus', 305, TRUE),
    ('AT76', 'ATR 72-600', 'ATR', 68, TRUE)
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------------ tuyến bay
INSERT INTO "TuyenBays" ("SanBayDiId", "SanBayDenId", "ThoiLuongDuKienPhut", "DangKhaiThac")
SELECT di."Id", den."Id", v."Phut", TRUE
FROM (VALUES
    ('SGN', 'HAN', 130),
    ('HAN', 'SGN', 130),
    ('SGN', 'DAD', 80),
    ('DAD', 'SGN', 80),
    ('SGN', 'PQC', 60),
    ('PQC', 'SGN', 60),
    ('HAN', 'DAD', 80),
    ('SGN', 'CXR', 65),
    ('HAN', 'PQC', 130)
) AS v("Di", "Den", "Phut")
JOIN "SanBays" di ON di."MaSanBay" = v."Di"
JOIN "SanBays" den ON den."MaSanBay" = v."Den"
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------------ chuyến bay
-- Giờ trong CSDL lưu UTC; các mốc dưới đây ghi kèm +07 để Postgres tự đổi.
INSERT INTO "ChuyenBays" ("SoHieu", "TuyenBayId", "MayBayId", "GioKhoiHanh", "GioHaCanh", "GiaCoBan", "TrangThai")
SELECT v."SoHieu", t."Id", m."Id", v."GioDi", v."GioHa", v."Gia", v."TrangThai"
FROM (VALUES
    -- 19/11/2026
    ('VN210', 'SGN', 'HAN', 'A321', '2026-11-19 08:00:00+07'::timestamptz, '2026-11-19 10:10:00+07'::timestamptz, 1450000, 'DuKien'),
    ('VN216', 'SGN', 'HAN', 'B789', '2026-11-19 12:00:00+07'::timestamptz, '2026-11-19 14:10:00+07'::timestamptz, 1650000, 'DuKien'),

    -- 20/11/2026
    ('VN210', 'SGN', 'HAN', 'A321', '2026-11-20 08:00:00+07'::timestamptz, '2026-11-20 10:10:00+07'::timestamptz, 1500000, 'DuKien'),
    ('VN216', 'SGN', 'HAN', 'B789', '2026-11-20 12:00:00+07'::timestamptz, '2026-11-20 14:10:00+07'::timestamptz, 1700000, 'DuKien'),
    ('VN220', 'SGN', 'HAN', 'B789', '2026-11-20 16:00:00+07'::timestamptz, '2026-11-20 18:10:00+07'::timestamptz, 2000000, 'DuKien'),
    ('VJ120', 'SGN', 'HAN', 'A350', '2026-11-20 07:15:00+07'::timestamptz, '2026-11-20 09:25:00+07'::timestamptz, 1350000, 'DuKien'),
    ('QH150', 'SGN', 'HAN', 'A321', '2026-11-20 18:30:00+07'::timestamptz, '2026-11-20 20:40:00+07'::timestamptz, 1750000, 'DuKien'),
    ('VN211', 'HAN', 'SGN', 'A321', '2026-11-20 11:00:00+07'::timestamptz, '2026-11-20 13:10:00+07'::timestamptz, 1550000, 'DuKien'),
    ('VJ121', 'HAN', 'SGN', 'A320', '2026-11-20 14:30:00+07'::timestamptz, '2026-11-20 16:40:00+07'::timestamptz, 1400000, 'DuKien'),
    ('QH151', 'HAN', 'SGN', 'A350', '2026-11-20 19:00:00+07'::timestamptz, '2026-11-20 21:10:00+07'::timestamptz, 1800000, 'DuKien'),
    ('VN130', 'SGN', 'DAD', 'A320', '2026-11-20 09:00:00+07'::timestamptz, '2026-11-20 10:20:00+07'::timestamptz, 990000, 'DuKien'),
    ('VJ630', 'SGN', 'DAD', 'A321', '2026-11-20 15:00:00+07'::timestamptz, '2026-11-20 16:20:00+07'::timestamptz, 890000, 'DuKien'),
    ('VN131', 'DAD', 'SGN', 'A320', '2026-11-20 11:30:00+07'::timestamptz, '2026-11-20 12:50:00+07'::timestamptz, 950000, 'DuKien'),
    ('VN180', 'SGN', 'PQC', 'A320', '2026-11-20 07:00:00+07'::timestamptz, '2026-11-20 08:00:00+07'::timestamptz, 1150000, 'DuKien'),
    ('VJ320', 'SGN', 'PQC', 'AT76', '2026-11-20 13:00:00+07'::timestamptz, '2026-11-20 14:00:00+07'::timestamptz, 1090000, 'DuKien'),
    ('VN160', 'HAN', 'DAD', 'B789', '2026-11-20 09:00:00+07'::timestamptz, '2026-11-20 10:20:00+07'::timestamptz, 1050000, 'DuKien'),
    ('VN134', 'SGN', 'CXR', 'B789', '2026-11-20 10:30:00+07'::timestamptz, '2026-11-20 11:35:00+07'::timestamptz, 980000, 'DuKien'),
    ('VN182', 'HAN', 'PQC', 'A350', '2026-11-20 12:00:00+07'::timestamptz, '2026-11-20 14:10:00+07'::timestamptz, 2100000, 'DuKien'),

    -- 21/11/2026
    ('VN210', 'SGN', 'HAN', 'A321', '2026-11-21 08:00:00+07'::timestamptz, '2026-11-21 10:10:00+07'::timestamptz, 1600000, 'DuKien'),
    ('VN216', 'SGN', 'HAN', 'B789', '2026-11-21 12:00:00+07'::timestamptz, '2026-11-21 14:10:00+07'::timestamptz, 1750000, 'DuKien'),
    ('VJ120', 'SGN', 'HAN', 'A350', '2026-11-21 07:15:00+07'::timestamptz, '2026-11-21 09:25:00+07'::timestamptz, 1420000, 'DuKien')
) AS v("SoHieu", "Tu", "Den", "MayBay", "GioDi", "GioHa", "Gia", "TrangThai")
JOIN "TuyenBays" t ON t."SanBayDiId" = (SELECT "Id" FROM "SanBays" WHERE "MaSanBay" = v."Tu")
                 AND t."SanBayDenId" = (SELECT "Id" FROM "SanBays" WHERE "MaSanBay" = v."Den")
JOIN "MayBays" m ON m."MaMayBay" = v."MayBay"
ON CONFLICT DO NOTHING;

-- ------------------------------------------------------------------ dữ liệu bán chỗ mẫu
INSERT INTO "KhachHangs" ("HoTen", "Email", "SoDienThoai", "NgayTao") VALUES
    ('Nguyễn Minh Anh', 'minhanh@example.com', '+84912345678', '2026-11-01 09:30:00+07')
ON CONFLICT DO NOTHING;

INSERT INTO "HanhKhachs" ("HoTen", "NgaySinh", "QuocTich", "SoHoChieu") VALUES
    ('NGUYEN MINH ANH', '1998-08-15', 'Việt Nam', '079098001234'),
    ('TRAN BAO NGOC', '1995-03-22', 'Việt Nam', '079095003456'),
    ('DANG THU HA', '1999-11-02', 'Việt Nam', '079099005678'),
    ('LE QUOC LONG', '1990-06-10', 'Việt Nam', '079090007890'),
    ('PHAM HOAI AN', '2001-01-18', 'Việt Nam', '079001009012'),
    ('HOANG TUAN KIET', '1997-09-09', 'Việt Nam', '079097001234'),
    ('VU THANH TAM', '1993-12-30', 'Việt Nam', '079093003456'),
    ('DO MAI LINH', '2000-04-14', 'Việt Nam', '079000005678'),
    ('BUI DUC HUY', '1996-07-07', 'Việt Nam', '079096007890')
ON CONFLICT DO NOTHING;

INSERT INTO "DatVes" ("MaDatVe", "KhachHangId", "NgayDat", "TrangThai", "TongTien", "DonViTienTe")
SELECT v."MaDatVe", k."Id", v."NgayDat", v."TrangThai", v."TongTien", 'VND'
FROM (VALUES
    ('AV8K2M', 'minhanh@example.com', '2026-11-01 09:30:00+07'::timestamptz, 'DaXacNhan', 6000000),
    ('AV9P4D', 'minhanh@example.com', '2026-11-02 10:05:00+07'::timestamptz, 'DaXacNhan', 5100000),
    ('AV7T2Q', 'minhanh@example.com', '2026-11-02 11:20:00+07'::timestamptz, 'DaXacNhan', 4000000),
    ('AV5N8B', 'minhanh@example.com', '2026-11-03 08:10:00+07'::timestamptz, 'DaXacNhan', 1350000)
) AS v("MaDatVe", "Email", "NgayDat", "TrangThai", "TongTien")
JOIN "KhachHangs" k ON k."Email" = v."Email"
ON CONFLICT DO NOTHING;

-- VN210 20/11: 3 vé đã phát hành + 1 vé đang giữ
INSERT INTO "Ves" ("MaVe", "ChuyenBayId", "DatVeId", "HanhKhachId", "SoGhe", "HangVe", "GiaVe", "TrangThai")
SELECT v."MaVe", cb."Id", dv."Id", hk."Id", v."SoGhe", 'PhoThong', v."GiaVe", v."TrangThai"
FROM (VALUES
    ('VE-VN210-01', 'VN210', '2026-11-20 08:00:00+07'::timestamptz, 'AV8K2M', '079098001234', '1A', 1500000, 'DaPhatHanh'),
    ('VE-VN210-02', 'VN210', '2026-11-20 08:00:00+07'::timestamptz, 'AV8K2M', '079095003456', '1B', 1500000, 'DaPhatHanh'),
    ('VE-VN210-03', 'VN210', '2026-11-20 08:00:00+07'::timestamptz, 'AV8K2M', '079099005678', '2A', 1500000, 'DaPhatHanh'),
    ('VE-VN210-04', 'VN210', '2026-11-20 08:00:00+07'::timestamptz, 'AV8K2M', '079090007890', '3A', 1500000, 'TamGiu'),

    -- VN216 20/11: 2 vé đã phát hành + 1 đang giữ
    ('VE-VN216-01', 'VN216', '2026-11-20 12:00:00+07'::timestamptz, 'AV9P4D', '079001009012', '5A', 1700000, 'DaPhatHanh'),
    ('VE-VN216-02', 'VN216', '2026-11-20 12:00:00+07'::timestamptz, 'AV9P4D', '079097001234', '5B', 1700000, 'DaPhatHanh'),
    ('VE-VN216-03', 'VN216', '2026-11-20 12:00:00+07'::timestamptz, 'AV9P4D', '079093003456', '6A', 1700000, 'TamGiu'),

    -- VN220 20/11: 2 vé đang giữ
    ('VE-VN220-01', 'VN220', '2026-11-20 16:00:00+07'::timestamptz, 'AV7T2Q', '079000005678', '7A', 2000000, 'TamGiu'),
    ('VE-VN220-02', 'VN220', '2026-11-20 16:00:00+07'::timestamptz, 'AV7T2Q', '079096007890', '7B', 2000000, 'TamGiu'),

    -- VJ120 20/11: 1 vé đã phát hành
    ('VE-VJ120-01', 'VJ120', '2026-11-20 07:15:00+07'::timestamptz, 'AV5N8B', '079098001234', '10A', 1350000, 'DaPhatHanh')
) AS v("MaVe", "SoHieu", "GioDi", "MaDatVe", "SoHoChieu", "SoGhe", "GiaVe", "TrangThai")
JOIN "ChuyenBays" cb ON cb."SoHieu" = v."SoHieu" AND cb."GioKhoiHanh" = v."GioDi"
JOIN "DatVes" dv ON dv."MaDatVe" = v."MaDatVe"
JOIN "HanhKhachs" hk ON hk."SoHoChieu" = v."SoHoChieu"
ON CONFLICT DO NOTHING;

COMMIT;
