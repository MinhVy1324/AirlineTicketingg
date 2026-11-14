using System.Text.RegularExpressions;
using AirlineTicketing.DAL.Repositories;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;

namespace AirlineTicketing.BUS;

/// <summary>Nghiệp vụ danh mục nền: sân bay, máy bay và tuyến bay.</summary>
public sealed class DanhMucBUS(
    ISanBayDAL sanBayDAL,
    IMayBayDAL mayBayDAL,
    ITuyenBayDAL tuyenBayDAL,
    IChuyenBayDAL chuyenBayDAL,
    IHangHangKhongDAL hangHangKhongDAL)
{
    private static readonly Regex MaSanBayRegex = new("^[A-Z]{3}$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<SanBayDTO>> LaySanBayAsync(
        string? tuKhoa = null,
        string? trangThai = null,
        CancellationToken cancellationToken = default)
    {
        var items = await sanBayDAL.GetAllAsync(cancellationToken);
        var query = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var keyword = tuKhoa.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.MaSanBay.ToUpperInvariant().Contains(keyword)
                || x.TenSanBay.ToUpperInvariant().Contains(keyword)
                || x.ThanhPho.ToUpperInvariant().Contains(keyword));
        }

        query = LocTrangThai(query, trangThai, x => x.DangHoatDong);

        return query.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<MayBayDTO>> LayMayBayAsync(
        string? tuKhoa = null,
        string? trangThai = null,
        CancellationToken cancellationToken = default)
    {
        var items = await mayBayDAL.GetAllAsync(cancellationToken);
        var query = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var keyword = tuKhoa.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.MaMayBay.ToUpperInvariant().Contains(keyword)
                || x.TenMayBay.ToUpperInvariant().Contains(keyword)
                || x.HangSanXuat.ToUpperInvariant().Contains(keyword));
        }

        query = LocTrangThai(query, trangThai, x => x.DangHoatDong);

        return query.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<HangHangKhongDTO>> LayHangHangKhongAsync(
        bool? dangHoatDong = null,
        CancellationToken cancellationToken = default)
    {
        var items = await hangHangKhongDAL.GetAllAsync(cancellationToken);
        var query = items.AsEnumerable();

        if (dangHoatDong is not null)
        {
            query = query.Where(x => x.DangHoatDong == dangHoatDong);
        }

        return query.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<TuyenBayDTO>> LayTuyenBayAsync(
        string? tuKhoa = null,
        string? trangThai = null,
        CancellationToken cancellationToken = default)
    {
        var items = await tuyenBayDAL.GetAllWithAirportsAsync(cancellationToken);
        var query = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            var keyword = tuKhoa.Trim().ToUpperInvariant();
            query = query.Where(x =>
                x.SanBayDi.MaSanBay.ToUpperInvariant().Contains(keyword)
                || x.SanBayDen.MaSanBay.ToUpperInvariant().Contains(keyword)
                || x.SanBayDi.ThanhPho.ToUpperInvariant().Contains(keyword)
                || x.SanBayDen.ThanhPho.ToUpperInvariant().Contains(keyword));
        }

        query = LocTrangThai(query, trangThai, x => x.DangKhaiThac);

        return query.Select(Map).ToArray();
    }

    /// <summary>
    /// Nhận chuỗi người dùng nhập ("TP. Hồ Chí Minh (SGN)", "SGN", "Hà Nội"...)
    /// và trả về sân bay khớp đầu tiên.
    /// </summary>
    public async Task<SanBayDTO?> TimSanBayTheoTuKhoaAsync(
        string? tuKhoa,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tuKhoa))
        {
            return null;
        }

        var value = tuKhoa.Trim();
        var codeMatch = Regex.Match(value, @"\(([A-Za-z]{3})\)");
        var code = codeMatch.Success ? codeMatch.Groups[1].Value : value;

        if (MaSanBayRegex.IsMatch(code.ToUpperInvariant()))
        {
            var byCode = await sanBayDAL.FindByCodeAsync(code, cancellationToken);
            if (byCode is not null)
            {
                return Map(byCode);
            }
        }

        var matches = await sanBayDAL.SearchAsync(value, cancellationToken);
        return matches.Count == 0 ? null : Map(matches[0]);
    }

    public async Task<KetQuaXuLy> LuuSanBayAsync(SanBayDTO dto, CancellationToken cancellationToken = default)
    {
        var ma = dto.MaSanBay.Trim().ToUpperInvariant();
        var ten = dto.TenSanBay.Trim();
        var thanhPho = dto.ThanhPho.Trim();
        var quocGia = dto.QuocGia.Trim();

        if (!MaSanBayRegex.IsMatch(ma))
        {
            return KetQuaXuLy.ThatBai("Mã sân bay phải gồm đúng 3 chữ cái IATA, ví dụ SGN.");
        }

        if (ten.Length == 0 || thanhPho.Length == 0 || quocGia.Length == 0)
        {
            return KetQuaXuLy.ThatBai("Tên sân bay, thành phố và quốc gia là bắt buộc.");
        }

        if (await sanBayDAL.ExistsCodeAsync(ma, dto.Id == 0 ? null : dto.Id, cancellationToken))
        {
            return KetQuaXuLy.ThatBai($"Mã sân bay {ma} đã tồn tại.");
        }

        if (dto.Id == 0)
        {
            await sanBayDAL.AddAsync(new SanBay
            {
                MaSanBay = ma,
                TenSanBay = ten,
                ThanhPho = thanhPho,
                QuocGia = quocGia,
                DangHoatDong = dto.DangHoatDong
            }, cancellationToken);
        }
        else
        {
            var entity = await sanBayDAL.GetByIdAsync(dto.Id, cancellationToken);
            if (entity is null)
            {
                return KetQuaXuLy.ThatBai("Không tìm thấy sân bay cần cập nhật.");
            }

            if (!string.Equals(entity.MaSanBay, ma, StringComparison.Ordinal)
                && await sanBayDAL.IsReferencedAsync(dto.Id, cancellationToken))
            {
                return KetQuaXuLy.ThatBai("Không đổi mã sân bay khi đã được tuyến bay tham chiếu.");
            }

            entity.MaSanBay = ma;
            entity.TenSanBay = ten;
            entity.ThanhPho = thanhPho;
            entity.QuocGia = quocGia;
            entity.DangHoatDong = dto.DangHoatDong;
            sanBayDAL.Update(entity);
        }

        await sanBayDAL.SaveChangesAsync(cancellationToken);
        return KetQuaXuLy.Ok();
    }

    public async Task<KetQuaXuLy> LuuMayBayAsync(MayBayDTO dto, CancellationToken cancellationToken = default)
    {
        var ma = dto.MaMayBay.Trim().ToUpperInvariant();
        var ten = dto.TenMayBay.Trim();
        var hang = dto.HangSanXuat.Trim();

        if (ma.Length == 0 || ten.Length == 0 || hang.Length == 0)
        {
            return KetQuaXuLy.ThatBai("Mã máy bay, tên máy bay và hãng sản xuất là bắt buộc.");
        }

        if (dto.SoGhe <= 0)
        {
            return KetQuaXuLy.ThatBai("Số ghế của máy bay phải lớn hơn 0.");
        }

        if (await mayBayDAL.ExistsCodeAsync(ma, dto.Id == 0 ? null : dto.Id, cancellationToken))
        {
            return KetQuaXuLy.ThatBai($"Mã máy bay {ma} đã tồn tại.");
        }

        if (dto.Id == 0)
        {
            await mayBayDAL.AddAsync(new MayBay
            {
                MaMayBay = ma,
                TenMayBay = ten,
                HangSanXuat = hang,
                SoGhe = dto.SoGhe,
                DangHoatDong = dto.DangHoatDong
            }, cancellationToken);
        }
        else
        {
            var entity = await mayBayDAL.GetByIdAsync(dto.Id, cancellationToken);
            if (entity is null)
            {
                return KetQuaXuLy.ThatBai("Không tìm thấy máy bay cần cập nhật.");
            }

            if (dto.SoGhe < entity.SoGhe)
            {
                var maxUsage = await chuyenBayDAL.GetMaxSeatUsageForAircraftAsync(dto.Id, cancellationToken);
                if (dto.SoGhe < maxUsage)
                {
                    return KetQuaXuLy.ThatBai(
                        $"Không giảm sức chứa xuống {dto.SoGhe} ghế vì đã có chuyến bán/giữ {maxUsage} ghế.");
                }
            }

            entity.MaMayBay = ma;
            entity.TenMayBay = ten;
            entity.HangSanXuat = hang;
            entity.SoGhe = dto.SoGhe;
            entity.DangHoatDong = dto.DangHoatDong;
            mayBayDAL.Update(entity);
        }

        await mayBayDAL.SaveChangesAsync(cancellationToken);
        return KetQuaXuLy.Ok();
    }

    public async Task<KetQuaXuLy> LuuTuyenBayAsync(TuyenBayDTO dto, CancellationToken cancellationToken = default)
    {
        if (dto.SanBayDiId == dto.SanBayDenId)
        {
            return KetQuaXuLy.ThatBai("Sân bay đi và sân bay đến phải khác nhau.");
        }

        if (dto.ThoiLuongDuKienPhut is <= 0)
        {
            return KetQuaXuLy.ThatBai("Thời lượng dự kiến phải lớn hơn 0 phút.");
        }

        var sanBayDi = await sanBayDAL.GetByIdAsync(dto.SanBayDiId, cancellationToken);
        var sanBayDen = await sanBayDAL.GetByIdAsync(dto.SanBayDenId, cancellationToken);
        if (sanBayDi is null || sanBayDen is null)
        {
            return KetQuaXuLy.ThatBai("Sân bay đi hoặc sân bay đến không tồn tại.");
        }

        if (dto.DangKhaiThac && (!sanBayDi.DangHoatDong || !sanBayDen.DangHoatDong))
        {
            return KetQuaXuLy.ThatBai("Không mở tuyến khi sân bay đầu mút đang tạm dừng.");
        }

        if (await tuyenBayDAL.ExistsPairAsync(
                dto.SanBayDiId,
                dto.SanBayDenId,
                dto.Id == 0 ? null : dto.Id,
                cancellationToken))
        {
            return KetQuaXuLy.ThatBai("Tuyến bay theo chiều này đã tồn tại.");
        }

        if (dto.Id == 0)
        {
            await tuyenBayDAL.AddAsync(new TuyenBay
            {
                SanBayDiId = dto.SanBayDiId,
                SanBayDenId = dto.SanBayDenId,
                ThoiLuongDuKienPhut = dto.ThoiLuongDuKienPhut,
                DangKhaiThac = dto.DangKhaiThac
            }, cancellationToken);
        }
        else
        {
            var entity = await tuyenBayDAL.GetByIdAsync(dto.Id, cancellationToken);
            if (entity is null)
            {
                return KetQuaXuLy.ThatBai("Không tìm thấy tuyến bay cần cập nhật.");
            }

            entity.SanBayDiId = dto.SanBayDiId;
            entity.SanBayDenId = dto.SanBayDenId;
            entity.ThoiLuongDuKienPhut = dto.ThoiLuongDuKienPhut;
            entity.DangKhaiThac = dto.DangKhaiThac;
            tuyenBayDAL.Update(entity);
        }

        await tuyenBayDAL.SaveChangesAsync(cancellationToken);
        return KetQuaXuLy.Ok();
    }

    private static IEnumerable<T> LocTrangThai<T>(
        IEnumerable<T> source,
        string? trangThai,
        Func<T, bool> isActive)
    {
        if (string.IsNullOrWhiteSpace(trangThai) || trangThai == "Tất cả")
        {
            return source;
        }

        var active = trangThai.Contains("Hoạt động", StringComparison.OrdinalIgnoreCase)
            || trangThai.Contains("khai thác", StringComparison.OrdinalIgnoreCase)
            || trangThai.Contains("Đang bán", StringComparison.OrdinalIgnoreCase);

        return active ? source.Where(isActive) : source.Where(x => !isActive(x));
    }

    private static HangHangKhongDTO Map(HangHangKhong entity) => new()
    {
        Id = entity.Id,
        MaHang = entity.MaHang,
        TenHang = entity.TenHang,
        QuocGia = entity.QuocGia,
        MaIcao = entity.MaIcao,
        DangHoatDong = entity.DangHoatDong
    };

    private static SanBayDTO Map(SanBay entity) => new()
    {
        Id = entity.Id,
        MaSanBay = entity.MaSanBay,
        TenSanBay = entity.TenSanBay,
        ThanhPho = entity.ThanhPho,
        QuocGia = entity.QuocGia,
        DangHoatDong = entity.DangHoatDong
    };

    private static MayBayDTO Map(MayBay entity) => new()
    {
        Id = entity.Id,
        MaMayBay = entity.MaMayBay,
        TenMayBay = entity.TenMayBay,
        HangSanXuat = entity.HangSanXuat,
        SoGhe = entity.SoGhe,
        DangHoatDong = entity.DangHoatDong
    };

    private static TuyenBayDTO Map(TuyenBay entity) => new()
    {
        Id = entity.Id,
        SanBayDiId = entity.SanBayDiId,
        SanBayDi = Map(entity.SanBayDi),
        SanBayDenId = entity.SanBayDenId,
        SanBayDen = Map(entity.SanBayDen),
        ThoiLuongDuKienPhut = entity.ThoiLuongDuKienPhut,
        DangKhaiThac = entity.DangKhaiThac
    };
}
