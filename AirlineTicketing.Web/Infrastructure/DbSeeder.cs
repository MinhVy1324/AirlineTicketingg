using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure;

public static class DbSeeder
{
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.TaiKhoans.AnyAsync(ct))
        {
            return;
        }

        var now = new DateTimeOffset(2026, 11, 1, 10, 0, 0, Vn);

        // --- Airports ---
        var airports = new[]
        {
            A("SGN", "Tân Sơn Nhất", "TP. Hồ Chí Minh", "Việt Nam"),
            A("HAN", "Nội Bài", "Hà Nội", "Việt Nam"),
            A("DAD", "Đà Nẵng", "Đà Nẵng", "Việt Nam"),
            A("PQC", "Phú Quốc", "Phú Quốc", "Việt Nam"),
            A("CXR", "Cam Ranh", "Khánh Hòa", "Việt Nam"),
            A("HUI", "Phú Bài", "Huế", "Việt Nam"),
            A("VCA", "Cần Thơ", "Cần Thơ", "Việt Nam"),
            A("HPH", "Cát Bi", "Hải Phòng", "Việt Nam"),
            A("DLI", "Liên Khương", "Đà Lạt", "Việt Nam"),
            A("VII", "Vinh", "Nghệ An", "Việt Nam"),
        };
        db.SanBays.AddRange(airports);
        await db.SaveChangesAsync(ct);
        var ap = airports.ToDictionary(x => x.MaSanBay, x => x.Id, StringComparer.OrdinalIgnoreCase);

        // --- Airlines ---
        var airlines = new[]
        {
            new HangHangKhong { MaHang = "VN", TenHang = "Vietnam Airlines", QuocGia = "Việt Nam", MaIcao = "HVN" },
            new HangHangKhong { MaHang = "VJ", TenHang = "Vietjet Air", QuocGia = "Việt Nam", MaIcao = "VJC" },
            new HangHangKhong { MaHang = "QH", TenHang = "Bamboo Airways", QuocGia = "Việt Nam", MaIcao = "BAV" },
            new HangHangKhong { MaHang = "BL", TenHang = "Pacific Airlines", QuocGia = "Việt Nam", MaIcao = "PIC" },
            new HangHangKhong { MaHang = "VU", TenHang = "Vietravel Airlines", QuocGia = "Việt Nam", MaIcao = "VAG" },
            new HangHangKhong { MaHang = "0V", TenHang = "VASCO", QuocGia = "Việt Nam", MaIcao = "VAS", DangHoatDong = false },
        };
        db.HangHangKhongS.AddRange(airlines);
        await db.SaveChangesAsync(ct);
        var al = airlines.ToDictionary(x => x.MaHang, x => x.Id, StringComparer.OrdinalIgnoreCase);

        // --- Aircraft ---
        var aircraft = new[]
        {
            new MayBay { MaMayBay = "A321", TenMayBay = "Airbus A321", HangSanXuat = "Airbus", SoGhe = 203, NamKhaiThac = 2019 },
            new MayBay { MaMayBay = "A320", TenMayBay = "Airbus A320", HangSanXuat = "Airbus", SoGhe = 180, NamKhaiThac = 2018 },
            new MayBay { MaMayBay = "A350", TenMayBay = "Airbus A350-900", HangSanXuat = "Airbus", SoGhe = 305, NamKhaiThac = 2020 },
            new MayBay { MaMayBay = "B789", TenMayBay = "Boeing 787-9", HangSanXuat = "Boeing", SoGhe = 274, NamKhaiThac = 2017 },
            new MayBay { MaMayBay = "AT76", TenMayBay = "ATR 72-600", HangSanXuat = "ATR", SoGhe = 68, NamKhaiThac = 2016 },
            new MayBay { MaMayBay = "E190", TenMayBay = "Embraer E190", HangSanXuat = "Embraer", SoGhe = 98, NamKhaiThac = 2015, DangHoatDong = false },
        };
        db.MayBays.AddRange(aircraft);
        await db.SaveChangesAsync(ct);
        var ac = aircraft.ToDictionary(x => x.MaMayBay, x => x.Id, StringComparer.OrdinalIgnoreCase);

        // --- Routes ---
        var routes = new List<TuyenBay>
        {
            R(ap["SGN"], ap["HAN"], 130), R(ap["SGN"], ap["DAD"], 80), R(ap["HAN"], ap["DAD"], 80),
            R(ap["SGN"], ap["PQC"], 60), R(ap["HAN"], ap["PQC"], 125), R(ap["SGN"], ap["CXR"], 60),
            R(ap["HAN"], ap["SGN"], 130), R(ap["DAD"], ap["SGN"], 80), R(ap["SGN"], ap["HUI"], 80),
            R(ap["SGN"], ap["VCA"], 45),
        };
        db.TuyenBays.AddRange(routes);
        await db.SaveChangesAsync(ct);
        var rt = routes.ToDictionary(x => (x.SanBayDiId, x.SanBayDenId));

        // --- Flights (20/11/2026 main demo date) ---
        var d = new DateOnly(2026, 11, 20);
        var vn210 = F("VN210", al["VN"], ac["A321"], rt[(ap["SGN"], ap["HAN"])].Id, d, 8, 0, 130, 1_500_000m);
        var vn216 = F("VN216", al["VN"], ac["A321"], rt[(ap["SGN"], ap["HAN"])].Id, d, 12, 0, 130, 1_700_000m);
        var vn220 = F("VN220", al["VN"], ac["A321"], rt[(ap["SGN"], ap["HAN"])].Id, d, 16, 0, 130, 2_000_000m);
        var vj120 = F("VJ120", al["VJ"], ac["A320"], rt[(ap["SGN"], ap["HAN"])].Id, d, 7, 15, 130, 1_350_000m);
        var vj620 = F("VJ620", al["VJ"], ac["A320"], rt[(ap["SGN"], ap["DAD"])].Id, new DateOnly(2026, 12, 5), 9, 30, 80, 1_050_000m);
        var vn118 = F("VN118", al["VN"], ac["A321"], rt[(ap["SGN"], ap["DAD"])].Id, new DateOnly(2026, 12, 6), 9, 0, 80, 1_150_000m);
        var vn214 = F("VN214", al["VN"], ac["A321"], rt[(ap["SGN"], ap["HAN"])].Id, new DateOnly(2026, 10, 12), 9, 0, 130, 1_450_000m, TrangThaiChuyenBay.HoanTat);
        var vn218 = F("VN218", al["VN"], ac["A321"], rt[(ap["SGN"], ap["DAD"])].Id, new DateOnly(2026, 11, 5), 7, 30, 80, 1_300_000m, TrangThaiChuyenBay.BiHuy);
        var flights = new[] { vn210, vn216, vn220, vj120, vj620, vn118, vn214, vn218 };
        db.ChuyenBays.AddRange(flights);
        await db.SaveChangesAsync(ct);

        // --- Fare classes ---
        foreach (var flight in flights.Where(f => f.SoHieu is "VN210" or "VN216" or "VN220" or "VJ120"))
        {
            db.HangVes.AddRange(
                HV(flight.Id, "Y", "Phổ thông Tiết kiệm", "Phổ thông", flight.GiaCoBan - 150_000m, 120, 7, 23, 360_000m, 500_000m),
                HV(flight.Id, "M", "Phổ thông Tiêu chuẩn", "Phổ thông", flight.GiaCoBan, 68, 10, 23, 360_000m, 500_000m),
                HV(flight.Id, "C", "Thương gia", "Thương gia", flight.GiaCoBan + 3_000_000m, 12, 7, 32, 0m, 0m));
        }
        foreach (var flight in flights.Where(f => f.SoHieu is "VJ620" or "VN118" or "VN214" or "VN218"))
        {
            db.HangVes.AddRange(
                HV(flight.Id, "Y", "Phổ thông Tiết kiệm", "Phổ thông", flight.GiaCoBan - 100_000m, 130, 12, 20, 360_000m, 500_000m),
                HV(flight.Id, "M", "Phổ thông Tiêu chuẩn", "Phổ thông", flight.GiaCoBan, 50, 10, 23, 360_000m, 500_000m));
        }
        await db.SaveChangesAsync(ct);

        // --- Settings ---
        db.CauHinhHeThongs.AddRange(
            S("booking.hold_minutes", "15", "booking", "Thời gian giữ chỗ (phút)", "number", now),
            S("booking.service_fee", "70000", "booking", "Phí dịch vụ / khách / chặng (VND)", "number", now),
            S("booking.tax_fee_text", "Theo biểu phí hãng / sân bay", "booking", "Thuế & phí sân bay", "text", now),
            S("policy.change_fee", "360000", "policy", "Phí đổi / khách (VND)", "number", now),
            S("policy.refund_fee", "500000", "policy", "Phí hoàn / khách (VND)", "number", now),
            S("policy.request_deadline_hours", "3", "policy", "Thời hạn gửi yêu cầu trước giờ bay (giờ)", "number", now),
            S("policy.refund_days", "7–15 ngày làm việc", "policy", "Thời gian hoàn tiền dự kiến", "text", now),
            S("payment.provider", "VNPay", "payment", "Cổng thanh toán", "text", now),
            S("payment.environment", "Sandbox", "payment", "Môi trường thanh toán", "text", now),
            S("notification.system_email", "no-reply@aeroviet.vn", "notification", "Email gửi hệ thống", "text", now));
        await db.SaveChangesAsync(ct);

        // --- Accounts & customers ---
        var khMinhAnh = new KhachHang
        {
            HoTen = "Nguyễn Minh Anh",
            Email = "minhanh@example.com",
            SoDienThoai = "+84 912 345 678",
            NgayTao = now.AddDays(-30)
        };
        var khLocked = new KhachHang
        {
            HoTen = "Đỗ Đức Nam",
            Email = "nam.user@example.com",
            SoDienThoai = "+84 909 111 222",
            NgayTao = now.AddDays(-20)
        };
        db.KhachHangs.AddRange(khMinhAnh, khLocked);
        await db.SaveChangesAsync(ct);

        var admin = Tk("admin", "Admin@123", "Trần Hoàng Tùng", "tung.admin@example.com", VaiTro.Admin, "NV-001", now.AddDays(-60), now.AddHours(-2));
        var manager = Tk("manager", "Manager@123", "Lê Quốc Long", "long.manager@example.com", VaiTro.Manager, "NV-002", now.AddDays(-45), now.AddHours(-2).AddMinutes(-15));
        var customer = Tk("minhanh", "Customer@123", "Nguyễn Minh Anh", "minhanh@example.com", VaiTro.KhachHang, null, now.AddDays(-30), now.AddMinutes(-40), khMinhAnh.Id, "+84 912 345 678");
        var manager2 = Tk("ha.manager", "Manager@123", "Phạm Thanh Hà", "ha.manager@example.com", VaiTro.Manager, "NV-003", now.AddDays(-40), now.AddDays(-1));
        var locked = Tk("nam.user", "Customer@123", "Đỗ Đức Nam", "nam.user@example.com", VaiTro.KhachHang, null, now.AddDays(-20), now.AddDays(-2), khLocked.Id, "+84 909 111 222");
        locked.TrangThai = TrangThaiTaiKhoan.BiKhoa;
        locked.GhiChu = "Tạm khóa do đăng nhập sai nhiều lần.";
        db.TaiKhoans.AddRange(admin, manager, customer, manager2, locked);
        await db.SaveChangesAsync(ct);

        // --- Passenger, booking, ticket, payment (main demo: AV8K2M / VN210) ---
        var hk = new HanhKhach
        {
            HoTen = "NGUYEN MINH ANH",
            NgaySinh = new DateOnly(1998, 8, 15),
            GioiTinh = GioiTinh.Nu,
            LoaiHanhKhach = LoaiHanhKhach.NguoiLon,
            QuocTich = "Việt Nam",
            SoHoChieu = "079098001234",
            Email = "minhanh@example.com",
            SoDienThoai = "+84 912 345 678"
        };
        db.HanhKhachs.Add(hk);
        await db.SaveChangesAsync(ct);

        var datVe = new DatVe
        {
            MaDatVe = "AV8K2M",
            KhachHangId = khMinhAnh.Id,
            NgayDat = now.AddMinutes(-30),
            TrangThai = TrangThaiDatVe.DaXacNhan,
            TongTien = 2_200_000m,
            DonViTienTe = "VND",
            HanGiuCho = now.AddMinutes(-15),
            NgayXacNhan = now.AddMinutes(-18),
            ThuePhi = 380_000m,
            PhiDichVu = 70_000m,
            PhiHanhLy = 250_000m,
            EmailLienHe = "minhanh@example.com",
            SoDienThoaiLienHe = "+84 912 345 678"
        };
        db.DatVes.Add(datVe);
        await db.SaveChangesAsync(ct);

        var ve = new Ve
        {
            MaVe = "738 2400012865",
            ChuyenBayId = vn210.Id,
            DatVeId = datVe.Id,
            HanhKhachId = hk.Id,
            SoGhe = "18A",
            HangVe = "Phổ thông Tiêu chuẩn",
            GiaVe = 1_500_000m,
            TrangThai = TrangThaiVe.DaPhatHanh,
            NgayPhatHanh = now.AddMinutes(-17),
            HanhLyXachTayKg = 12,
            HanhLyKyGuiKg = 33,
            PhiDoi = 360_000m,
            PhiHoan = 500_000m
        };
        db.Ves.Add(ve);
        // adjust sold seats
        var hv210 = await db.HangVes.FirstAsync(x => x.ChuyenBayId == vn210.Id && x.MaHang == "M", ct);
        hv210.SoGheDaBan += 1;
        await db.SaveChangesAsync(ct);

        var tt = new ThanhToan
        {
            DatVeId = datVe.Id,
            SoTien = 2_200_000m,
            PhuongThuc = PhuongThucThanhToan.TheNganHang,
            NhaCungCap = "VNPay",
            TrangThai = TrangThaiThanhToan.ThanhCong,
            MaGiaoDich = "TX0111260942",
            NgayTao = now.AddMinutes(-19),
            NgayHoanTat = now.AddMinutes(-18)
        };
        db.ThanhToans.Add(tt);
        await db.SaveChangesAsync(ct);

        // --- Extra booking/request for manager screens ---
        var khLinh = new KhachHang { HoTen = "Trần Mai Linh", Email = "linh@example.com", SoDienThoai = "+84 903 000 111", NgayTao = now.AddDays(-10) };
        db.KhachHangs.Add(khLinh);
        await db.SaveChangesAsync(ct);
        var hkLinh = new HanhKhach { HoTen = "TRAN MAI LINH", NgaySinh = new DateOnly(1995, 3, 2), GioiTinh = GioiTinh.Nu, LoaiHanhKhach = LoaiHanhKhach.NguoiLon, QuocTich = "Việt Nam", Email = "linh@example.com" };
        db.HanhKhachs.Add(hkLinh);
        await db.SaveChangesAsync(ct);
        var dvLinh = new DatVe { MaDatVe = "AV4R6C", KhachHangId = khLinh.Id, NgayDat = now.AddHours(-2), TrangThai = TrangThaiDatVe.DaXacNhan, TongTien = 2_200_000m, ThuePhi = 380_000m, PhiDichVu = 70_000m, PhiHanhLy = 250_000m, EmailLienHe = "linh@example.com", NgayXacNhan = now.AddHours(-2).AddMinutes(5) };
        db.DatVes.Add(dvLinh);
        await db.SaveChangesAsync(ct);
        db.Ves.Add(new Ve { MaVe = "738 2400012751", ChuyenBayId = vn216.Id, DatVeId = dvLinh.Id, HanhKhachId = hkLinh.Id, SoGhe = "12C", HangVe = "Phổ thông Tiêu chuẩn", GiaVe = 1_700_000m, TrangThai = TrangThaiVe.DaPhatHanh, NgayPhatHanh = now.AddHours(-2).AddMinutes(6), HanhLyXachTayKg = 12, HanhLyKyGuiKg = 33, PhiDoi = 360_000m, PhiHoan = 500_000m });
        var ttLinh = new ThanhToan { DatVeId = dvLinh.Id, SoTien = 2_200_000m, PhuongThuc = PhuongThucThanhToan.TheNganHang, NhaCungCap = "VNPay", TrangThai = TrangThaiThanhToan.ThanhCong, MaGiaoDich = "TX0111260810", NgayTao = now.AddHours(-2).AddMinutes(2), NgayHoanTat = now.AddHours(-2).AddMinutes(5) };
        db.ThanhToans.Add(ttLinh);
        await db.SaveChangesAsync(ct);

        db.YeuCauDoiHoans.Add(new YeuCauDoiHoan
        {
            MaYeuCau = "RF-1027",
            DatVeId = dvLinh.Id,
            LoaiYeuCau = LoaiYeuCau.HoanVe,
            LyDo = "Thay đổi kế hoạch cá nhân",
            TrangThai = TrangThaiYeuCau.ChoDuyet,
            PhiHoan = 500_000m,
            PhiDichVuKhongHoan = 70_000m,
            PhiHanhLyKhongHoan = 250_000m,
            SoTienHoan = 1_380_000m,
            PhuongThucHoan = "Visa •••• 1762",
            NgayTao = now.AddMinutes(-50)
        });
        db.YeuCauDoiHoans.Add(new YeuCauDoiHoan
        {
            MaYeuCau = "RF-1031",
            DatVeId = datVe.Id,
            LoaiYeuCau = LoaiYeuCau.HoanVe,
            LyDo = "Thay đổi kế hoạch cá nhân",
            TrangThai = TrangThaiYeuCau.DaDuyet,
            PhiHoan = 500_000m,
            PhiDichVuKhongHoan = 70_000m,
            PhiHanhLyKhongHoan = 250_000m,
            SoTienHoan = 1_380_000m,
            PhuongThucHoan = "Visa •••• 4242",
            NgayTao = now.AddDays(-1).AddHours(1),
            NgayXuLy = now.AddDays(-1).AddHours(4),
            NguoiXuLyId = manager.Id,
            GhiChuXuLy = "Đã tạo lệnh hoàn, chờ cổng thanh toán xác nhận."
        });
        await db.SaveChangesAsync(ct);

        // --- Audit logs ---
        db.NhatKyHoatDongs.AddRange(
            NK("LOG-20261101-0284", admin, "Admin", "Cập nhật phí dịch vụ", "CFG-102", KetQuaNhatKy.ThanhCong, "Xác nhận lại mức phí hiện hành.", now.AddMinutes(-10)),
            NK("LOG-20261101-0283", manager, "Manager", "Xuất vé điện tử", "AV8K2M", KetQuaNhatKy.ThanhCong, "Vé 738 2400012865.", now.AddMinutes(-17)),
            NK("LOG-20261101-0282", null, "Hệ thống", "Xác nhận thanh toán", "TX0111260942", KetQuaNhatKy.ThanhCong, "VNPay xác nhận thành công.", now.AddMinutes(-18), "Cổng VNPay"),
            NK("LOG-20261101-0281", customer, "Khách hàng", "Tạo đặt chỗ", "AV8K2M", KetQuaNhatKy.ThanhCong, "VN210 20/11/2026.", now.AddMinutes(-30)),
            NK("LOG-20261101-0278", locked, "Khách hàng", "Đăng nhập sai 5 lần", "TK-0426", KetQuaNhatKy.ThatBai, "Tài khoản bị khóa tạm.", now.AddHours(-2).AddMinutes(-3)));
        await db.SaveChangesAsync(ct);
    }

    private static SanBay A(string code, string name, string city, string country) => new()
        { MaSanBay = code, TenSanBay = name, ThanhPho = city, QuocGia = country, MuiGio = "UTC+7" };

    private static TuyenBay R(int di, int den, int minutes) => new()
        { SanBayDiId = di, SanBayDenId = den, ThoiLuongDuKienPhut = minutes };

    private static ChuyenBay F(string code, int airline, int aircraft, int route, DateOnly date, int h, int m, int minutes, decimal price, TrangThaiChuyenBay status = TrangThaiChuyenBay.DuKien)
    {
        var dep = new DateTimeOffset(date.ToDateTime(new TimeOnly(h, m)), Vn);
        return new ChuyenBay
        {
            SoHieu = code, HangHangKhongId = airline, MayBayId = aircraft, TuyenBayId = route,
            GioKhoiHanh = dep, GioHaCanh = dep.AddMinutes(minutes), GiaCoBan = price, TrangThai = status
        };
    }

    private static HangVe HV(int flightId, string ma, string ten, string nhom, decimal gia, int ghe, int xach, int ky, decimal doi, decimal hoan) => new()
    {
        ChuyenBayId = flightId, MaHang = ma, TenHang = ten, NhomHang = nhom, GiaCoBan = gia,
        SoGhe = ghe, HanhLyXachTayKg = xach, HanhLyKyGuiKg = ky, PhiDoi = doi, PhiHoan = hoan
    };

    private static CauHinhHeThong S(string key, string value, string group, string? desc, string? type, DateTimeOffset at) => new()
        { Khoa = key, GiaTri = value, Nhom = group, MoTa = desc, KieuDuLieu = type, NgayCapNhat = at, NguoiCapNhat = "Hệ thống" };

    private static TaiKhoan Tk(string user, string pass, string name, string email, VaiTro role, string? staffCode, DateTimeOffset created, DateTimeOffset? lastLogin, int? khId = null, string? phone = null) => new()
    {
        TenDangNhap = user, MatKhauHash = PasswordHelper.Hash(pass), HoTen = name, Email = email,
        VaiTro = role, MaNhanSu = staffCode, NgayTao = created, LanDangNhapCuoi = lastLogin,
        KhachHangId = khId, SoDienThoai = phone
    };

    private static NhatKyHoatDong NK(string ma, TaiKhoan? tk, string vaiTro, string hanhDong, string doiTuong, KetQuaNhatKy kq, string? ct, DateTimeOffset at, string? ten = null) => new()
    {
        MaNhatKy = ma, TaiKhoanId = tk?.Id, TenNguoiDung = ten ?? tk?.HoTen ?? "Hệ thống", VaiTro = vaiTro,
        HanhDong = hanhDong, DoiTuong = doiTuong, KetQua = kq, ChiTiet = ct, ThoiGian = at, DiaChiIp = "127.0.0.1"
    };
}
