using System.Globalization;
using System.Text;
using AirlineTicketing.BUS;
using AirlineTicketing.Domain;

namespace AirlineTicketing.Web.Infrastructure.Tickets;

/// <summary>Nội dung email xác nhận đặt vé, dùng bảng + style inline để hiển thị tốt trong hộp thư.</summary>
public static class VeEmailContent
{
    private const string MauChinh = "#1655A1";
    private const string MauNavy = "#102E50";
    private const string MauChu = "#172D46";
    private const string MauMo = "#64758A";
    private const string MauVien = "#DFE6EE";
    private const string MauNen = "#F4F7FB";
    private const string MauThanhCong = "#087F83";

    public static string TieuDe(DatVe booking) =>
        $"Vé điện tử AeroViet · {booking.MaDatVe} · " +
        $"{TenChuyen(booking)}";

    public static string TaoHtml(DatVe booking, ChinhSachDatVe chinhSach)
    {
        var veDau = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var chuyenBay = veDau?.ChuyenBay;
        var khoiHanh = chuyenBay is null ? DateTimeOffset.UtcNow : FlightMapping.ToVietnamTime(chuyenBay.GioKhoiHanh);
        var haCanh = chuyenBay is null ? DateTimeOffset.UtcNow : FlightMapping.ToVietnamTime(chuyenBay.GioHaCanh);
        var di = chuyenBay?.TuyenBay?.SanBayDi;
        var den = chuyenBay?.TuyenBay?.SanBayDen;
        var daThanhToan = booking.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong)
            .Sum(x => x.SoTien);
        if (daThanhToan <= 0) daThanhToan = booking.TongTien;
        var thanhToan = booking.ThanhToans
            .Where(x => x.TrangThai == TrangThaiThanhToan.ThanhCong)
            .OrderByDescending(x => x.NgayHoanTat ?? x.NgayTao)
            .FirstOrDefault();

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html lang=\"vi\"><head><meta charset=\"utf-8\" />")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />")
            .Append("<title>").Append(H(booking.MaDatVe)).Append("</title></head>")
            .Append("<body style=\"margin:0;padding:0;background:#EEF3F9;")
            .Append("font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:").Append(MauChu).Append(";\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"background:#EEF3F9;padding:24px 12px;\"><tr><td align=\"center\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"max-width:600px;background:#FFFFFF;border-radius:14px;overflow:hidden;")
            .Append("border:1px solid ").Append(MauVien).Append(";\">");

        // Thương hiệu
        html.Append("<tr><td style=\"background:").Append(MauChinh).Append(";padding:20px 24px;\">")
            .Append("<div style=\"font-size:20px;font-weight:700;letter-spacing:2px;color:#FFFFFF;\">AEROVIET</div>")
            .Append("<div style=\"font-size:12px;color:#D7E6F8;margin-top:2px;\">")
            .Append("Vé điện tử · Hãng hàng không AeroViet</div>")
            .Append("</td></tr>");

        // Lời chào + PNR
        html.Append("<tr><td style=\"padding:22px 24px 0;\">")
            .Append("<div style=\"font-size:16px;font-weight:700;color:").Append(MauNavy).Append(";\">")
            .Append("Đặt vé của bạn đã được xác nhận</div>")
            .Append("<div style=\"font-size:13px;color:").Append(MauMo).Append(";margin-top:4px;\">")
            .Append("Cảm ơn ").Append(H(booking.KhachHang?.HoTen ?? "Quý khách"))
            .Append(" đã bay cùng AeroViet. Vé điện tử PDF được đính kèm email này.</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"margin-top:16px;background:").Append(MauNen).Append(";border-radius:10px;\">")
            .Append("<tr><td style=\"padding:12px 16px;\">")
            .Append("<div style=\"font-size:10px;letter-spacing:1px;color:").Append(MauMo).Append(";\">MÃ ĐẶT CHỖ (PNR)</div>")
            .Append("<div style=\"font-size:24px;font-weight:700;color:").Append(MauNavy)
            .Append(";letter-spacing:2px;\">").Append(H(booking.MaDatVe)).Append("</div>")
            .Append("</td><td align=\"right\" style=\"padding:12px 16px;\">")
            .Append("<span style=\"display:inline-block;background:#E7F5F2;color:").Append(MauThanhCong)
            .Append(";font-size:11px;font-weight:700;letter-spacing:1px;padding:6px 10px;border-radius:999px;\">")
            .Append("ĐÃ XUẤT VÉ</span></td></tr></table></td></tr>");

        // Chuyến bay
        html.Append("<tr><td style=\"padding:20px 24px 0;\">")
            .Append("<div style=\"font-size:11px;letter-spacing:1px;color:").Append(MauMo).Append(";\">CHUYẾN BAY</div>")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-top:8px;\">")
            .Append("<tr>")
            .Append("<td width=\"33%\" valign=\"top\">")
            .Append("<div style=\"font-size:20px;font-weight:700;color:").Append(MauNavy).Append(";\">")
            .Append(H(di?.MaSanBay ?? "—")).Append("</div>")
            .Append("<div style=\"font-size:12px;color:").Append(MauMo).Append(";\">").Append(H(di?.ThanhPho ?? ""))
            .Append("</div><div style=\"font-size:14px;font-weight:600;margin-top:4px;\">")
            .Append(khoiHanh.ToString("HH:mm", CultureInfo.InvariantCulture)).Append("</div></td>")
            .Append("<td width=\"34%\" align=\"center\" valign=\"middle\" style=\"color:").Append(MauMo)
            .Append(";font-size:12px;\">").Append(H(chuyenBay?.SoHieu ?? "—"))
            .Append("<div style=\"font-size:11px;margin-top:2px;\">Bay thẳng</div></td>")
            .Append("<td width=\"33%\" align=\"right\" valign=\"top\">")
            .Append("<div style=\"font-size:20px;font-weight:700;color:").Append(MauNavy).Append(";\">")
            .Append(H(den?.MaSanBay ?? "—")).Append("</div>")
            .Append("<div style=\"font-size:12px;color:").Append(MauMo).Append(";\">").Append(H(den?.ThanhPho ?? ""))
            .Append("</div><div style=\"font-size:14px;font-weight:600;margin-top:4px;\">")
            .Append(haCanh.ToString("HH:mm", CultureInfo.InvariantCulture)).Append("</div></td>")
            .Append("</tr></table>")
            .Append("<div style=\"font-size:12px;color:").Append(MauMo).Append(";margin-top:8px;\">")
            .Append(Money.LongDate(khoiHanh)).Append(" · ").Append(H(chuyenBay?.MayBay?.TenMayBay ?? "—"))
            .Append("</div></td></tr>");

        // Hành khách
        html.Append("<tr><td style=\"padding:20px 24px 0;\">")
            .Append("<div style=\"font-size:11px;letter-spacing:1px;color:").Append(MauMo).Append(";\">HÀNH KHÁCH</div>")
            .Append(HanhKhachTable(booking)).Append("</td></tr>");

        // Thanh toán
        html.Append("<tr><td style=\"padding:18px 24px 0;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"background:").Append(MauNen).Append(";border-radius:10px;\">")
            .Append("<tr><td style=\"padding:14px 16px;\">")
            .Append(Dong("Tổng đã thanh toán", Money.Vnd(daThanhToan), dam: true))
            .Append(Dong("Phương thức", thanhToan?.NhaCungCap ?? booking.GhiChu ?? "Thanh toán nội bộ"))
            .Append(Dong("Mã giao dịch", thanhToan?.MaGiaoDich ?? "—"))
            .Append("</td></tr></table></td></tr>");

        // Ghi chú
        html.Append("<tr><td style=\"padding:18px 24px 22px;\">")
            .Append("<div style=\"font-size:12px;line-height:1.6;color:").Append(MauChu).Append(";\">")
            .Append("<strong>Trước khi khởi hành</strong><br />")
            .Append("• Có mặt tại sân bay trước giờ bay 2 tiếng, mang giấy tờ tùy thân trùng thông tin trên vé.<br />")
            .Append("• Vé điện tử không thay thế thẻ lên máy bay; vui lòng làm thủ tục tại quầy hoặc trực tuyến.<br />")
            .Append("• Hành lý: ").Append(veDau?.HanhLyXachTayKg ?? 10).Append(" kg xách tay · ")
            .Append(veDau?.HanhLyKyGuiKg ?? 23).Append(" kg ký gửi mỗi hành khách.<br />")
            .Append("• Hỗ trợ: ").Append(H(chinhSach.Hotline)).Append(" · ").Append(H(chinhSach.SupportEmail))
            .Append("</div></td></tr>");

        // Chân trang
        html.Append("<tr><td style=\"background:").Append(MauNavy).Append(";padding:14px 24px;")
            .Append("font-size:11px;color:#CEDCED;line-height:1.5;\">")
            .Append("Email tự động gửi tới ").Append(H(EmailCuaKhach(booking)))
            .Append(" cho đặt chỗ ").Append(H(booking.MaDatVe))
            .Append(". Vui lòng không trả lời email này.</td></tr>");

        html.Append("</table></td></tr></table></body></html>");
        return html.ToString();
    }

    public static string TaoVanBan(DatVe booking, ChinhSachDatVe chinhSach)
    {
        var veDau = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var chuyenBay = veDau?.ChuyenBay;
        var text = new StringBuilder();
        text.AppendLine($"AEROVIET · Vé điện tử {booking.MaDatVe}")
            .AppendLine($"Chuyến {chuyenBay?.SoHieu} · {TenChuyen(booking)}")
            .AppendLine($"Khởi hành: {Money.LongDate(FlightMapping.ToVietnamTime(chuyenBay?.GioKhoiHanh ?? DateTimeOffset.UtcNow))} lúc " +
                        $"{FlightMapping.ToVietnamTime(chuyenBay?.GioKhoiHanh ?? DateTimeOffset.UtcNow):HH:mm} (UTC+7)")
            .AppendLine()
            .AppendLine("Hành khách:");
        foreach (var ve in booking.Ves.OrderBy(x => x.Id))
        {
            text.AppendLine($"- {JourneyTicketText.Flight(ve)} · {JourneyTicketText.Person(ve)} · {JourneyTicketText.Seat(booking, ve)} · {JourneyBookingBuilder.CabinOf(ve.HangVe).Name} · vé {ve.MaVe}");
        }
        text.AppendLine()
            .AppendLine($"Tổng đã thanh toán: {Money.Vnd(booking.TongTien)}")
            .AppendLine($"Hỗ trợ: {chinhSach.Hotline} · {chinhSach.SupportEmail}")
            .AppendLine()
            .AppendLine("Vé điện tử PDF được đính kèm email này.");
        return text.ToString();
    }

    public static string EmailCuaKhach(DatVe booking) =>
        (booking.EmailLienHe ?? booking.KhachHang?.Email ?? string.Empty).Trim();

    private static string TenChuyen(DatVe booking)
    {
        var ve = booking.Ves.OrderBy(x => x.Id).FirstOrDefault();
        var route = ve?.ChuyenBay?.TuyenBay;
        return route is null
            ? "AeroViet"
            : $"{route.SanBayDi.MaSanBay} → {route.SanBayDen.MaSanBay}";
    }

    private static string HanhKhachTable(DatVe booking)
    {
        var html = new StringBuilder();
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ")
            .Append("style=\"margin-top:8px;border:1px solid ").Append(MauVien).Append(";border-radius:8px;\">")
            .Append("<tr style=\"background:").Append(MauNavy).Append(";color:#FFFFFF;font-size:11px;\">")
            .Append("<th align=\"left\" style=\"padding:8px 10px;\">Họ và tên</th>")
            .Append("<th align=\"left\" style=\"padding:8px 10px;\">Ghế</th>")
            .Append("<th align=\"left\" style=\"padding:8px 10px;\">Hạng vé</th>")
            .Append("<th align=\"left\" style=\"padding:8px 10px;\">Số vé</th></tr>");

        foreach (var ve in booking.Ves.OrderBy(x => x.Id))
        {
            html.Append("<tr style=\"font-size:12px;border-top:1px solid ").Append(MauVien).Append(";\">")
                .Append("<td style=\"padding:8px 10px;font-weight:600;\">").Append(H(JourneyTicketText.Person(ve))).Append("<br>").Append(H(JourneyTicketText.Flight(ve)))
                .Append("</td><td style=\"padding:8px 10px;\">").Append(H(JourneyTicketText.Seat(booking, ve)))
                .Append("</td><td style=\"padding:8px 10px;\">").Append(H(JourneyBookingBuilder.CabinOf(ve.HangVe).Name))
                .Append("</td><td style=\"padding:8px 10px;color:").Append(MauNavy).Append(";\">")
                .Append(H(ve.MaVe)).Append("</td></tr>");
        }

        return html.Append("</table>").ToString();
    }

    private static string Dong(string nhan, string giaTri, bool dam = false) =>
        $"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>" +
        $"<td style=\"font-size:12px;color:{MauMo};padding:3px 0;\">{H(nhan)}</td>" +
        $"<td align=\"right\" style=\"font-size:{(dam ? "14px" : "12px")};" +
        $"font-weight:{(dam ? "700" : "500")};" +
        $"color:{(dam ? MauThanhCong : MauChu)};padding:3px 0;\">{H(giaTri)}</td>" +
        "</tr></table>";

    private static string H(string value) =>
        System.Net.WebUtility.HtmlEncode(value);
}
