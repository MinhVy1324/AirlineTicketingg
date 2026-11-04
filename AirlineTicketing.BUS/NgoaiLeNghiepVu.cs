namespace AirlineTicketing.BUS;

public class NgoaiLeNghiepVu(string maLoi, string message) : InvalidOperationException(message)
{
    public string MaLoi { get; } = maLoi;
}

public sealed class GheKhongConException(string soGhe)
    : NgoaiLeNghiepVu("GHE_VUA_DUOC_GIU", $"Ghế {soGhe} vừa được người khác giữ.");

public sealed class KhongTimThayException(string doiTuong)
    : NgoaiLeNghiepVu("KHONG_TIM_THAY", $"Không tìm thấy {doiTuong}.");
