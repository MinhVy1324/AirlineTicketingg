using System.Data;
using System.Text.Json;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AirlineTicketing.BUS;

public sealed class VeBUS(AppDbContext dbContext, TimeProvider timeProvider, DatVeOptions options)
{
    public async Task<VeDTO> TraCuuAsync(string maVe, string emailXacMinh,
        CancellationToken cancellationToken = default)
    {
        var email = emailXacMinh.Trim().ToLowerInvariant();
        var ticket = await TicketQuery().AsNoTracking().FirstOrDefaultAsync(x =>
            x.MaVe == maVe.Trim().ToUpperInvariant() && x.DatVe.KhachHang.Email == email, cancellationToken);
        return ticket is null ? throw new KhongTimThayException("vé") : DatVeMapper.ToDto(ticket);
    }

    public async Task<VeDTO> HuyVeAsync(HuyVeDTO request, string emailXacMinh,
        string? nguoiThucHien = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LyDo))
            throw new NgoaiLeNghiepVu("THIEU_LY_DO", "Phải nhập lý do hủy vé.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var email = emailXacMinh.Trim().ToLowerInvariant();
        var ticket = await TicketQuery().FirstOrDefaultAsync(x =>
            x.MaVe == request.MaVe.Trim().ToUpperInvariant() && x.DatVe.KhachHang.Email == email,
            cancellationToken) ?? throw new KhongTimThayException("vé");

        KiemTraCoTheThayDoi(ticket);
        var now = timeProvider.GetUtcNow();
        ticket.TrangThai = TrangThaiVe.DaHuy;
        ticket.NgayHuy = now;
        ticket.LyDoHuy = request.LyDo.Trim();
        if (ticket.DatVe.Ves.All(x => x.Id == ticket.Id || x.TrangThai is TrangThaiVe.DaHuy or TrangThaiVe.DaHoanTien))
            ticket.DatVe.TrangThai = TrangThaiDatVe.DaHuy;
        ticket.DatVe.LichSus.Add(new LichSuDatVe
        {
            DatVe = ticket.DatVe, Ve = ticket, HanhDong = "HuyVe",
            TrangThaiTruoc = TrangThaiVe.DaPhatHanh.ToString(), TrangThaiSau = TrangThaiVe.DaHuy.ToString(),
            LyDo = request.LyDo.Trim(), NguoiThucHien = nguoiThucHien, ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(new { ticket.ChuyenBayId, ticket.SoGhe, ticket.GiaVe, ticket.HangVe })
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return DatVeMapper.ToDto(ticket);
    }

    public async Task<VeDTO> DoiVeAsync(DoiVeDTO request, string emailXacMinh,
        string? nguoiThucHien = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LyDo) || string.IsNullOrWhiteSpace(request.SoGheMoi) ||
            string.IsNullOrWhiteSpace(request.HangVeMoi))
            throw new NgoaiLeNghiepVu("DU_LIEU_KHONG_HOP_LE", "Chuyến, ghế, hạng vé và lý do đổi phải đầy đủ.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var email = emailXacMinh.Trim().ToLowerInvariant();
        var ticket = await TicketQuery().FirstOrDefaultAsync(x => x.Id == request.VeId &&
            x.DatVe.KhachHang.Email == email, cancellationToken) ?? throw new KhongTimThayException("vé");
        KiemTraCoTheThayDoi(ticket);

        var now = timeProvider.GetUtcNow();
        var newFlight = await dbContext.ChuyenBays.Include(x => x.MayBay)
            .SingleOrDefaultAsync(x => x.Id == request.ChuyenBayMoiId, cancellationToken)
            ?? throw new KhongTimThayException("chuyến bay mới");
        if (newFlight.TrangThai != TrangThaiChuyenBay.DuKien || newFlight.GioKhoiHanh <= now)
            throw new NgoaiLeNghiepVu("CHUYEN_BAY_KHONG_MO_BAN", "Chuyến bay mới không còn mở bán.");
        if (!options.HeSoHangVe.TryGetValue(request.HangVeMoi.Trim(), out var multiplier))
            throw new NgoaiLeNghiepVu("HANG_VE_KHONG_HOP_LE", "Hạng vé mới không được mở bán.");
        var active = await dbContext.Ves.CountAsync(x => x.ChuyenBayId == newFlight.Id && x.Id != ticket.Id &&
            (x.TrangThai == TrangThaiVe.TamGiu || x.TrangThai == TrangThaiVe.DaPhatHanh), cancellationToken);
        if (active >= newFlight.MayBay.SoGhe)
            throw new NgoaiLeNghiepVu("CHUYEN_BAY_HET_CHO", "Chuyến bay mới đã hết chỗ.");

        var old = new { ticket.ChuyenBayId, ticket.SoGhe, ticket.HangVe, ticket.GiaVe };
        ticket.ChuyenBayId = newFlight.Id;
        ticket.ChuyenBay = newFlight;
        ticket.SoGhe = request.SoGheMoi.Trim().ToUpperInvariant();
        ticket.HangVe = request.HangVeMoi.Trim();
        ticket.GiaVe = decimal.Round(newFlight.GiaCoBan * multiplier, 2, MidpointRounding.AwayFromZero);
        ticket.DatVe.TongTien = ticket.DatVe.Ves.Sum(x => x.GiaVe);
        ticket.DatVe.LichSus.Add(new LichSuDatVe
        {
            DatVe = ticket.DatVe, Ve = ticket, HanhDong = "DoiVe",
            TrangThaiTruoc = TrangThaiVe.DaPhatHanh.ToString(), TrangThaiSau = TrangThaiVe.DaPhatHanh.ToString(),
            LyDo = request.LyDo.Trim(), NguoiThucHien = nguoiThucHien, ThoiDiem = now,
            DuLieuBoSung = JsonSerializer.Serialize(new
            {
                Truoc = old,
                Sau = new { ticket.ChuyenBayId, ticket.SoGhe, ticket.HangVe, ticket.GiaVe },
                ChenhLech = ticket.GiaVe - old.GiaVe
            })
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DatVeMapper.ToDto(ticket);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Ves_ChuyenBayId_SoGhe"
        })
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new GheKhongConException(request.SoGheMoi);
        }
    }

    private IQueryable<Ve> TicketQuery() => dbContext.Ves
        .Include(x => x.HanhKhach)
        .Include(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
        .Include(x => x.ChuyenBay).ThenInclude(x => x.TuyenBay).ThenInclude(x => x.SanBayDen)
        .Include(x => x.DatVe).ThenInclude(x => x.KhachHang)
        .Include(x => x.DatVe).ThenInclude(x => x.Ves)
        .Include(x => x.DatVe).ThenInclude(x => x.LichSus);

    private void KiemTraCoTheThayDoi(Ve ticket)
    {
        if (ticket.TrangThai != TrangThaiVe.DaPhatHanh || ticket.DatVe.TrangThai != TrangThaiDatVe.DaXacNhan)
            throw new NgoaiLeNghiepVu("VE_KHONG_THE_THAY_DOI", "Chỉ vé đã phát hành mới có thể đổi hoặc hủy.");
        if (ticket.ChuyenBay.GioKhoiHanh <= timeProvider.GetUtcNow())
            throw new NgoaiLeNghiepVu("DA_QUA_GIO_BAY", "Không thể đổi hoặc hủy sau giờ khởi hành.");
    }
}
