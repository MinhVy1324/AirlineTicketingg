using AirlineTicketing.BUS;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Payments;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AirlineTicketing.Web.Infrastructure.Tickets;

/// <summary>
/// Dựng vé điện tử PDF từ dữ liệu đặt chỗ thật. Font Be Vietnam Pro được nhúng
/// sẵn trong wwwroot để tiếng Việt hiển thị đúng trên cả Windows lẫn container Linux.
/// </summary>
public static class VeDienTuPdf
{
    public const string FontFamily = "Be Vietnam Pro";

    private static readonly string[] FontFiles =
    [
        "BeVietnamPro-Regular.ttf",
        "BeVietnamPro-SemiBold.ttf",
        "BeVietnamPro-Bold.ttf"
    ];

    private const string MauChinh = "#1655A1";
    private const string MauNavy = "#102E50";
    private const string MauChu = "#172D46";
    private const string MauMo = "#64758A";
    private const string MauVien = "#DFE6EE";
    private const string MauNen = "#F4F7FB";
    private const string MauThanhCong = "#087F83";
    private const string MauThanhCongNen = "#E7F5F2";

    private static readonly object KhoaKhoiTao = new();
    private static bool _daKhoiTao;

    public static void KhoiTao(IWebHostEnvironment environment)
    {
        if (_daKhoiTao) return;

        lock (KhoaKhoiTao)
        {
            if (_daKhoiTao) return;

            QuestPDF.Settings.License = LicenseType.Community;

            foreach (var fileName in FontFiles)
            {
                var file = environment.WebRootFileProvider.GetFileInfo($"fonts/ticket/{fileName}");
                if (!file.Exists) continue;

                using var stream = file.CreateReadStream();
                FontManager.RegisterFontFromStream(stream);
            }

            _daKhoiTao = true;
        }
    }

    public static byte[] Tao(DatVe booking, ChinhSachDatVe chinhSach, DateTimeOffset now)
    {
        var veDau = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var chuyenBay = veDau?.ChuyenBay;

        var khoiHanh = chuyenBay is null
            ? now
            : FlightMapping.ToVietnamTime(chuyenBay.GioKhoiHanh);
        var haCanh = chuyenBay is null
            ? now
            : FlightMapping.ToVietnamTime(chuyenBay.GioHaCanh);
        var thoiLuong = chuyenBay is null
            ? "—"
            : Money.Duration(chuyenBay.GioHaCanh - chuyenBay.GioKhoiHanh);

        var ticketPayments = booking.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong && !VnpayBookingService.IsRefundOnly(x)).ToArray();
        var thanhToan = ticketPayments
            .OrderByDescending(x => x.NgayHoanTat ?? x.NgayTao)
            .FirstOrDefault();
        var daThanhToan = ticketPayments.Sum(x => x.SoTien);
        if (daThanhToan <= 0) daThanhToan = booking.TongTien;

        var phatHanh = veDau?.NgayPhatHanh ?? booking.NgayXacNhan ?? now;
        var phatHanhVietNam = FlightMapping.ToVietnamTime(phatHanh);
        var maGiaoDich = thanhToan?.MaGiaoDich ?? "—";
        var phuongThuc = thanhToan?.NhaCungCap ?? booking.GhiChu ?? "Thanh toán nội bộ";

