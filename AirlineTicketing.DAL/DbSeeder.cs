using AirlineTicketing.DAL.Security;
using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.DAL;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
    {
        // 1. Seed Accounts if none exist
        if (!await context.TaiKhoans.AnyAsync(cancellationToken))
        {
            // Seed Customer Profile first
            var khachHang = new KhachHang
            {
                HoTen = "Trần Mai Phương",
                Email = "phuong.khachhang@gmail.com",
                SoDienThoai = "0901234567",
                NgayTao = DateTimeOffset.UtcNow
            };
            await context.KhachHangs.AddAsync(khachHang, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var taiKhoans = new List<TaiKhoan>
            {
                new()
                {
                    TenDangNhap = "admin",
                    MatKhauHash = PasswordSecurity.HashPassword("Admin@123456"),
                    HoTen = "Quản Trị Viên Hệ Thống",
                    Email = "admin@airline.vn",
                    SoDienThoai = "0988888888",
                    VaiTro = VaiTroTaiKhoan.Admin,
                    TrangThai = TrangThaiTaiKhoan.HoatDong,
                    NgayTao = DateTimeOffset.UtcNow
                },
                new()
                {
                    TenDangNhap = "nhanvien",
                    MatKhauHash = PasswordSecurity.HashPassword("Staff@123456"),
                    HoTen = "Nhân Viên Bán Vé & CSKH",
                    Email = "nhanvien@airline.vn",
                    SoDienThoai = "0977777777",
                    VaiTro = VaiTroTaiKhoan.NhanVien,
                    TrangThai = TrangThaiTaiKhoan.HoatDong,
                    NgayTao = DateTimeOffset.UtcNow
                },
                new()
                {
                    TenDangNhap = "khachhang",
                    MatKhauHash = PasswordSecurity.HashPassword("Customer@123456"),
                    HoTen = "Trần Mai Phương",
                    Email = "phuong.khachhang@gmail.com",
                    SoDienThoai = "0901234567",
                    VaiTro = VaiTroTaiKhoan.KhachHang,
                    TrangThai = TrangThaiTaiKhoan.HoatDong,
                    NgayTao = DateTimeOffset.UtcNow,
                    KhachHangId = khachHang.Id
                }
            };

            await context.TaiKhoans.AddRangeAsync(taiKhoans, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        // 2. Seed Master Data (SanBay, MayBay, TuyenBay, ChuyenBay) if none exist
        if (!await context.SanBays.AnyAsync(cancellationToken))
        {
            var sgn = new SanBay { MaSanBay = "SGN", TenSanBay = "Sân bay Quốc tế Tân Sơn Nhất", ThanhPho = "TP. Hồ Chí Minh", QuocGia = "Việt Nam", DangHoatDong = true };
            var han = new SanBay { MaSanBay = "HAN", TenSanBay = "Sân bay Quốc tế Nội Bài", ThanhPho = "Hà Nội", QuocGia = "Việt Nam", DangHoatDong = true };
            var dad = new SanBay { MaSanBay = "DAD", TenSanBay = "Sân bay Quốc tế Đà Nẵng", ThanhPho = "Đà Nẵng", QuocGia = "Việt Nam", DangHoatDong = true };
            var cxr = new SanBay { MaSanBay = "CXR", TenSanBay = "Sân bay Quốc tế Cam Ranh", ThanhPho = "Nha Trang", QuocGia = "Việt Nam", DangHoatDong = true };
            var pqc = new SanBay { MaSanBay = "PQC", TenSanBay = "Sân bay Quốc tế Phú Quốc", ThanhPho = "Phú Quốc", QuocGia = "Việt Nam", DangHoatDong = true };

            await context.SanBays.AddRangeAsync([sgn, han, dad, cxr, pqc], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var a321 = new MayBay { MaMayBay = "VN-A321", TenMayBay = "Airbus A321-200", HangSanXuat = "Airbus", SoGhe = 180, DangHoatDong = true };
            var a350 = new MayBay { MaMayBay = "VN-A350", TenMayBay = "Airbus A350-900", HangSanXuat = "Airbus", SoGhe = 305, DangHoatDong = true };
            var b787 = new MayBay { MaMayBay = "VN-A787", TenMayBay = "Boeing 787-9 Dreamliner", HangSanXuat = "Boeing", SoGhe = 274, DangHoatDong = true };

            await context.MayBays.AddRangeAsync([a321, a350, b787], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var route1 = new TuyenBay { SanBayDiId = sgn.Id, SanBayDenId = han.Id, ThoiLuongDuKienPhut = 130, DangKhaiThac = true };
            var route2 = new TuyenBay { SanBayDiId = han.Id, SanBayDenId = sgn.Id, ThoiLuongDuKienPhut = 130, DangKhaiThac = true };
            var route3 = new TuyenBay { SanBayDiId = sgn.Id, SanBayDenId = dad.Id, ThoiLuongDuKienPhut = 80, DangKhaiThac = true };
            var route4 = new TuyenBay { SanBayDiId = han.Id, SanBayDenId = dad.Id, ThoiLuongDuKienPhut = 85, DangKhaiThac = true };

            await context.TuyenBays.AddRangeAsync([route1, route2, route3, route4], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var flight1 = new ChuyenBay
            {
                SoHieu = "VN-120",
                TuyenBayId = route1.Id,
                MayBayId = a321.Id,
                GioKhoiHanh = now.AddDays(1).Date.AddHours(8),
                GioHaCanh = now.AddDays(1).Date.AddHours(10).AddMinutes(10),
                GiaCoBan = 1500000m,
                TrangThai = TrangThaiChuyenBay.DuKien
            };

            var flight2 = new ChuyenBay
            {
                SoHieu = "VN-122",
                TuyenBayId = route1.Id,
                MayBayId = a350.Id,
                GioKhoiHanh = now.AddDays(1).Date.AddHours(14),
                GioHaCanh = now.AddDays(1).Date.AddHours(16).AddMinutes(10),
                GiaCoBan = 1800000m,
                TrangThai = TrangThaiChuyenBay.DuKien
            };

            var flight3 = new ChuyenBay
            {
                SoHieu = "VN-215",
                TuyenBayId = route2.Id,
                MayBayId = b787.Id,
                GioKhoiHanh = now.AddDays(2).Date.AddHours(9),
                GioHaCanh = now.AddDays(2).Date.AddHours(11).AddMinutes(10),
                GiaCoBan = 1600000m,
                TrangThai = TrangThaiChuyenBay.DuKien
            };

            var flight4 = new ChuyenBay
            {
                SoHieu = "VN-301",
                TuyenBayId = route3.Id,
                MayBayId = a321.Id,
                GioKhoiHanh = now.AddDays(3).Date.AddHours(7),
                GioHaCanh = now.AddDays(3).Date.AddHours(8).AddMinutes(20),
                GiaCoBan = 1200000m,
                TrangThai = TrangThaiChuyenBay.DuKien
            };

            await context.ChuyenBays.AddRangeAsync([flight1, flight2, flight3, flight4], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // 3. Seed Sample Bookings, Passengers, Tickets, Payments & Refunds for Reporting & Demo
            var passenger1 = new HanhKhach
            {
                HoTen = "Trần Mai Phương",
                NgaySinh = new DateOnly(1995, 5, 20),
                QuocTich = "Việt Nam",
                SoHoChieu = "C1234567"
            };

            var passenger2 = new HanhKhach
            {
                HoTen = "Lê Hoàng Nam",
                NgaySinh = new DateOnly(1993, 10, 15),
                QuocTich = "Việt Nam",
                SoHoChieu = "B9876543"
            };

            await context.HanhKhachs.AddRangeAsync([passenger1, passenger2], cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var khachHang = await context.KhachHangs.FirstAsync(cancellationToken);

            // Booking 1: Confirmed & Paid (3,000,000 VND)
            var booking1 = new DatVe
            {
                MaDatVe = "DATVE-DEMO01",
                KhachHangId = khachHang.Id,
                NgayDat = now.AddDays(-2),
                TrangThai = TrangThaiDatVe.DaXacNhan,
                TongTien = 3000000m,
                DonViTienTe = "VND"
            };
            await context.DatVes.AddAsync(booking1, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var ticket1 = new Ve
            {
                MaVe = "VE-SGN-HAN-001",
                DatVeId = booking1.Id,
                ChuyenBayId = flight1.Id,
                HanhKhachId = passenger1.Id,
                SoGhe = "12A",
                HangVe = "PhoThong",
                GiaVe = 1500000m,
                TrangThai = TrangThaiVe.DaPhatHanh
            };

            var ticket2 = new Ve
            {
                MaVe = "VE-SGN-HAN-002",
                DatVeId = booking1.Id,
                ChuyenBayId = flight1.Id,
                HanhKhachId = passenger2.Id,
                SoGhe = "12B",
                HangVe = "PhoThong",
                GiaVe = 1500000m,
                TrangThai = TrangThaiVe.DaPhatHanh
            };
            await context.Ves.AddRangeAsync([ticket1, ticket2], cancellationToken);

            var payment1 = new ThanhToan
            {
                DatVeId = booking1.Id,
                SoTien = 3000000m,
                DonViTienTe = "VND",
                PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                NhaCungCap = "VNPAY",
                TrangThai = TrangThaiThanhToan.ThanhCong,
                MaGiaoDich = "VNPAY-TXN-20261030-001",
                NgayTao = now.AddDays(-2),
                NgayHoanTat = now.AddDays(-2).AddMinutes(5)
            };
            await context.ThanhToans.AddAsync(payment1, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // Booking 2: Refunded (Paid 1,500,000, Refunded 1,500,000)
            var booking2 = new DatVe
            {
                MaDatVe = "DATVE-DEMO02",
                KhachHangId = khachHang.Id,
                NgayDat = now.AddDays(-1),
                TrangThai = TrangThaiDatVe.HoanTien,
                TongTien = 1500000m,
                DonViTienTe = "VND"
            };
            await context.DatVes.AddAsync(booking2, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var ticket3 = new Ve
            {
                MaVe = "VE-HAN-SGN-003",
                DatVeId = booking2.Id,
                ChuyenBayId = flight3.Id,
                HanhKhachId = passenger1.Id,
                SoGhe = "15C",
                HangVe = "PhoThong",
                GiaVe = 1500000m,
                TrangThai = TrangThaiVe.DaHoanTien
            };
            await context.Ves.AddAsync(ticket3, cancellationToken);

            var payment2 = new ThanhToan
            {
                DatVeId = booking2.Id,
                SoTien = 1500000m,
                DonViTienTe = "VND",
                PhuongThuc = PhuongThucThanhToan.TheNganHang,
                NhaCungCap = "MoMo",
                TrangThai = TrangThaiThanhToan.ThanhCong,
                MaGiaoDich = "MOMO-TXN-20261031-002",
                NgayTao = now.AddDays(-1),
                NgayHoanTat = now.AddDays(-1).AddMinutes(2)
            };
            await context.ThanhToans.AddAsync(payment2, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var refund1 = new HoanTien
            {
                ThanhToanId = payment2.Id,
                SoTien = 1500000m,
                DonViTienTe = "VND",
                NhaCungCap = "MoMo",
                MaGiaoDich = "MOMO-REFUND-001",
                LyDo = "Khách hàng yêu cầu hủy vé do việc gia đình đột xuất",
                TrangThai = TrangThaiHoanTien.ThanhCong,
                NgayTao = now.AddDays(-1).AddHours(3),
                NgayHoanTat = now.AddDays(-1).AddHours(3).AddMinutes(10)
            };
            await context.HoanTiens.AddAsync(refund1, cancellationToken);

            // Booking 3: Pending payment (1,200,000 VND) ready for test payment!
            var booking3 = new DatVe
            {
                MaDatVe = "DATVE-DEMO03",
                KhachHangId = khachHang.Id,
                NgayDat = now,
                TrangThai = TrangThaiDatVe.ChoThanhToan,
                TongTien = 1200000m,
                DonViTienTe = "VND"
            };
            await context.DatVes.AddAsync(booking3, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            var ticket4 = new Ve
            {
                MaVe = "VE-SGN-DAD-004",
                DatVeId = booking3.Id,
                ChuyenBayId = flight4.Id,
                HanhKhachId = passenger2.Id,
                SoGhe = "08D",
                HangVe = "PhoThong",
                GiaVe = 1200000m,
                TrangThai = TrangThaiVe.TamGiu
            };
            await context.Ves.AddAsync(ticket4, cancellationToken);

            var payment3 = new ThanhToan
            {
                DatVeId = booking3.Id,
                SoTien = 1200000m,
                DonViTienTe = "VND",
                PhuongThuc = PhuongThucThanhToan.ChuyenKhoan,
                NhaCungCap = "VNPAY",
                TrangThai = TrangThaiThanhToan.ChoXuLy,
                MaGiaoDich = "VNPAY-PENDING-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                NgayTao = now
            };
            await context.ThanhToans.AddAsync(payment3, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
