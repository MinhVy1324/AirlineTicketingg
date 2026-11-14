using AirlineTicketing.Domain;
using AirlineTicketing.DTO;

namespace AirlineTicketing.BUS;

internal static class DatVeMapper
{
    public static DatVeDTO ToDto(DatVe value) => new()
    {
        Id = value.Id,
        MaDatVe = value.MaDatVe,
        NgayDat = value.NgayDat,
        HetHanGiuCho = value.HetHanGiuCho,
        TrangThai = value.TrangThai.ToString(),
        TongTien = value.TongTien,
        DonViTienTe = value.DonViTienTe,
        KhachHang = new KhachHangDTO
        {
            Id = value.KhachHang.Id,
            HoTen = value.KhachHang.HoTen,
            Email = value.KhachHang.Email,
            SoDienThoai = value.KhachHang.SoDienThoai
        },
        Ves = value.Ves.Select(ToDto).ToArray(),
        ThanhToans = value.ThanhToans.OrderBy(x => x.NgayTao).Select(x => new ThanhToanDTO
        {
            Id = x.Id,
            SoTien = x.SoTien,
            DonViTienTe = x.DonViTienTe,
            PhuongThuc = x.PhuongThuc.ToString(),
            NhaCungCap = x.NhaCungCap,
            TrangThai = x.TrangThai.ToString(),
            MaGiaoDich = x.MaGiaoDich,
            NgayTao = x.NgayTao,
            NgayHoanTat = x.NgayHoanTat
        }).ToArray()
    };

    public static VeDTO ToDto(Ve value) => new()
    {
        Id = value.Id,
        MaVe = value.MaVe,
        MaDatVe = value.DatVe?.MaDatVe ?? string.Empty,
        SoHieuChuyenBay = value.ChuyenBay?.SoHieu ?? string.Empty,
        GioKhoiHanh = value.ChuyenBay?.GioKhoiHanh ?? default,
        GioHaCanh = value.ChuyenBay?.GioHaCanh ?? default,
        MaSanBayDi = value.ChuyenBay?.TuyenBay?.SanBayDi?.MaSanBay ?? string.Empty,
        TenThanhPhoDi = value.ChuyenBay?.TuyenBay?.SanBayDi?.ThanhPho ?? string.Empty,
        MaSanBayDen = value.ChuyenBay?.TuyenBay?.SanBayDen?.MaSanBay ?? string.Empty,
        TenThanhPhoDen = value.ChuyenBay?.TuyenBay?.SanBayDen?.ThanhPho ?? string.Empty,
        SoGhe = value.SoGhe,
        HangVe = value.HangVe,
        GiaVe = value.GiaVe,
        TrangThai = value.TrangThai.ToString(),
        HanhKhach = new HanhKhachDTO
        {
            Id = value.HanhKhach.Id,
            HoTen = value.HanhKhach.HoTen,
            NgaySinh = value.HanhKhach.NgaySinh,
            QuocTich = value.HanhKhach.QuocTich,
            SoHoChieu = value.HanhKhach.SoHoChieu
        }
    };
}
