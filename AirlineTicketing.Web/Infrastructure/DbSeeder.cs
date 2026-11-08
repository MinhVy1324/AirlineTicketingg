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

        var nowVn = new DateTimeOffset(2026, 11, 1, 10, 0, 0, Vn);
        var now = nowVn.ToUniversalTime();
        var rng = new Random(20261108);

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
            A("BMT", "Buôn Ma Thuột", "Đắk Lắk", "Việt Nam"),
            A("UIH", "Phù Cát", "Bình Định", "Việt Nam"),
        };
        db.SanBays.AddRange(airports);
        await db.SaveChangesAsync(ct);
        var ap = airports.ToDictionary(x => x.MaSanBay, x => x);

        // --- Airlines ---
        var airlines = new[]
        {
            H("VN", "Vietnam Airlines", "HVN"),
            H("VJ", "Vietjet Air", "VJC"),
            H("QH", "Bamboo Airways", "BAV"),
            H("BL", "Pacific Airlines", "PIC"),
            H("VU", "Vietravel Airlines", "VAG"),
        };
        db.HangHangKhongS.AddRange(airlines);
        await db.SaveChangesAsync(ct);
        var al = airlines.ToDictionary(x => x.MaHang, x => x);

        // --- Aircraft ---
        var aircraft = new[]
        {
            M("VN-A321", "Airbus A321", 184),
            M("VN-A350", "Airbus A350", 305),
            M("VJ-A320", "Airbus A320", 180),
            M("VJ-A321N", "Airbus A321neo", 230),
            M("QH-B789", "Boeing 787-9", 294),
            M("BL-A320", "Airbus A320", 180),
            M("VU-A321", "Airbus A321", 220),
            M("VN-ATR72", "ATR 72-500", 68),
        };
        db.MayBays.AddRange(aircraft);
        await db.SaveChangesAsync(ct);
        var ac = aircraft.ToDictionary(x => x.MaMayBay, x => x);

        // --- Routes (both directions for key pairs) ---
        var routeDefs = new (string di, string den, int km)[]
        {
            ("SGN","HAN",1160), ("HAN","SGN",1160),
            ("SGN","DAD",610), ("DAD","SGN",610),
            ("HAN","DAD",630), ("DAD","HAN",630),
            ("SGN","PQC",300), ("PQC","SGN",300),
            ("SGN","CXR",310), ("CXR","SGN",310),
            ("HAN","PQC",1220), ("PQC","HAN",1220),
            ("SGN","DLI",230), ("DLI","SGN",230),
            ("SGN","HUI",670), ("HUI","SGN",670),
            ("HAN","HUI",540), ("HUI","HAN",540),
            ("SGN","VCA",170), ("VCA","SGN",170),
            ("HAN","VII",290), ("VII","HAN",290),
            ("SGN","BMT",350), ("BMT","SGN",350),
            ("SGN","UIH",430), ("UIH","SGN",430),
            ("HAN","HPH",100), ("HPH","HAN",100),
        };
        var routes = routeDefs.Select(r => new TuyenBay { SanBayDiId = ap[r.di].Id, SanBayDenId = ap[r.den].Id, ThoiLuongDuKienPhut = r.km <= 350 ? 75 : r.km <= 700 ? 95 : 130, DangKhaiThac = true }).ToList();
        db.TuyenBays.AddRange(routes);
        await db.SaveChangesAsync(ct);
        var rt = routeDefs.Zip(routes, (d, r) => (key: $"{d.di}-{d.den}", route: r)).ToDictionary(x => x.key, x => x.route);

        // --- Flights (many dates/routes/times) ---
        var flights = new List<ChuyenBay>();
        var baseDay = new DateTimeOffset(2026, 10, 25, 0, 0, 0, Vn); // include past+upcoming
        var airlineCycle = new[] { ("VN","VN-A321"), ("VJ","VJ-A320"), ("QH","QH-B789"), ("VN","VN-A350"), ("VJ","VJ-A321N"), ("BL","BL-A320"), ("VU","VU-A321") };
        var timeSlots = new[] { (h:6,m:0,dur:125), (h:8,m:30,dur:125), (h:11,m:0,dur:125), (h:14,m:15,dur:125), (h:17,m:40,dur:125), (h:20,m:10,dur:125) };
        int fIdx = 0;
        foreach (var (di, den, km) in routeDefs.Where(r => string.Compare(r.di, r.den, StringComparison.Ordinal) < 0)) // one direction seed, add both
        {
            foreach (var dir in new[] { (di, den), (den, di) })
            {
                var maTuyen = $"{dir.Item1}-{dir.Item2}";
                if (!rt.ContainsKey(maTuyen)) continue;
                var dur = km <= 350 ? 75 : km <= 700 ? 95 : 130;
                for (int day = 0; day < 21; day++) // 3 weeks of flights
                {
                    int slotsPerDay = (dir.Item1 is "SGN" or "HAN") && (dir.Item2 is "SGN" or "HAN" or "DAD") ? 4 : 2;
                    for (int s = 0; s < slotsPerDay; s++)
                    {
                        var slot = timeSlots[(fIdx + s) % timeSlots.Length];
                        var (alCode, acCode) = airlineCycle[(fIdx + day + s) % airlineCycle.Length];
                        var dep = baseDay.AddDays(day).AddHours(slot.h).AddMinutes(slot.m + (fIdx % 3) * 5);
                        var arr = dep.AddMinutes(dur + (s % 2 == 0 ? 0 : 10));
                        var basePrice = km <= 350 ? 950000m : km <= 700 ? 1350000m : 1650000m;
                        basePrice += (s % 3) * 150000m + (day % 5) * 50000m;
                        var depUtc = dep.ToUniversalTime();
                        var arrUtc = arr.ToUniversalTime();
                        var status = depUtc < now.AddHours(-3) ? TrangThaiChuyenBay.HoanTat
                            : depUtc < now ? TrangThaiChuyenBay.DangBay
                            : TrangThaiChuyenBay.DuKien;
                        var soHieu = $"{alCode}{1000 + fIdx}";
                        flights.Add(new ChuyenBay
                        {
                            SoHieu = soHieu,
                            TuyenBayId = rt[maTuyen].Id,
                            MayBayId = ac.ContainsKey(acCode) ? ac[acCode].Id : ac.Values.First().Id,
                            HangHangKhongId = al[alCode].Id,
                            GioKhoiHanh = depUtc,
                            GioHaCanh = arrUtc,
                            GiaCoBan = basePrice,
                            TrangThai = status
                        });
                        fIdx++;
                    }
                }
            }
        }
        db.ChuyenBays.AddRange(flights);
        await db.SaveChangesAsync(ct);

        // --- Fare classes for every flight (2 classes) ---
        var fareClasses = new List<HangVe>(flights.Count * 2);
        foreach (var f in flights)
        {
            var cap = ac.Values.FirstOrDefault(a => a.Id == f.MayBayId)?.SoGhe ?? 180;
            int eco = Math.Max(20, cap - 16);
            int biz = Math.Min(16, Math.Max(4, cap - eco));
            fareClasses.Add(new HangVe { ChuyenBayId = f.Id, MaHang = "ECO", TenHang = "Phổ thông Tiết kiệm", NhomHang = "Phổ thông", GiaCoBan = f.GiaCoBan, SoGhe = eco, SoGheDaBan = Math.Min(eco - 5, rng.Next(10, eco)), HanhLyXachTayKg = 7, HanhLyKyGuiKg = 0, PhiDoi = 360000m, PhiHoan = 500000m, DangMoBan = true });
            fareClasses.Add(new HangVe { ChuyenBayId = f.Id, MaHang = "STD", TenHang = "Phổ thông Tiêu chuẩn", NhomHang = "Phổ thông", GiaCoBan = f.GiaCoBan + 300000m, SoGhe = biz, SoGheDaBan = Math.Min(biz, rng.Next(0, biz + 1)), HanhLyXachTayKg = 10, HanhLyKyGuiKg = 23, PhiDoi = 200000m, PhiHoan = 350000m, DangMoBan = true });
        }
        db.HangVes.AddRange(fareClasses);
        await db.SaveChangesAsync(ct);

        // --- Settings ---
        db.CauHinhHeThongs.AddRange(
            C("booking.hold_minutes", "15", "Booking", "Thời gian giữ chỗ (phút)", now),
            C("booking.service_fee", "70000", "Booking", "Phí dịch vụ mặc định (VND)", now),
            C("booking.tax_default", "380000", "Booking", "Thuế/phí mặc định mỗi khách (VND)", now),
            C("policy.change_fee", "360000", "Policy", "Phí đổi vé mặc định (VND)", now),
            C("policy.refund_fee", "500000", "Policy", "Phí hoàn vé mặc định (VND)", now),
            C("contact.hotline", "1900 636 063", "Contact", "Hotline hỗ trợ", now),
            C("contact.email", "support@aeroviet.vn", "Contact", "Email hỗ trợ", now)
        );

        // --- Accounts ---
        var accounts = new[]
        {
            TK("admin", "admin@aeroviet.vn", "Trần Hoàng Tùng", VaiTro.Admin, "NV-001", TrangThaiTaiKhoan.HoatDong, now),
            TK("manager", "long.manager@example.com", "Lê Quốc Long", VaiTro.Manager, "NV-002", TrangThaiTaiKhoan.HoatDong, now),
            TK("manager.ha", "ha.manager@example.com", "Phạm Thanh Hà", VaiTro.Manager, "NV-003", TrangThaiTaiKhoan.HoatDong, now),
            TK("manager.minh", "minh.manager@example.com", "Nguyễn Văn Minh", VaiTro.Manager, "NV-004", TrangThaiTaiKhoan.HoatDong, now),
            TK("minhanh", "minhanh@example.com", "Nguyễn Minh Anh", VaiTro.KhachHang, null, TrangThaiTaiKhoan.HoatDong, now),
            TK("nam.user", "nam.user@example.com", "Đỗ Đức Nam", VaiTro.KhachHang, null, TrangThaiTaiKhoan.BiKhoa, now),
            TK("linh.tran", "linh.tran@example.com", "Trần Mai Linh", VaiTro.KhachHang, null, TrangThaiTaiKhoan.HoatDong, now),
            TK("bao.ngoc", "baongoc@example.com", "Trần Bảo Ngọc", VaiTro.KhachHang, null, TrangThaiTaiKhoan.HoatDong, now),
            TK("hai.dang", "haidang@example.com", "Phạm Hải Đăng", VaiTro.KhachHang, null, TrangThaiTaiKhoan.HoatDong, now),
            TK("thu.ha", "thuha@example.com", "Đặng Thu Hà", VaiTro.KhachHang, null, TrangThaiTaiKhoan.HoatDong, now),
            TK("tuan.kiet", "tuankiet@example.com", "Hoàng Tuấn Kiệt", VaiTro.KhachHang, null, TrangThaiTaiKhoan.HoatDong, now),
            TK("hoai.an", "hoaian@example.com", "Phạm Hoài An", VaiTro.KhachHang, null, TrangThaiTaiKhoan.HoatDong, now),
        };
        db.TaiKhoans.AddRange(accounts);
        await db.SaveChangesAsync(ct);

        // --- Customers ---
        var customers = new[]
        {
            K("Nguyễn Minh Anh", "minhanh@example.com", "0912345678", now),
            K("Trần Mai Linh", "linh.tran@example.com", "0987654321", now),
            K("Đỗ Đức Nam", "nam.user@example.com", "0900001122", now),
            K("Lê Quốc Bảo", "bao.le@example.com", "0911223344", now),
            K("Phạm Thu Hà", "ha.pham@example.com", "0933445566", now),
            K("Trần Bảo Ngọc", "baongoc@example.com", "0944556677", now),
            K("Phạm Hải Đăng", "haidang@example.com", "0955667788", now),
            K("Đặng Thu Hà", "thuha@example.com", "0966778899", now),
            K("Hoàng Tuấn Kiệt", "tuankiet@example.com", "0977889900", now),
            K("Phạm Hoài An", "hoaian@example.com", "0988990011", now),
            K("Vũ Thanh Tâm", "thanhtam@example.com", "0911002200", now),
            K("Ngô Gia Hân", "giahan@example.com", "0922003300", now),
        };
        db.KhachHangs.AddRange(customers);
        await db.SaveChangesAsync(ct);

        // --- Passengers ---
        var paxDefs = new (string name, string nat, string? pp, DateOnly? dob)[]
        {
            ("NGUYEN MINH ANH","Việt Nam","079098001234", new DateOnly(1998,8,15)),
            ("TRAN MAI LINH","Việt Nam","079099005678", new DateOnly(1995,3,22)),
            ("DO DUC NAM","Việt Nam","079097009999", new DateOnly(1992,12,1)),
            ("LE QUOC BAO","Việt Nam",null, new DateOnly(2000,5,10)),
            ("PHAM THU HA","Việt Nam","079096001111", new DateOnly(1997,7,7)),
            ("TRAN BAO NGOC","Việt Nam","079095002222", new DateOnly(1999,1,19)),
            ("PHAM HAI DANG","Việt Nam","079094003333", new DateOnly(1996,11,30)),
            ("DANG THU HA","Việt Nam","079093004444", new DateOnly(1994,4,25)),
            ("HOANG TUAN KIET","Việt Nam","079092005555", new DateOnly(1993,9,9)),
            ("PHAM HOAI AN","Việt Nam","079091006666", new DateOnly(2001,6,14)),
            ("VU THANH TAM","Việt Nam","079090007777", new DateOnly(1990,2,28)),
            ("NGO GIA HAN","Việt Nam","079089008888", new DateOnly(2002,10,5)),
        };
        var passengers = paxDefs.Select(p => new HanhKhach { HoTen = p.name, QuocTich = p.nat, SoHoChieu = p.pp, NgaySinh = p.dob }).ToList();
        db.HanhKhachs.AddRange(passengers);
        await db.SaveChangesAsync(ct);

        // pick upcoming & past flights for bookings
        var upcomingFlights = flights.Where(f => f.GioKhoiHanh > now.AddHours(6)).OrderBy(f => f.GioKhoiHanh).Take(80).ToList();
        var pastFlights = flights.Where(f => f.GioHaCanh < now).OrderByDescending(f => f.GioHaCanh).Take(40).ToList();
        var flightPool = upcomingFlights.Concat(pastFlights).ToList();
        if (flightPool.Count == 0) flightPool = flights.Take(20).ToList();

        // --- Bookings + tickets + payments ---
        var bookings = new List<DatVe>();
        var tickets = new List<Ve>();
        var payments = new List<ThanhToan>();
        var bookingStates = new[] { TrangThaiDatVe.DaXacNhan, TrangThaiDatVe.DaXacNhan, TrangThaiDatVe.ChoThanhToan, TrangThaiDatVe.DaHuy, TrangThaiDatVe.HetHan, TrangThaiDatVe.DaXacNhan };
        for (int i = 0; i < 36; i++)
        {
            var cust = customers[i % customers.Length];
            var pax = passengers[i % passengers.Count];
            var fl = flightPool[i % flightPool.Count];
            var state = bookingStates[i % bookingStates.Length];
            var maDatVe = $"AV{(100000 + i * 37):D6}".Substring(0, 8).Replace("AV10", "AV") + ((i % 26) + 'A');
            // ensure 6 chars-ish alnum PNR
            maDatVe = $"AV{(char)('A' + (i % 26))}{(i % 10)}{(char)('K' + (i % 10))}{(i % 9 + 1)}{(char)('M' - (i % 10))}";
            var total = fl.GiaCoBan + 380000m + 70000m + (i % 3 == 0 ? 250000m : 0m);
            var dv = new DatVe
            {
                MaDatVe = maDatVe,
                KhachHangId = cust.Id,
                NgayDat = now.AddDays(- (i % 14)).AddMinutes(-i * 7),
                TrangThai = state,
                TongTien = total,
                DonViTienTe = "VND",
                HanGiuCho = state == TrangThaiDatVe.ChoThanhToan ? now.AddMinutes(15) : null,
                NgayXacNhan = state == TrangThaiDatVe.DaXacNhan ? now.AddDays(-(i % 10)) : null,
                ThuePhi = 380000m,
                PhiDichVu = 70000m,
                PhiHanhLy = i % 3 == 0 ? 250000m : 0m,
                EmailLienHe = cust.Email,
                SoDienThoaiLienHe = cust.SoDienThoai,
                GhiChu = i % 5 == 0 ? "Yêu cầu ghế cạnh cửa sổ" : null
            };
            bookings.Add(dv);
        }
        db.DatVes.AddRange(bookings);
        await db.SaveChangesAsync(ct);

        for (int i = 0; i < bookings.Count; i++)
        {
            var dv = bookings[i];
            var fl = flightPool[i % flightPool.Count];
            var pax = passengers[i % passengers.Count];
            var veState = dv.TrangThai == TrangThaiDatVe.DaXacNhan ? TrangThaiVe.DaPhatHanh
                : dv.TrangThai == TrangThaiDatVe.DaHuy ? TrangThaiVe.DaHuy
                : dv.TrangThai == TrangThaiDatVe.HetHan ? TrangThaiVe.DaHuy
                : TrangThaiVe.TamGiu;
            // MaVe is unique (IX_Ves_MaVe). Use a placeholder for non-issued tickets.
            var maVe = veState == TrangThaiVe.DaPhatHanh ? $"738 24{(10000000 + i * 913):D8}" : $"TMP{i + 1:D10}";
            tickets.Add(new Ve
            {
                MaVe = maVe,
                ChuyenBayId = fl.Id,
                DatVeId = dv.Id,
                HanhKhachId = pax.Id,
                SoGhe = veState == TrangThaiVe.DaPhatHanh ? $"{12 + (i % 18)}{(char)('A' + (i % 6))}" : $"W{i:D4}",
                HangVe = i % 4 == 0 ? "Thương gia" : "Phổ thông",
                GiaVe = fl.GiaCoBan + (i % 4 == 0 ? 800000m : 300000m),
                TrangThai = veState,
                NgayPhatHanh = veState == TrangThaiVe.DaPhatHanh ? dv.NgayXacNhan : null,
                HanhLyXachTayKg = 10,
                HanhLyKyGuiKg = i % 4 == 0 ? 32 : 23,
                PhiDoi = 360000m,
                PhiHoan = 500000m
            });
        }
        db.Ves.AddRange(tickets);
        await db.SaveChangesAsync(ct);

        for (int i = 0; i < bookings.Count; i++)
        {
            var dv = bookings[i];
            if (dv.TrangThai == TrangThaiDatVe.DaXacNhan)
            {
                payments.Add(new ThanhToan { DatVeId = dv.Id, SoTien = dv.TongTien, DonViTienTe = "VND", PhuongThuc = (i % 2 == 0 ? PhuongThucThanhToan.TheNganHang : PhuongThucThanhToan.ViDienTu), NhaCungCap = "VNPAY", TrangThai = TrangThaiThanhToan.ThanhCong, MaGiaoDich = $"TX{2026110000 + i}", NgayTao = dv.NgayDat, NgayHoanTat = dv.NgayDat.AddMinutes(2) });
            }
            else if (dv.TrangThai == TrangThaiDatVe.ChoThanhToan)
            {
                payments.Add(new ThanhToan { DatVeId = dv.Id, SoTien = dv.TongTien, DonViTienTe = "VND", PhuongThuc = PhuongThucThanhToan.TheNganHang, NhaCungCap = "VNPAY", TrangThai = TrangThaiThanhToan.ChoXuLy, MaGiaoDich = $"TX{2026110000 + i}", NgayTao = dv.NgayDat });
            }
            else if (dv.TrangThai == TrangThaiDatVe.DaHuy && i % 2 == 0)
            {
                payments.Add(new ThanhToan { DatVeId = dv.Id, SoTien = dv.TongTien, DonViTienTe = "VND", PhuongThuc = PhuongThucThanhToan.TheNganHang, NhaCungCap = "VNPAY", TrangThai = TrangThaiThanhToan.ThanhCong, MaGiaoDich = $"TX{2026110000 + i}", NgayTao = dv.NgayDat, NgayHoanTat = dv.NgayDat.AddMinutes(2) });
            }
        }
        db.ThanhToans.AddRange(payments);

        // --- Change/refund requests ---
        var manager = accounts.FirstOrDefault(a => a.VaiTro == VaiTro.Manager);
        var reqs = new List<YeuCauDoiHoan>();
        var confirmedBookings = bookings.Where(b => b.TrangThai == TrangThaiDatVe.DaXacNhan).Take(10).ToList();
        for (int i = 0; i < Math.Min(8, confirmedBookings.Count); i++)
        {
            var dv = confirmedBookings[i];
            bool isRefund = i % 2 == 0;
            var status = i switch { 0 => TrangThaiYeuCau.ChoDuyet, 1 => TrangThaiYeuCau.DaDuyet, 2 => TrangThaiYeuCau.DaDuyet, 3 => TrangThaiYeuCau.HoanTat, 4 => TrangThaiYeuCau.TuChoi, _ => TrangThaiYeuCau.ChoDuyet };
            var soTienHoan = isRefund ? Math.Max(0, dv.TongTien - 500000m - 70000m - dv.PhiHanhLy) : 0m;
            reqs.Add(new YeuCauDoiHoan
            {
                MaYeuCau = isRefund ? $"RF-{1030 + i}" : $"CH-{1030 + i}",
                DatVeId = dv.Id,
                LoaiYeuCau = isRefund ? LoaiYeuCau.HoanVe : LoaiYeuCau.DoiVe,
                LyDo = isRefund ? "Thay đổi kế hoạch cá nhân" : "Muốn đổi sang chuyến khác",
                ThongTinBoSung = isRefund ? "Không còn nhu cầu thực hiện hành trình." : "Ưu tiên chuyến buổi chiều.",
                TrangThai = status,
                PhiDoi = isRefund ? 0m : 360000m,
                PhiHoan = isRefund ? 500000m : 0m,
                PhiDichVuKhongHoan = 70000m,
                PhiHanhLyKhongHoan = dv.PhiHanhLy,
                SoTienHoan = status is TrangThaiYeuCau.DaDuyet or TrangThaiYeuCau.DaDuyet or TrangThaiYeuCau.HoanTat ? soTienHoan : 0m,
                PhuongThucHoan = "Visa ****4242",
                ChuyenBayMoiId = !isRefund ? flightPool[(i + 3) % flightPool.Count].Id : null,
                NgayTao = now.AddDays(-(i + 1)),
                NgayXuLy = status != TrangThaiYeuCau.ChoDuyet ? now.AddDays(-i) : null,
                NguoiXuLyId = status != TrangThaiYeuCau.ChoDuyet ? manager?.Id : null,
                GhiChuXuLy = status == TrangThaiYeuCau.TuChoi ? "Không đủ điều kiện theo chính sách vé." : (status != TrangThaiYeuCau.ChoDuyet ? "Đã kiểm tra điều kiện vé." : null)
            });
        }
        db.YeuCauDoiHoans.AddRange(reqs);

        // --- Audit logs ---
        var audits = new List<NhatKyHoatDong>();
        for (int i = 0; i < 60; i++)
        {
            var acc = accounts[i % accounts.Length];
            var isFail = i % 17 == 16;
            audits.Add(new NhatKyHoatDong
            {
                MaNhatKy = $"LOG-{20261101:00000000}-{(284 - i):0000}",
                TaiKhoanId = acc.Id,
                TenNguoiDung = acc.HoTen,
                VaiTro = acc.VaiTro.ToString(),
                HanhDong = (i % 6) switch { 0 => "Cập nhật cấu hình", 1 => "Xuất vé điện tử", 2 => "Xác nhận thanh toán", 3 => "Tạo đặt chỗ", 4 => "Đăng nhập hệ thống", _ => "Xử lý yêu cầu đổi/hoàn" },
                DoiTuong = (i % 6) switch { 0 => $"CFG-{100 + i}", 1 => bookings[i % bookings.Count].MaDatVe, 2 => payments.Count > 0 ? (payments[i % payments.Count].MaGiaoDich ?? "TX") : "TX", 3 => bookings[i % bookings.Count].MaDatVe, 4 => acc.Email, _ => reqs.Count > 0 ? reqs[i % reqs.Count].MaYeuCau : "RF" },
                KetQua = isFail ? KetQuaNhatKy.ThatBai : KetQuaNhatKy.ThanhCong,
                ChiTiet = isFail ? "Thao tác bị từ chối do không đủ quyền hoặc dữ liệu không hợp lệ." : "Thao tác hoàn tất.",
                DiaChiIp = $"10.10.1.{20 + (i % 30)}",
                ThoiGian = now.AddMinutes(-(i * 11))
            });
        }
        db.NhatKyHoatDongs.AddRange(audits);

        await db.SaveChangesAsync(ct);
    }

    private static SanBay A(string ma, string ten, string tp, string qg) => new()
        { MaSanBay = ma, TenSanBay = ten, ThanhPho = tp, QuocGia = qg };
    private static HangHangKhong H(string ma, string ten, string? icao) => new()
        { MaHang = ma, TenHang = ten, MaIcao = icao, QuocGia = "Việt Nam", DangHoatDong = true };
    private static MayBay M(string ma, string ten, int ghe) => new()
        { MaMayBay = ma, TenMayBay = ten, HangSanXuat = ten.Split(' ')[0], SoGhe = ghe, DangHoatDong = true };
    private static CauHinhHeThong C(string khoa, string gt, string nhom, string? moTa, DateTimeOffset t) => new()
        { Khoa = khoa, GiaTri = gt, Nhom = nhom, MoTa = moTa, NgayCapNhat = t, NguoiCapNhat = "seed" };
    private static TaiKhoan TK(string user, string email, string name, VaiTro role, string? maNv, TrangThaiTaiKhoan st, DateTimeOffset t) => new()
        { TenDangNhap = user, MatKhauHash = PasswordHelper.Hash("123456"), HoTen = name, Email = email, VaiTro = role, TrangThai = st, MaNhanSu = maNv, NgayTao = t, LanDangNhapCuoi = t };
    private static KhachHang K(string name, string email, string phone, DateTimeOffset t) => new()
        { HoTen = name, Email = email, SoDienThoai = phone, NgayTao = t };
}