        var maQr = $"AEROVIET|{booking.MaDatVe}|{chuyenBay?.SoHieu}|" +
                   $"{khoiHanh:yyyyMMddHHmm}|{veDau?.SoGhe}|{veDau?.MaVe}";
        var qrBytes = TaoQr(maQr);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(style => style
                    .FontFamily(FontFamily)
                    .FontSize(9.5f)
                    .FontColor(MauChu)
                    .LineHeight(1.35f));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text("AEROVIET")
                                .FontSize(22).Bold().FontColor(MauChinh).LetterSpacing(0.06f);
                            column.Item().Text("Hãng hàng không AeroViet · Vé điện tử")
                                .FontSize(8.5f).FontColor(MauMo);
                        });

                        row.ConstantItem(180).AlignRight().Column(column =>
                        {
                            column.Item().AlignRight().Text("VÉ ĐIỆN TỬ / E-TICKET")
                                .FontSize(10.5f).SemiBold().FontColor(MauNavy);
                            column.Item().AlignRight()
                                .Text($"Xuất vé {phatHanhVietNam:dd/MM/yyyy · HH:mm} (UTC+7)")
                                .FontSize(8f).FontColor(MauMo);
                        });
                    });

                    header.Item().PaddingTop(10).LineHorizontal(1.2f).LineColor(MauChinh);
                });

                page.Content().PaddingVertical(12).Column(content =>
                {
                    content.Item().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text("MÃ ĐẶT CHỖ (PNR)")
                                .FontSize(7.5f).FontColor(MauMo).LetterSpacing(0.08f);
                            column.Item().Text(booking.MaDatVe)
                                .FontSize(26).Bold().FontColor(MauNavy).LetterSpacing(0.04f);
                        });

                        row.ConstantItem(150).AlignRight().AlignMiddle().Element(box =>
                            box.Background(MauThanhCongNen).PaddingVertical(6).PaddingHorizontal(10)
                                .Text("ĐÃ XUẤT VÉ")
                                .FontSize(8f).SemiBold().FontColor(MauThanhCong).LetterSpacing(0.06f));
                    });

                    content.Item().PaddingTop(14).Background(MauNen).Padding(14).Column(panel =>
                    {
                        panel.Item().Row(row =>
                        {
                            row.RelativeItem().Column(column =>
                            {
                                column.Item().Text(chuyenBay?.TuyenBay?.SanBayDi?.MaSanBay ?? "—")
                                    .FontSize(22).Bold().FontColor(MauNavy);
                                column.Item().Text(chuyenBay?.TuyenBay?.SanBayDi?.ThanhPho ?? "")
                                    .FontSize(8f).FontColor(MauMo);
                                column.Item().PaddingTop(4).Text($"{khoiHanh:HH:mm}")
                                    .FontSize(13).SemiBold().FontColor(MauNavy);
                            });

                            row.RelativeItem().AlignCenter().Column(column =>
                            {
                                column.Item().AlignCenter().Text(thoiLuong)
                                    .FontSize(7.5f).FontColor(MauMo);
                                column.Item().AlignCenter().PaddingVertical(5).Width(64)
                                    .LineHorizontal(0.8f).LineColor(MauVien);
                                column.Item().AlignCenter().Text(chuyenBay?.SoHieu ?? "—")
                                    .FontSize(10f).SemiBold().FontColor(MauChinh);
                            });

                            row.RelativeItem().AlignRight().Column(column =>
                            {
                                column.Item().AlignRight()
                                    .Text(chuyenBay?.TuyenBay?.SanBayDen?.MaSanBay ?? "—")
                                    .FontSize(22).Bold().FontColor(MauNavy);
                                column.Item().AlignRight()
                                    .Text(chuyenBay?.TuyenBay?.SanBayDen?.ThanhPho ?? "")
                                    .FontSize(8f).FontColor(MauMo);
                                column.Item().AlignRight().PaddingTop(4).Text($"{haCanh:HH:mm}")
                                    .FontSize(13).SemiBold().FontColor(MauNavy);
                            });
                        });

                        panel.Item().PaddingTop(8).LineHorizontal(0.8f).LineColor(MauVien);

                        panel.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Text(Money.LongDate(khoiHanh))
                                .FontSize(9f).SemiBold().FontColor(MauChu);
                            row.RelativeItem().AlignRight()
                                .Text($"{FlightMapping.AirlineNameFor(chuyenBay?.SoHieu ?? "")} · " +
                                      $"{chuyenBay?.MayBay?.TenMayBay ?? "—"} · {veDau?.HangVe ?? "Phổ thông"}")
                                .FontSize(8f).FontColor(MauMo);
                        });
                    });

                    content.Item().PaddingTop(14).Text("HÀNH KHÁCH")
                        .FontSize(7.5f).FontColor(MauMo).LetterSpacing(0.08f);

                    content.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.ConstantColumn(52);
                            columns.ConstantColumn(78);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(MauNavy).Padding(6)
                                .Text("HỌ VÀ TÊN").FontSize(7.5f).SemiBold().FontColor("#FFFFFF");
                            header.Cell().Background(MauNavy).Padding(6)
                                .Text("GHẾ").FontSize(7.5f).SemiBold().FontColor("#FFFFFF");
                            header.Cell().Background(MauNavy).Padding(6)
                                .Text("HẠNG VÉ").FontSize(7.5f).SemiBold().FontColor("#FFFFFF");
                            header.Cell().Background(MauNavy).Padding(6)
                                .Text("SỐ VÉ ĐIỆN TỬ").FontSize(7.5f).SemiBold().FontColor("#FFFFFF");
                        });

                        foreach (var ve in booking.Ves.OrderBy(x => x.Id))
                        {
                            table.Cell().BorderBottom(0.6f).BorderColor(MauVien).Padding(6)
                                .Text(JourneyTicketText.Person(ve) + "\n" + JourneyTicketText.Flight(ve) + $"\nHành lý: {ve.HanhLyXachTayKg} kg xách tay + {ve.HanhLyKyGuiKg} kg ký gửi").FontSize(8f);
                            table.Cell().BorderBottom(0.6f).BorderColor(MauVien).Padding(6)
                                .Text(JourneyTicketText.Seat(booking, ve)).FontSize(8f);
                            table.Cell().BorderBottom(0.6f).BorderColor(MauVien).Padding(6)
                                .Text(JourneyBookingBuilder.CabinOf(ve.HangVe).Name).FontSize(8.5f);
                            table.Cell().BorderBottom(0.6f).BorderColor(MauVien).Padding(6)
                                .Text(ve.MaVe).FontSize(8.5f).FontColor(MauNavy);
                        }
                    });

                    content.Item().PaddingTop(14).Row(row =>
                    {
                        row.RelativeItem(3).Column(column =>
                        {
                            column.Item().Text("THANH TOÁN")
                                .FontSize(7.5f).FontColor(MauMo).LetterSpacing(0.08f);

                            column.Item().PaddingTop(4).Row(line =>
                            {
                                line.RelativeItem().Text("Tổng đã thanh toán").FontSize(9f).FontColor(MauMo);
                                line.RelativeItem().AlignRight().Text(Money.Vnd(daThanhToan))
                                    .FontSize(12f).Bold().FontColor(MauThanhCong);
                            });

                            column.Item().PaddingTop(4).Row(line =>
                            {
                                line.RelativeItem().Text("Phương thức").FontSize(8.5f).FontColor(MauMo);
                                line.RelativeItem().AlignRight().Text(phuongThuc)
                                    .FontSize(8.5f).FontColor(MauChu);
                            });

                            column.Item().PaddingTop(3).Row(line =>
                            {
                                line.RelativeItem().Text("Mã giao dịch").FontSize(8.5f).FontColor(MauMo);
                                line.RelativeItem().AlignRight().Text(maGiaoDich)
                                    .FontSize(8.5f).FontColor(MauChu);
                            });

                            if (thanhToan?.NgayHoanTat is { } hoanTat)
                            {
                                column.Item().PaddingTop(3).Row(line =>
                                {
                                    line.RelativeItem().Text("Thời điểm").FontSize(8.5f).FontColor(MauMo);
                                    line.RelativeItem().AlignRight()
                                        .Text(FlightMapping.ToVietnamTime(hoanTat)
                                            .ToString("dd/MM/yyyy · HH:mm"))
                                        .FontSize(8.5f).FontColor(MauChu);
                                });
                            }

                            column.Item().PaddingTop(6).Text(
                                    $"Hành lý: {veDau?.HanhLyXachTayKg ?? 10} kg xách tay · " +
                                    $"{veDau?.HanhLyKyGuiKg ?? 23} kg ký gửi mỗi hành khách.")
                                .FontSize(8f).FontColor(MauMo);
                        });

                        row.ConstantItem(24);

                        row.RelativeItem(2).AlignRight().Column(column =>
                        {
                            column.Item().AlignRight().Width(88).Image(qrBytes).FitWidth();
                            column.Item().AlignRight().PaddingTop(4)
                                .Text("Quét mã để tra cứu đặt chỗ")
                                .FontSize(7f).FontColor(MauMo);
                        });
                    });

                    content.Item().PaddingTop(14).Background(MauNen).Padding(12).Column(note =>
                    {
                        note.Item().Text("Lưu ý khi đi máy bay")
                            .FontSize(8.5f).SemiBold().FontColor(MauNavy);
                        note.Item().PaddingTop(3).Text(
                                "• Có mặt tại sân bay trước giờ khởi hành 2 tiếng và mang giấy tờ tùy thân " +
                                "trùng với thông tin trên vé.\n" +
                                "• Vé điện tử không thay thế thẻ lên máy bay; vui lòng làm thủ tục tại quầy " +
                                "hoặc trực tuyến.\n" +
                                $"• Đổi vé: phí {Money.Vnd(veDau?.PhiDoi > 0 ? veDau!.PhiDoi : chinhSach.ChangeFee)} " +
                                "cộng chênh lệch giá. " +
                                $"Hoàn vé: phí {Money.Vnd(veDau?.PhiHoan > 0 ? veDau!.PhiHoan : chinhSach.RefundFee)} " +
                                "theo điều kiện vé.")
                            .FontSize(8f).FontColor(MauChu);
                    });
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().LineHorizontal(0.8f).LineColor(MauVien);
                    footer.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem()
                            .Text($"Hỗ trợ: {chinhSach.Hotline} · {chinhSach.SupportEmail}")
                            .FontSize(7.5f).FontColor(MauMo);
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.DefaultTextStyle(style => style.FontSize(7.5f).FontColor(MauMo));
                            text.Span($"PNR {booking.MaDatVe} · Trang ");
                            text.CurrentPageNumber();
                            text.Span("/");
                            text.TotalPages();
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static byte[] TaoQr(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }
}
