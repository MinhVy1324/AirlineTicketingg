using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Tickets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AirlineTicketing.Tests;

/// <summary>
/// Kiểm tra vé điện tử sinh ra đúng định dạng và hiển thị được tiếng Việt.
/// Test chạy trên font thật trong wwwroot nên bắt được lỗi thiếu glyph (ví dụ ký tự
/// trang trí không có trong Be Vietnam Pro) trước khi phát hành.
/// </summary>
public sealed class VeDienTuPdfTests
{
    [Fact]
    public void TaoPdf_TiengVietVaThongTinDatCho_HopLe()
    {
        VeDienTuPdf.KhoiTao(TaoMoiTruongWeb());

        var booking = TaoDatVe();
        var pdf = VeDienTuPdf.Tao(booking, new ChinhSachDatVe(), DateTimeOffset.UtcNow);

        Assert.True(pdf.Length > 20_000, $"PDF quá nhỏ ({pdf.Length} byte), có thể thiếu nội dung.");
        var header = System.Text.Encoding.ASCII.GetString(pdf, 0, 8);
        Assert.StartsWith("%PDF-", header);
        Assert.Contains("/Type /Page", System.Text.Encoding.ASCII.GetString(pdf));
    }

    [Fact]
    public void TaoHtml_ChuaThongTinChuyenBayVaHotline()
    {
        var booking = TaoDatVe();
        var chinhSach = new ChinhSachDatVe { Hotline = "1900 999 888", SupportEmail = "hotro@aeroviet.vn" };

        var html = VeEmailContent.TaoHtml(booking, chinhSach);
        var text = VeEmailContent.TaoVanBan(booking, chinhSach);

        Assert.Contains("AVTEST", html);
        Assert.Contains("Nguyễn Văn Ước", html);
        Assert.Contains("SGN", html);
        // WebUtility.HtmlEncode chuyển ký tự Latin-1 (à) sang entity, phần còn lại giữ nguyên.
        Assert.Contains("H&#224; Nội", html);
        Assert.Contains("1900 999 888", html);
        Assert.Contains("Nguyễn Văn Ước", text);
        Assert.Contains("1900 999 888", text);
    }

    [Fact]
    public void EmailCuaKhach_UuTienEmailLienHe()
    {
        var booking = TaoDatVe();
        Assert.Equal("lienhe@example.com", VeEmailContent.EmailCuaKhach(booking));

        booking.EmailLienHe = null;
        Assert.Equal("chuve@example.com", VeEmailContent.EmailCuaKhach(booking));
    }

    private static DatVe TaoDatVe()
    {
        var sanBayDi = new SanBay
        {
            MaSanBay = "SGN",
            TenSanBay = "Tân Sơn Nhất",
            ThanhPho = "TP. Hồ Chí Minh",
            QuocGia = "Việt Nam"
        };
        var sanBayDen = new SanBay
        {
            MaSanBay = "HAN",
            TenSanBay = "Nội Bài",
            ThanhPho = "Hà Nội",
            QuocGia = "Việt Nam"
        };
        var chuyenBay = new ChuyenBay
        {
            SoHieu = "VN5001",
            GioKhoiHanh = new DateTimeOffset(2026, 12, 20, 1, 30, 0, TimeSpan.Zero),
            GioHaCanh = new DateTimeOffset(2026, 12, 20, 3, 40, 0, TimeSpan.Zero),
            TuyenBay = new TuyenBay
            {
                SanBayDi = sanBayDi,
                SanBayDen = sanBayDen,
                SanBayDiId = 1,
                SanBayDenId = 2
            },
            MayBay = new MayBay { MaMayBay = "A321", TenMayBay = "Airbus A321" }
        };

        return new DatVe
        {
            MaDatVe = "AVTEST",
            TrangThai = TrangThaiDatVe.DaXacNhan,
            NgayDat = new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.Zero),
            NgayXacNhan = new DateTimeOffset(2026, 11, 1, 2, 5, 0, TimeSpan.Zero),
            HetHanGiuCho = new DateTimeOffset(2026, 11, 1, 2, 15, 0, TimeSpan.Zero),
            TongTien = 2_450_000m,
            EmailLienHe = "lienhe@example.com",
            GhiChu = "Thanh toán thẻ Visa •••• 4242",
            KhachHang = new KhachHang
            {
                HoTen = "Nguyễn Văn Ước",
                Email = "chuve@example.com"
            },
            ThanhToans =
            [
                new ThanhToan
                {
                    SoTien = 2_450_000m,
                    TrangThai = TrangThaiThanhToan.ThanhCong,
                    NhaCungCap = "Cổng thanh toán AeroPay",
                    MaGiaoDich = "GD-20261101-0001",
                    NgayTao = new DateTimeOffset(2026, 11, 1, 2, 4, 0, TimeSpan.Zero),
                    NgayHoanTat = new DateTimeOffset(2026, 11, 1, 2, 5, 0, TimeSpan.Zero)
                }
            ],
            Ves =
            [
                new Ve
                {
                    MaVe = "7382400012865",
                    SoGhe = "12A",
                    HangVe = "Phổ thông linh hoạt",
                    TrangThai = TrangThaiVe.DaPhatHanh,
                    NgayPhatHanh = new DateTimeOffset(2026, 11, 1, 2, 5, 0, TimeSpan.Zero),
                    HanhLyXachTayKg = 10,
                    HanhLyKyGuiKg = 23,
                    HanhKhach = new HanhKhach { HoTen = "Nguyễn Văn Ước" },
                    ChuyenBay = chuyenBay
                }
            ]
        };
    }

    private static IWebHostEnvironment TaoMoiTruongWeb()
    {
        var wwwroot = TimWwwroot();
        return new MoiTruongWeb
        {
            ApplicationName = "AirlineTicketing.Web",
            EnvironmentName = Environments.Development,
            ContentRootPath = Path.GetDirectoryName(wwwroot.TrimEnd(Path.DirectorySeparatorChar))!,
            WebRootPath = wwwroot,
            ContentRootFileProvider = new PhysicalFileProvider(
                Path.GetDirectoryName(wwwroot.TrimEnd(Path.DirectorySeparatorChar))!),
            WebRootFileProvider = new PhysicalFileProvider(wwwroot)
        };
    }

    private static string TimWwwroot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "AirlineTicketing.Web", "wwwroot");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Không tìm thấy AirlineTicketing.Web/wwwroot để nạp font vé điện tử.");
    }

    private sealed class MoiTruongWeb : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
