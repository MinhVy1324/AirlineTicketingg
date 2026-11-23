using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.Web.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Web.Infrastructure.Tickets;

public sealed class GuiVeDienTuService(AppDbContext db, CauHinhHeThongBUS cauHinhHeThong)
{
    public const string HanhDongGuiVe = "GuiVeDienTu";

    internal static bool HasDeliverableAddress(DatVe booking)
    {
        var address = VeEmailContent.EmailCuaKhach(booking);
        return !address.EndsWith("@example.local", StringComparison.OrdinalIgnoreCase)
            && System.Net.Mail.MailAddress.TryCreate(address, out _);
    }

    internal async Task<EmailMessage?> PrepareAsync(int bookingId, CancellationToken ct)
    {
        var booking = await LoadAsync(bookingId, tracking: false, ct);
        if (booking?.TrangThai != TrangThaiDatVe.DaXacNhan || FlightDisruptionService.NeedsAttention(booking)
            || !HasDeliverableAddress(booking))
            return null;

        var policy = await cauHinhHeThong.LayChinhSachAsync(ct);
        var sender = await cauHinhHeThong.LayAsync("notification.sender_email", ct);
        if (string.IsNullOrWhiteSpace(sender))
            sender = await cauHinhHeThong.LayAsync("contact.sender_email", ct);

        return new EmailMessage
        {
            To = VeEmailContent.EmailCuaKhach(booking),
            ToName = booking.TenLienHe ?? booking.KhachHang.HoTen,
            FromAddress = string.IsNullOrWhiteSpace(sender) ? policy.SupportEmail : sender.Trim(),
            Subject = VeEmailContent.TieuDe(booking),
            HtmlBody = VeEmailContent.TaoHtml(booking, policy),
            TextBody = VeEmailContent.TaoVanBan(booking, policy),
            Attachment = new EmailAttachment($"ve-dien-tu-{booking.MaDatVe}.pdf", "application/pdf",
                VeDienTuPdf.Tao(booking, policy, DateTimeOffset.UtcNow))
        };
    }

    /// <summary>Tạo PDF vé để tải trực tiếp; trả null khi không tìm thấy đặt chỗ.</summary>
    public async Task<byte[]?> LayPdfAsync(int datVeId, CancellationToken cancellationToken = default)
    {
        var booking = await LoadAsync(datVeId, tracking: false, cancellationToken);
        if (booking?.TrangThai != TrangThaiDatVe.DaXacNhan || FlightDisruptionService.NeedsAttention(booking)) return null;

        var chinhSach = await cauHinhHeThong.LayChinhSachAsync(cancellationToken);
        return VeDienTuPdf.Tao(booking, chinhSach, DateTimeOffset.UtcNow);
    }

    private Task<DatVe?> LoadAsync(int datVeId, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<DatVe> query = db.DatVes.AsSplitQuery()
            .Include(x => x.KhachHang)
            .Include(x => x.BienDongs.Where(change => change.NgayXuLy == null))
            .Include(x => x.ThanhToans).ThenInclude(x => x.HoanTiens)
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
