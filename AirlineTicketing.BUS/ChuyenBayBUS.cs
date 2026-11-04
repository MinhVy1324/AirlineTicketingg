using AirlineTicketing.DAL.Repositories;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;

namespace AirlineTicketing.BUS;

/// <summary>
/// Nghiệp vụ chuyến bay: tìm chuyến, kiểm tra tồn chỗ, lịch khai thác và các quy tắc
/// thời gian / máy bay / tuyến trước khi lưu.
/// </summary>
public sealed class ChuyenBayBUS(
    IChuyenBayDAL chuyenBayDAL,
    ISanBayDAL sanBayDAL,
    IMayBayDAL mayBayDAL,
    ITuyenBayDAL tuyenBayDAL)
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private const int SeatsPerRow = 6;
    private static readonly char[] SeatLetters = ['A', 'B', 'C', 'D', 'E', 'F'];

    public async Task<IReadOnlyList<ChuyenBayDTO>> TimChuyenBayAsync(
        TimChuyenBayDTO dto,
        CancellationToken cancellationToken = default)
    {
        var loi = KiemTraTimKiem(dto);
        if (loi is not null)
        {
            throw new ArgumentException(loi, nameof(dto));
        }

        var (fromUtc, toUtc) = LayKhoangNgay(dto.NgayDi);
        var items = await chuyenBayDAL.SearchAsync(
            dto.SanBayDiId,
            dto.SanBayDenId,
            fromUtc,
            toUtc,
            cancellationToken);

        var usage = await chuyenBayDAL.GetSeatUsageAsync(items.Select(x => x.Id), cancellationToken);

        return items
            .Select(item => Map(item, LaySuDung(usage, item.Id)))
            .Where(item => item.SoGheTrong >= dto.SoHanhKhach)
            .ToArray();
    }

    public async Task<ChuyenBayDTO?> LayChuyenBayAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await chuyenBayDAL.GetByIdWithDetailsAsync(id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var usage = await chuyenBayDAL.GetSeatUsageAsync([id], cancellationToken);
        return Map(entity, LaySuDung(usage, id));
    }

    public async Task<IReadOnlyList<ChuyenBayDTO>> LayLichBayAsync(
        DateOnly? ngay = null,
        int? tuyenBayId = null,
        string? trangThai = null,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset? fromUtc = null;
        DateTimeOffset? toUtc = null;

        if (ngay is not null)
        {
            (fromUtc, toUtc) = LayKhoangNgay(ngay.Value);
        }

        var items = await chuyenBayDAL.GetAllWithDetailsAsync(fromUtc, toUtc, tuyenBayId, cancellationToken);
        var usage = await chuyenBayDAL.GetSeatUsageAsync(items.Select(x => x.Id), cancellationToken);

        var query = items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "Tất cả")
        {
            query = query.Where(x =>
                HienThiTrangThai(x.TrangThai).Contains(trangThai, StringComparison.OrdinalIgnoreCase)
                || x.TrangThai.ToString().Contains(trangThai, StringComparison.OrdinalIgnoreCase));
        }

        return query.Select(item => Map(item, LaySuDung(usage, item.Id))).ToArray();
    }

    public async Task<SoDoGheDTO> LaySoDoGheAsync(int chuyenBayId, CancellationToken cancellationToken = default)
    {
        var entity = await chuyenBayDAL.GetByIdWithDetailsAsync(chuyenBayId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy chuyến bay.");

        var occupied = await chuyenBayDAL.GetSeatStatesAsync(chuyenBayId, cancellationToken);
        var total = entity.MayBay.SoGhe;
        var seats = new List<GheDTO>(total);
        var rowCount = (int)Math.Ceiling(total / (double)SeatsPerRow);

        for (var row = 1; row <= rowCount; row++)
        {
            for (var column = 0; column < SeatsPerRow; column++)
            {
                if ((row - 1) * SeatsPerRow + column >= total)
                {
                    break;
                }

                var code = $"{row}{SeatLetters[column]}";
                seats.Add(new GheDTO
                {
                    SoGhe = code,
                    TrangThai = occupied.TryGetValue(code, out var state) ? state : "Trong"
                });
            }
        }

        return new SoDoGheDTO
        {
            ChuyenBayId = entity.Id,
            SoHieu = entity.SoHieu,
            TongSoGhe = total,
            Ghes = seats
        };
    }

    public async Task<KetQuaXuLy> TaoChuyenBayAsync(
        TaoChuyenBayDTO dto,
        CancellationToken cancellationToken = default)
    {
        var loi = KiemTraDuLieu(
            dto.SoHieu,
            dto.TuyenBayId,
            dto.MayBayId,
            dto.GioKhoiHanh,
            dto.GioHaCanh,
            dto.GiaCoBan,
            dto.TrangThai);
        if (loi is not null)
        {
            return KetQuaXuLy.ThatBai(loi);
        }

        var tuyen = await tuyenBayDAL.GetByIdAsync(dto.TuyenBayId, cancellationToken);
        if (tuyen is null)
        {
            return KetQuaXuLy.ThatBai("Tuyến bay không tồn tại.");
        }

        if (!tuyen.DangKhaiThac)
        {
            return KetQuaXuLy.ThatBai("Tuyến bay đang tạm dừng khai thác.");
        }

        var sanBayLoi = await KiemTraSanBayDauMutAsync(tuyen, cancellationToken);
        if (sanBayLoi is not null)
        {
            return KetQuaXuLy.ThatBai(sanBayLoi);
        }

        var mayBay = await mayBayDAL.GetByIdAsync(dto.MayBayId, cancellationToken);
        if (mayBay is null)
        {
            return KetQuaXuLy.ThatBai("Máy bay không tồn tại.");
        }

        if (!mayBay.DangHoatDong)
        {
            return KetQuaXuLy.ThatBai("Máy bay đang bảo trì hoặc tạm dừng khai thác.");
        }

        var soHieu = ChuanHoaSoHieu(dto.SoHieu);
        var gioDi = dto.GioKhoiHanh.ToUniversalTime();
        var gioDen = dto.GioHaCanh.ToUniversalTime();

        if (await chuyenBayDAL.ExistsSoHieuAsync(soHieu, gioDi, null, cancellationToken))
        {
            return KetQuaXuLy.ThatBai($"Số hiệu {soHieu} đã có chuyến khởi hành cùng thời điểm.");
        }

        if (await chuyenBayDAL.HasAircraftConflictAsync(dto.MayBayId, gioDi, gioDen, null, cancellationToken))
        {
            return KetQuaXuLy.ThatBai("Máy bay đã được xếp cho chuyến khác trong khoảng thời gian này.");
        }

        await chuyenBayDAL.AddAsync(new ChuyenBay
        {
            SoHieu = soHieu,
            TuyenBayId = dto.TuyenBayId,
            MayBayId = dto.MayBayId,
            GioKhoiHanh = gioDi,
            GioHaCanh = gioDen,
            GiaCoBan = dto.GiaCoBan,
            TrangThai = ParseTrangThai(dto.TrangThai)
        }, cancellationToken);

        await chuyenBayDAL.SaveChangesAsync(cancellationToken);
        return KetQuaXuLy.Ok();
    }

    public async Task<KetQuaXuLy> CapNhatChuyenBayAsync(
        CapNhatChuyenBayDTO dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Id <= 0)
        {
            return KetQuaXuLy.ThatBai("Chuyến bay cần cập nhật không hợp lệ.");
        }

        var loi = KiemTraDuLieu(
            dto.SoHieu,
            dto.TuyenBayId,
            dto.MayBayId,
            dto.GioKhoiHanh,
            dto.GioHaCanh,
            dto.GiaCoBan,
            dto.TrangThai);
        if (loi is not null)
        {
            return KetQuaXuLy.ThatBai(loi);
        }

        var entity = await chuyenBayDAL.GetByIdAsync(dto.Id, cancellationToken);
        if (entity is null)
        {
            return KetQuaXuLy.ThatBai("Không tìm thấy chuyến bay cần cập nhật.");
        }

        var tuyen = await tuyenBayDAL.GetByIdAsync(dto.TuyenBayId, cancellationToken);
        if (tuyen is null || !tuyen.DangKhaiThac)
        {
            return KetQuaXuLy.ThatBai("Tuyến bay không tồn tại hoặc đang tạm dừng khai thác.");
        }

        var sanBayLoi = await KiemTraSanBayDauMutAsync(tuyen, cancellationToken);
        if (sanBayLoi is not null)
        {
            return KetQuaXuLy.ThatBai(sanBayLoi);
        }

        var mayBay = await mayBayDAL.GetByIdAsync(dto.MayBayId, cancellationToken);
        if (mayBay is null)
        {
            return KetQuaXuLy.ThatBai("Máy bay không tồn tại.");
        }

        if (!mayBay.DangHoatDong)
        {
            return KetQuaXuLy.ThatBai("Máy bay đang bảo trì hoặc tạm dừng khai thác.");
        }

        var maxUsage = await chuyenBayDAL.GetMaxSeatUsageForAircraftAsync(dto.MayBayId, cancellationToken);
        if (mayBay.SoGhe < maxUsage)
        {
            return KetQuaXuLy.ThatBai(
                $"Máy bay chỉ có {mayBay.SoGhe} ghế nhưng đã bán/giữ tối đa {maxUsage} ghế trên một chuyến.");
        }

        var soHieu = ChuanHoaSoHieu(dto.SoHieu);
        var gioDi = dto.GioKhoiHanh.ToUniversalTime();
        var gioDen = dto.GioHaCanh.ToUniversalTime();

        if (await chuyenBayDAL.ExistsSoHieuAsync(soHieu, gioDi, dto.Id, cancellationToken))
        {
            return KetQuaXuLy.ThatBai($"Số hiệu {soHieu} đã có chuyến khởi hành cùng thời điểm.");
        }

        if (await chuyenBayDAL.HasAircraftConflictAsync(dto.MayBayId, gioDi, gioDen, dto.Id, cancellationToken))
        {
            return KetQuaXuLy.ThatBai("Máy bay đã được xếp cho chuyến khác trong khoảng thời gian này.");
        }

        entity.SoHieu = soHieu;
        entity.TuyenBayId = dto.TuyenBayId;
        entity.MayBayId = dto.MayBayId;
        entity.GioKhoiHanh = gioDi;
        entity.GioHaCanh = gioDen;
        entity.GiaCoBan = dto.GiaCoBan;
        entity.TrangThai = ParseTrangThai(dto.TrangThai);
        chuyenBayDAL.Update(entity);

        await chuyenBayDAL.SaveChangesAsync(cancellationToken);
        return KetQuaXuLy.Ok();
    }

    public async Task<KetQuaXuLy> HuyChuyenBayAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await chuyenBayDAL.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return KetQuaXuLy.ThatBai("Không tìm thấy chuyến bay cần hủy.");
        }

        if (entity.TrangThai == TrangThaiChuyenBay.BiHuy)
        {
            return KetQuaXuLy.Ok();
        }

        entity.TrangThai = TrangThaiChuyenBay.BiHuy;
        chuyenBayDAL.Update(entity);
        await chuyenBayDAL.SaveChangesAsync(cancellationToken);
        return KetQuaXuLy.Ok();
    }

    public async Task<KetQuaXuLy> DoiGiaAsync(
        int id,
        decimal giaCoBanMoi,
        CancellationToken cancellationToken = default)
    {
        if (KiemTraGia(giaCoBanMoi) is { } loi)
        {
            return KetQuaXuLy.ThatBai(loi);
        }

        var entity = await chuyenBayDAL.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return KetQuaXuLy.ThatBai("Không tìm thấy chuyến bay cần đổi giá.");
        }

        entity.GiaCoBan = giaCoBanMoi;
        chuyenBayDAL.Update(entity);
        await chuyenBayDAL.SaveChangesAsync(cancellationToken);
        return KetQuaXuLy.Ok();
    }

    public static string? KiemTraThoiGian(DateTimeOffset gioKhoiHanh, DateTimeOffset gioHaCanh) =>
        gioHaCanh <= gioKhoiHanh ? "Giờ hạ cánh phải sau giờ khởi hành." : null;

    public static string? KiemTraGia(decimal giaCoBan) =>
        giaCoBan < 0 ? "Giá cơ bản không được âm." : null;

    public static string? KiemTraTuyen(int sanBayDiId, int sanBayDenId) =>
        sanBayDiId <= 0 || sanBayDenId <= 0
            ? "Phải chọn sân bay đi và sân bay đến."
            : sanBayDiId == sanBayDenId
                ? "Sân bay đi và sân bay đến phải khác nhau."
                : null;

    public static string? KiemTraSoKhach(int soHanhKhach) =>
        soHanhKhach is < 1 or > 9 ? "Số hành khách phải từ 1 đến 9." : null;

    public static int TinhSoGheTrong(int tongSoGhe, int soGheDaBan, int soGheDangGiu) =>
        Math.Max(0, tongSoGhe - soGheDaBan - soGheDangGiu);

    public static string HienThiTrangThai(TrangThaiChuyenBay trangThai) => trangThai switch
    {
        TrangThaiChuyenBay.DuKien => "Mở bán",
        TrangThaiChuyenBay.DangBay => "Đang bay",
        TrangThaiChuyenBay.HoanTat => "Hoàn tất",
        TrangThaiChuyenBay.BiHuy => "Đã hủy",
        _ => trangThai.ToString()
    };

    private async Task<string?> KiemTraSanBayDauMutAsync(
        TuyenBay tuyen,
        CancellationToken cancellationToken)
    {
        var sanBayDi = await sanBayDAL.GetByIdAsync(tuyen.SanBayDiId, cancellationToken);
        var sanBayDen = await sanBayDAL.GetByIdAsync(tuyen.SanBayDenId, cancellationToken);

        if (sanBayDi is null || sanBayDen is null)
        {
            return "Sân bay đầu mút của tuyến không tồn tại.";
        }

        if (!sanBayDi.DangHoatDong || !sanBayDen.DangHoatDong)
        {
            return "Không tạo chuyến cho tuyến có sân bay đang tạm dừng.";
        }

        return null;
    }

    private static string? KiemTraTimKiem(TimChuyenBayDTO dto)
    {
        var tuyen = KiemTraTuyen(dto.SanBayDiId, dto.SanBayDenId);
        if (tuyen is not null)
        {
            return tuyen;
        }

        if (dto.NgayDi == default)
        {
            return "Ngày đi không hợp lệ.";
        }

        return KiemTraSoKhach(dto.SoHanhKhach);
    }

    private static string? KiemTraDuLieu(
        string soHieu,
        int tuyenBayId,
        int mayBayId,
        DateTimeOffset gioKhoiHanh,
        DateTimeOffset gioHaCanh,
        decimal giaCoBan,
        string trangThai)
    {
        var code = soHieu.Trim();
        if (code.Length == 0)
        {
            return "Số hiệu chuyến bay là bắt buộc.";
        }

        if (code.Length > 20)
        {
            return "Số hiệu chuyến bay tối đa 20 ký tự.";
        }

        if (tuyenBayId <= 0)
        {
            return "Phải chọn tuyến bay.";
        }

        if (mayBayId <= 0)
        {
            return "Phải chọn máy bay.";
        }

        if (gioKhoiHanh == default || gioHaCanh == default)
        {
            return "Giờ khởi hành và giờ hạ cánh là bắt buộc.";
        }

        var thoiGian = KiemTraThoiGian(gioKhoiHanh, gioHaCanh);
        if (thoiGian is not null)
        {
            return thoiGian;
        }

        if (KiemTraGia(giaCoBan) is { } gia)
        {
            return gia;
        }

        return TryParseTrangThai(trangThai, out _) ? null : "Trạng thái chuyến bay không hợp lệ.";
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) LayKhoangNgay(DateOnly ngay)
    {
        var start = new DateTimeOffset(ngay.ToDateTime(TimeOnly.MinValue), VietnamOffset);
        return (start.ToUniversalTime(), start.AddDays(1).ToUniversalTime());
    }

    private static (int DaBan, int DangGiu) LaySuDung(
        IReadOnlyDictionary<int, (int DaBan, int DangGiu)> usage,
        int id) =>
        usage.TryGetValue(id, out var value) ? value : (0, 0);

    private static ChuyenBayDTO Map(ChuyenBay entity, (int DaBan, int DangGiu) usage) => new()
    {
        Id = entity.Id,
        SoHieu = entity.SoHieu,
        TuyenBayId = entity.TuyenBayId,
        MaSanBayDi = entity.TuyenBay.SanBayDi.MaSanBay,
        TenSanBayDi = entity.TuyenBay.SanBayDi.TenSanBay,
        ThanhPhoDi = entity.TuyenBay.SanBayDi.ThanhPho,
        MaSanBayDen = entity.TuyenBay.SanBayDen.MaSanBay,
        TenSanBayDen = entity.TuyenBay.SanBayDen.TenSanBay,
        ThanhPhoDen = entity.TuyenBay.SanBayDen.ThanhPho,
        MayBayId = entity.MayBayId,
        MaMayBay = entity.MayBay.MaMayBay,
        TenMayBay = entity.MayBay.TenMayBay,
        GioKhoiHanh = entity.GioKhoiHanh,
        GioHaCanh = entity.GioHaCanh,
        GiaCoBan = entity.GiaCoBan,
        TongSoGhe = entity.MayBay.SoGhe,
        SoGheDaBan = usage.DaBan,
        SoGheDangGiu = usage.DangGiu,
        SoGheTrong = TinhSoGheTrong(entity.MayBay.SoGhe, usage.DaBan, usage.DangGiu),
        TrangThai = entity.TrangThai.ToString()
    };

    private static string ChuanHoaSoHieu(string value) => value.Trim().ToUpperInvariant();

    private static bool TryParseTrangThai(string value, out TrangThaiChuyenBay trangThai)
    {
        if (Enum.TryParse(value, ignoreCase: true, out trangThai))
        {
            return true;
        }

        trangThai = value.Trim().ToLowerInvariant() switch
        {
            "mở bán" or "đang mở bán" => TrangThaiChuyenBay.DuKien,
            "đang bay" => TrangThaiChuyenBay.DangBay,
            "hoàn tất" => TrangThaiChuyenBay.HoanTat,
            "đã hủy" or "hủy" => TrangThaiChuyenBay.BiHuy,
            _ => TrangThaiChuyenBay.DuKien
        };

        return value.Trim().ToLowerInvariant() is "mở bán" or "đang mở bán" or "đang bay" or "hoàn tất" or "đã hủy" or "hủy";
    }

    private static TrangThaiChuyenBay ParseTrangThai(string value)
    {
        TryParseTrangThai(value, out var trangThai);
        return trangThai;
    }
}
