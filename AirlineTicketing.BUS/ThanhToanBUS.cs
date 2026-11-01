using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.BUS;

public sealed class ThanhToanBUS(AppDbContext dbContext) : IThanhToanBUS
{
    public async Task<ThanhToanDTO?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var thanhToan = await dbContext.ThanhToans
            .Include(x => x.DatVe)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return thanhToan != null ? MapToDTO(thanhToan) : null;
    }

    public async Task<IReadOnlyList<ThanhToanDTO>> GetByDatVeIdAsync(int datVeId, CancellationToken cancellationToken = default)
    {
        var list = await dbContext.ThanhToans
            .Include(x => x.DatVe)
            .Where(x => x.DatVeId == datVeId)
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDTO).ToList();
    }

    public async Task<ThanhToanDTO> KhoiTaoThanhToanAsync(
        int datVeId,
        PhuongThucThanhToan phuongThuc,
        string nhaCungCap = "NoiBo",
        CancellationToken cancellationToken = default)
    {
        var datVe = await dbContext.DatVes.FindAsync([datVeId], cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy lượt đặt vé #{datVeId}.");

        if (datVe.TrangThai == TrangThaiDatVe.DaHuy || datVe.TrangThai == TrangThaiDatVe.HetHan)
        {
            throw new InvalidOperationException($"Đặt vé #{datVe.MaDatVe} đã bị hủy hoặc hết hạn giữ chỗ.");
        }

        if (datVe.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            throw new InvalidOperationException($"Đặt vé #{datVe.MaDatVe} đã được thanh toán thành công.");
        }

        string prefix = string.IsNullOrWhiteSpace(nhaCungCap) ? "PAY" : nhaCungCap.ToUpperInvariant();
        string maGiaoDich = $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var thanhToan = new ThanhToan
        {
            DatVeId = datVe.Id,
            SoTien = datVe.TongTien,
            DonViTienTe = datVe.DonViTienTe,
            PhuongThuc = phuongThuc,
            NhaCungCap = string.IsNullOrWhiteSpace(nhaCungCap) ? "NoiBo" : nhaCungCap,
            TrangThai = TrangThaiThanhToan.ChoXuLy,
            MaGiaoDich = maGiaoDich,
            NgayTao = DateTimeOffset.UtcNow
        };

        await dbContext.ThanhToans.AddAsync(thanhToan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        thanhToan.DatVe = datVe;
        return MapToDTO(thanhToan);
    }

    public async Task<KetQuaThanhToanDTO> XuLyThanhToanGiaLapAsync(
        int thanhToanId,
        bool thanhCong,
        string? maGiaoDichNgoai = null,
        CancellationToken cancellationToken = default)
    {
        var thanhToan = await dbContext.ThanhToans
            .Include(x => x.DatVe)
                .ThenInclude(d => d.Ves)
            .FirstOrDefaultAsync(x => x.Id == thanhToanId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy giao dịch thanh toán #{thanhToanId}.");

        // Idempotency check: Giao dịch đã thành công trước đó thì trả về thành công ngay, tránh lặp
        if (thanhToan.TrangThai == TrangThaiThanhToan.ThanhCong)
        {
            return new KetQuaThanhToanDTO
            {
                IsSuccess = true,
                Message = "Giao dịch này đã được xác nhận thanh toán thành công trước đó.",
                ThanhToanId = thanhToan.Id,
                MaGiaoDich = thanhToan.MaGiaoDich,
                MaDatVe = thanhToan.DatVe.MaDatVe,
                TrangThai = thanhToan.TrangThai.ToString(),
                SoTien = thanhToan.SoTien,
                DonViTienTe = thanhToan.DonViTienTe,
                NgayHoanTat = thanhToan.NgayHoanTat
            };
        }

        if (thanhCong)
        {
            thanhToan.TrangThai = TrangThaiThanhToan.ThanhCong;
            thanhToan.NgayHoanTat = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(maGiaoDichNgoai))
            {
                thanhToan.MaGiaoDich = maGiaoDichNgoai;
            }

            // Cập nhật trạng thái Đặt vé
            thanhToan.DatVe.TrangThai = TrangThaiDatVe.DaXacNhan;

            // Phát hành vé điện tử cho các vé thuộc đơn đặt chỗ
            foreach (var ve in thanhToan.DatVe.Ves)
            {
                ve.TrangThai = TrangThaiVe.DaPhatHanh;
                if (string.IsNullOrWhiteSpace(ve.MaVe))
                {
                    ve.MaVe = $"TKT-{thanhToan.DatVeId:D4}-{ve.Id:D4}";
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            return new KetQuaThanhToanDTO
            {
                IsSuccess = true,
                Message = "Thanh toán thành công! Vé máy bay điện tử đã được phát hành.",
                ThanhToanId = thanhToan.Id,
                MaGiaoDich = thanhToan.MaGiaoDich,
                MaDatVe = thanhToan.DatVe.MaDatVe,
                TrangThai = thanhToan.TrangThai.ToString(),
                SoTien = thanhToan.SoTien,
                DonViTienTe = thanhToan.DonViTienTe,
                NgayHoanTat = thanhToan.NgayHoanTat
            };
        }
        else
        {
            thanhToan.TrangThai = TrangThaiThanhToan.ThatBai;
            thanhToan.NgayHoanTat = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            return new KetQuaThanhToanDTO
            {
                IsSuccess = false,
                Message = "Giao dịch thanh toán không thành công hoặc người dùng đã hủy giao dịch.",
                ThanhToanId = thanhToan.Id,
                MaGiaoDich = thanhToan.MaGiaoDich,
                MaDatVe = thanhToan.DatVe.MaDatVe,
                TrangThai = thanhToan.TrangThai.ToString(),
                SoTien = thanhToan.SoTien,
                DonViTienTe = thanhToan.DonViTienTe,
                NgayHoanTat = thanhToan.NgayHoanTat
            };
        }
    }

    public async Task<HoanTienDTO> TaoHoanTienAsync(
        TaoHoanTienDTO dto,
        string? nguoiThucHien = null,
        CancellationToken cancellationToken = default)
    {
        var thanhToan = await dbContext.ThanhToans
            .Include(x => x.DatVe)
                .ThenInclude(d => d.Ves)
            .Include(x => x.HoanTiens)
            .FirstOrDefaultAsync(x => x.Id == dto.ThanhToanId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy giao dịch thanh toán #{dto.ThanhToanId}.");

        if (thanhToan.TrangThai != TrangThaiThanhToan.ThanhCong)
        {
            throw new InvalidOperationException("Chỉ có thể hoàn tiền cho giao dịch đã thanh toán thành công.");
        }

        decimal tongDaHoan = thanhToan.HoanTiens
            .Where(x => x.TrangThai == TrangThaiHoanTien.ThanhCong)
            .Sum(x => x.SoTien);

        decimal soTienConLai = thanhToan.SoTien - tongDaHoan;
        if (dto.SoTien > soTienConLai)
        {
            throw new InvalidOperationException(
                $"Số tiền hoàn ({dto.SoTien:N0} {thanhToan.DonViTienTe}) vượt quá số tiền khả dụng để hoàn ({soTienConLai:N0} {thanhToan.DonViTienTe}).");
        }

        string maHoanTien = $"REF-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        string lyDoWithActor = string.IsNullOrWhiteSpace(nguoiThucHien)
            ? dto.LyDo ?? "Hoàn tiền theo yêu cầu"
            : $"{dto.LyDo ?? "Hoàn tiền theo yêu cầu"} [Thực hiện bởi: {nguoiThucHien}]";

        var hoanTien = new HoanTien
        {
            ThanhToanId = thanhToan.Id,
            SoTien = dto.SoTien,
            DonViTienTe = thanhToan.DonViTienTe,
            NhaCungCap = thanhToan.NhaCungCap,
            MaGiaoDich = maHoanTien,
            LyDo = lyDoWithActor,
            TrangThai = TrangThaiHoanTien.ThanhCong,
            NgayTao = DateTimeOffset.UtcNow,
            NgayHoanTat = DateTimeOffset.UtcNow
        };

        await dbContext.HoanTiens.AddAsync(hoanTien, cancellationToken);

        // Nếu đã hoàn hết toàn bộ số tiền, chuyển trạng thái Đặt vé và Vé sang Hoàn tiền
        if ((tongDaHoan + dto.SoTien) >= thanhToan.SoTien)
        {
            thanhToan.DatVe.TrangThai = TrangThaiDatVe.HoanTien;
            foreach (var ve in thanhToan.DatVe.Ves)
            {
                ve.TrangThai = TrangThaiVe.DaHoanTien;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new HoanTienDTO
        {
            Id = hoanTien.Id,
            ThanhToanId = hoanTien.ThanhToanId,
            SoTien = hoanTien.SoTien,
            DonViTienTe = hoanTien.DonViTienTe,
            TrangThai = hoanTien.TrangThai.ToString(),
            MaGiaoDich = hoanTien.MaGiaoDich,
            LyDo = hoanTien.LyDo,
            NgayTao = hoanTien.NgayTao,
            NgayHoanTat = hoanTien.NgayHoanTat
        };
    }

    public async Task<IReadOnlyList<ThanhToanDTO>> GetAllGiaoDichAsync(
        string? keyword = null,
        TrangThaiThanhToan? trangThai = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ThanhToan> query = dbContext.ThanhToans
            .Include(x => x.DatVe)
            .AsNoTracking();

        if (trangThai.HasValue)
        {
            query = query.Where(x => x.TrangThai == trangThai.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string lower = keyword.Trim().ToLower();
            query = query.Where(x => (x.MaGiaoDich != null && x.MaGiaoDich.ToLower().Contains(lower))
                                  || x.DatVe.MaDatVe.ToLower().Contains(lower)
                                  || x.NhaCungCap.ToLower().Contains(lower));
        }

        var list = await query
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDTO).ToList();
    }

    public async Task<IReadOnlyList<HoanTienDTO>> GetAllHoanTienAsync(CancellationToken cancellationToken = default)
    {
        var list = await dbContext.HoanTiens
            .AsNoTracking()
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync(cancellationToken);

        return list.Select(x => new HoanTienDTO
        {
            Id = x.Id,
            ThanhToanId = x.ThanhToanId,
            SoTien = x.SoTien,
            DonViTienTe = x.DonViTienTe,
            TrangThai = x.TrangThai.ToString(),
            MaGiaoDich = x.MaGiaoDich,
            LyDo = x.LyDo,
            NgayTao = x.NgayTao,
            NgayHoanTat = x.NgayHoanTat
        }).ToList();
    }

    private static ThanhToanDTO MapToDTO(ThanhToan entity) => new()
    {
        Id = entity.Id,
        DatVeId = entity.DatVeId,
        MaDatVe = entity.DatVe?.MaDatVe,
        SoTien = entity.SoTien,
        DonViTienTe = entity.DonViTienTe,
        PhuongThuc = entity.PhuongThuc.ToString(),
        NhaCungCap = entity.NhaCungCap,
        TrangThai = entity.TrangThai.ToString(),
        MaGiaoDich = entity.MaGiaoDich,
        NgayTao = entity.NgayTao,
        NgayHoanTat = entity.NgayHoanTat
    };
}
