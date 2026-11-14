using System.Text.Json;
using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure.Tickets;

public sealed record KetQuaGuiVe(
    bool ThanhCong,
    string? Email = null,
    string? Kenh = null,
    string? Loi = null,
    bool BoQua = false)
{
    public static KetQuaGuiVe Ok(string email, string kenh) => new(true, email, kenh);

    public static KetQuaGuiVe DaGuiTruoc(string email) =>
        new(true, email, null, "Vé điện tử đã được gửi trước đó.", BoQua: true);

    public static KetQuaGuiVe ThatBai(string loi) => new(false, Loi: loi);
}

/// <summary>
/// Soạn và gửi vé điện tử (email kèm PDF) cho một đặt chỗ đã xác nhận, đồng thời
/// ghi lịch sử để không gửi trùng khi thanh toán được xác nhận nhiều lần.
/// </summary>
public sealed class GuiVeDienTuService(
    AppDbContext db,
    CauHinhHeThongBUS cauHinhHeThong,
    IEmailSender emailSender,
    ILogger<GuiVeDienTuService> logger)
{
    /// <summary>Dấu vết trong LichSuDatVes cho mỗi lần gửi vé điện tử.</summary>
    public const string HanhDongGuiVe = "GuiVeDienTu";

    public async Task<KetQuaGuiVe> GuiAsync(
        int datVeId,
        string nguoiThucHien,
        bool guiLai = false,
        CancellationToken cancellationToken = default)
    {
        var booking = await LoadAsync(datVeId, tracking: true, cancellationToken);
        if (booking is null)
        {
            return KetQuaGuiVe.ThatBai("Không tìm thấy đặt chỗ để gửi vé.");
        }

        if (booking.TrangThai != TrangThaiDatVe.DaXacNhan)
        {
            return KetQuaGuiVe.ThatBai("Đặt chỗ chưa được xác nhận nên chưa thể gửi vé điện tử.");
        }

        var email = VeEmailContent.EmailCuaKhach(booking);
        if (string.IsNullOrWhiteSpace(email) || email.EndsWith("@example.local", StringComparison.OrdinalIgnoreCase))
        {
            return KetQuaGuiVe.ThatBai("Đặt chỗ không có email liên hệ hợp lệ nên chưa gửi được vé.");
        }

        var daGui = await db.LichSuDatVes.AnyAsync(
            x => x.DatVeId == booking.Id && x.HanhDong == HanhDongGuiVe,
            cancellationToken);
        if (daGui && !guiLai)
        {
            return KetQuaGuiVe.DaGuiTruoc(email);
        }

        var chinhSach = await cauHinhHeThong.LayChinhSachAsync(cancellationToken);
        var nguoiGui = await cauHinhHeThong.LayAsync("notification.sender_email", cancellationToken);
        if (string.IsNullOrWhiteSpace(nguoiGui))
        {
            nguoiGui = await cauHinhHeThong.LayAsync("contact.sender_email", cancellationToken);
        }
        nguoiGui = string.IsNullOrWhiteSpace(nguoiGui) ? chinhSach.SupportEmail : nguoiGui!.Trim();

        var now = DateTimeOffset.UtcNow;
        byte[] pdf;
        try
        {
            pdf = VeDienTuPdf.Tao(booking, chinhSach, now);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Không tạo được PDF vé cho {Pnr}.", booking.MaDatVe);
            return KetQuaGuiVe.ThatBai("Không tạo được vé PDF. Vui lòng thử lại sau.");
        }

        var message = new EmailMessage
        {
            To = email,
            ToName = booking.KhachHang?.HoTen,
            FromAddress = nguoiGui,
            Subject = VeEmailContent.TieuDe(booking),
            HtmlBody = VeEmailContent.TaoHtml(booking, chinhSach),
            TextBody = VeEmailContent.TaoVanBan(booking, chinhSach),
            Attachment = new EmailAttachment(
                $"ve-dien-tu-{booking.MaDatVe}.pdf", "application/pdf", pdf)
        };

        try
        {
            await emailSender.GuiAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Gửi email vé cho {Pnr} tới {Email} thất bại.",
                booking.MaDatVe, email);
            return KetQuaGuiVe.ThatBai("Không gửi được email vé. Vui lòng thử lại hoặc tải vé PDF trực tiếp.");
        }

        db.LichSuDatVes.Add(new LichSuDatVe
        {
            DatVeId = booking.Id,
            HanhDong = HanhDongGuiVe,
            TrangThaiTruoc = booking.TrangThai.ToString(),
            TrangThaiSau = booking.TrangThai.ToString(),
            LyDo = $"Đã gửi vé điện tử tới {email}",
            NguoiThucHien = string.IsNullOrWhiteSpace(nguoiThucHien) ? "system" : nguoiThucHien,
            ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(new
            {
                email,
                sender = nguoiGui,
                channel = emailSender.TenKenh,
                attachment = message.Attachment?.FileName,
                guiLai
            })
        });
        await db.SaveChangesAsync(cancellationToken);

        return KetQuaGuiVe.Ok(email, emailSender.TenKenh);
    }

    /// <summary>Tạo PDF vé để tải trực tiếp; trả null khi không tìm thấy đặt chỗ.</summary>
    public async Task<byte[]?> LayPdfAsync(int datVeId, CancellationToken cancellationToken = default)
    {
        var booking = await LoadAsync(datVeId, tracking: false, cancellationToken);
        if (booking is null) return null;

        var chinhSach = await cauHinhHeThong.LayChinhSachAsync(cancellationToken);
        return VeDienTuPdf.Tao(booking, chinhSach, DateTimeOffset.UtcNow);
    }

    private Task<DatVe?> LoadAsync(int datVeId, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<DatVe> query = db.DatVes
            .Include(x => x.KhachHang)
            .Include(x => x.ThanhToans)
            .Include(x => x.Ves).ThenInclude(x => x.HanhKhach)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay).ThenInclude(x => x.MayBay)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay)
                .ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.Ves).ThenInclude(x => x.ChuyenBay)
                .ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen);

        if (!tracking) query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(x => x.Id == datVeId, cancellationToken);
    }
}
