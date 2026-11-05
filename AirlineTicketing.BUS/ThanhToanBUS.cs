using System.Data;
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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var datVe = await dbContext.DatVes
            .FromSqlInterpolated($"SELECT * FROM \"DatVes\" WHERE \"Id\" = {datVeId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy lượt đặt vé #{datVeId}.");

        if (datVe.TrangThai == TrangThaiDatVe.DaHuy || datVe.TrangThai == TrangThaiDatVe.HetHan)
        {
            throw new InvalidOperationException($"Đặt vé #{datVe.MaDatVe} đã bị hủy hoặc hết hạn giữ chỗ.");
        }

        if (datVe.TrangThai == TrangThaiDatVe.DaXacNhan)
        {
            throw new InvalidOperationException($"Đặt vé #{datVe.MaDatVe} đã được thanh toán thành công.");
        }

        if (datVe.TongTien <= 0 || datVe.DonViTienTe.Length != 3
            || datVe.DonViTienTe.Any(character => character is < 'A' or > 'Z'))
        {
            throw new InvalidOperationException("Đặt vé chưa có tổng tiền hoặc đơn vị tiền tệ hợp lệ.");
        }

        if (!Enum.IsDefined(phuongThuc))
        {
            throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");
        }

        var pendingPayment = await dbContext.ThanhToans
            .Include(x => x.DatVe)
            .FirstOrDefaultAsync(x => x.DatVeId == datVe.Id && x.TrangThai == TrangThaiThanhToan.ChoXuLy, cancellationToken);
        if (pendingPayment is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return MapToDTO(pendingPayment);
        }

        var provider = nhaCungCap?.Trim().ToUpperInvariant() switch
        {
            "VNPAY" => "VNPAY",
            "MOMO" => "MoMo",
            "ZALOPAY" => "ZaloPay",
            _ => "NoiBo"
        };
        string prefix = provider.ToUpperInvariant();
        string maGiaoDich = $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var thanhToan = new ThanhToan
        {
            DatVeId = datVe.Id,
            SoTien = datVe.TongTien,
            DonViTienTe = datVe.DonViTienTe,
            PhuongThuc = phuongThuc,
            NhaCungCap = provider,
            TrangThai = TrangThaiThanhToan.ChoXuLy,
            MaGiaoDich = maGiaoDich,
            NgayTao = DateTimeOffset.UtcNow
        };

        await dbContext.ThanhToans.AddAsync(thanhToan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        thanhToan.DatVe = datVe;
        return MapToDTO(thanhToan);
    }

    public async Task<KetQuaThanhToanDTO> XuLyThanhToanGiaLapAsync(
        int thanhToanId,
        bool thanhCong,
        string? maGiaoDichNgoai = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var thanhToan = await dbContext.ThanhToans
            .FromSqlInterpolated($"SELECT * FROM \"ThanhToans\" WHERE \"Id\" = {thanhToanId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy giao dịch thanh toán #{thanhToanId}.");
        await dbContext.Entry(thanhToan).Reference(x => x.DatVe).LoadAsync(cancellationToken);
        await dbContext.Entry(thanhToan.DatVe).Collection(x => x.Ves).LoadAsync(cancellationToken);

        // Idempotency check: Giao dịch đã thành công trước đó thì trả về thành công ngay, tránh lặp
        if (thanhToan.TrangThai == TrangThaiThanhToan.ThanhCong)
        {
            await transaction.CommitAsync(cancellationToken);
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

        if (thanhToan.TrangThai != TrangThaiThanhToan.ChoXuLy)
        {
            await transaction.CommitAsync(cancellationToken);
            return new KetQuaThanhToanDTO
            {
                IsSuccess = false,
                Message = "Giao dịch đã kết thúc. Hãy tạo lần thanh toán mới nếu cần thử lại.",
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
            if (thanhToan.DatVe.TrangThai is TrangThaiDatVe.DaHuy or TrangThaiDatVe.HetHan
                || thanhToan.DatVe.Ves.Count == 0)
            {
                throw new InvalidOperationException("Đặt vé đã đóng hoặc chưa có vé giữ chỗ để phát hành.");
            }

            if (!string.IsNullOrWhiteSpace(maGiaoDichNgoai) && maGiaoDichNgoai.Trim().Length > 100)
            {
                throw new InvalidOperationException("Mã giao dịch từ cổng thanh toán quá dài.");
            }
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
            await transaction.CommitAsync(cancellationToken);

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
            await transaction.CommitAsync(cancellationToken);

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
        if (dto.SoTien <= 0)
        {
            throw new InvalidOperationException("Số tiền hoàn phải lớn hơn 0.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var thanhToan = await dbContext.ThanhToans
            .FromSqlInterpolated($"SELECT * FROM \"ThanhToans\" WHERE \"Id\" = {dto.ThanhToanId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy giao dịch thanh toán #{dto.ThanhToanId}.");
        await dbContext.Entry(thanhToan).Reference(x => x.DatVe).LoadAsync(cancellationToken);
        await dbContext.Entry(thanhToan).Collection(x => x.HoanTiens).LoadAsync(cancellationToken);
        await dbContext.Entry(thanhToan.DatVe).Collection(x => x.Ves).LoadAsync(cancellationToken);

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
        await transaction.CommitAsync(cancellationToken);

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
