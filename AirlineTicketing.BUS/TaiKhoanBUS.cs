using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Security;
using AirlineTicketing.Domain;
using AirlineTicketing.DTO;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.BUS;

public sealed class TaiKhoanBUS(AppDbContext dbContext) : ITaiKhoanBUS
{
    public async Task<TaiKhoanDTO> DangKyAsync(DangKyDTO dto, CancellationToken cancellationToken = default)
    {
        string normalizedUsername = dto.TenDangNhap.Trim().ToLowerInvariant();
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (await dbContext.TaiKhoans.AnyAsync(x => x.TenDangNhap.ToLower() == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException($"Tên đăng nhập '{dto.TenDangNhap}' đã tồn tại.");
        }

        if (await dbContext.TaiKhoans.AnyAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException($"Email '{dto.Email}' đã được đăng ký.");
        }

        // Tạo hồ sơ khách hàng trước
        var khachHang = new KhachHang
        {
            HoTen = dto.HoTen.Trim(),
            Email = normalizedEmail,
            SoDienThoai = dto.SoDienThoai.Trim(),
            NgayTao = DateTimeOffset.UtcNow
        };
        var taiKhoan = new TaiKhoan
        {
            TenDangNhap = normalizedUsername,
            MatKhauHash = PasswordSecurity.HashPassword(dto.MatKhau),
            HoTen = dto.HoTen.Trim(),
            Email = normalizedEmail,
            SoDienThoai = dto.SoDienThoai.Trim(),
            VaiTro = VaiTroTaiKhoan.KhachHang,
            TrangThai = TrangThaiTaiKhoan.HoatDong,
            NgayTao = DateTimeOffset.UtcNow,
            KhachHang = khachHang
        };

        await dbContext.TaiKhoans.AddAsync(taiKhoan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return MapToDTO(taiKhoan);
    }

    public async Task<TaiKhoanDTO?> DangNhapAsync(string tenDangNhap, string matKhau, CancellationToken cancellationToken = default)
    {
        string normalized = tenDangNhap.Trim().ToLowerInvariant();

        var taiKhoan = await dbContext.TaiKhoans
            .FirstOrDefaultAsync(x => x.TenDangNhap.ToLower() == normalized || x.Email.ToLower() == normalized, cancellationToken);

        if (taiKhoan == null)
        {
            return null;
        }

        if (taiKhoan.TrangThai == TrangThaiTaiKhoan.BiKhoa)
        {
            throw new InvalidOperationException("Tài khoản của bạn đang bị tạm khóa. Vui lòng liên hệ quản trị viên.");
        }

        if (!PasswordSecurity.VerifyPassword(matKhau, taiKhoan.MatKhauHash))
        {
            return null;
        }

        taiKhoan.LanDangNhapCuoi = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToDTO(taiKhoan);
    }

    public async Task<TaiKhoanDTO?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var taiKhoan = await dbContext.TaiKhoans.FindAsync([id], cancellationToken);
        return taiKhoan != null ? MapToDTO(taiKhoan) : null;
    }

    public async Task<TaiKhoanDTO?> GetByTenDangNhapAsync(string tenDangNhap, CancellationToken cancellationToken = default)
    {
        string normalized = tenDangNhap.Trim().ToLowerInvariant();
        var taiKhoan = await dbContext.TaiKhoans
            .FirstOrDefaultAsync(x => x.TenDangNhap.ToLower() == normalized, cancellationToken);
        return taiKhoan != null ? MapToDTO(taiKhoan) : null;
    }

    public async Task<IReadOnlyList<TaiKhoanDTO>> GetAllAsync(
        string? vaiTro = null,
        string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<TaiKhoan> query = dbContext.TaiKhoans.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(vaiTro) && Enum.TryParse<VaiTroTaiKhoan>(vaiTro, true, out var roleEnum))
        {
            query = query.Where(x => x.VaiTro == roleEnum);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string lowerKw = keyword.Trim().ToLower();
            query = query.Where(x => x.TenDangNhap.ToLower().Contains(lowerKw)
                                  || x.HoTen.ToLower().Contains(lowerKw)
                                  || x.Email.ToLower().Contains(lowerKw)
                                  || x.SoDienThoai.Contains(lowerKw));
        }

        var list = await query
            .OrderByDescending(x => x.NgayTao)
            .ToListAsync(cancellationToken);

        return list.Select(MapToDTO).ToList();
    }

    public async Task<TaiKhoanDTO> TaoTaiKhoanNoiBoAsync(TaoNhanVienDTO dto, CancellationToken cancellationToken = default)
    {
        string normalizedUsername = dto.TenDangNhap.Trim().ToLowerInvariant();
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        if (await dbContext.TaiKhoans.AnyAsync(x => x.TenDangNhap.ToLower() == normalizedUsername, cancellationToken))
        {
            throw new InvalidOperationException($"Tên đăng nhập '{dto.TenDangNhap}' đã tồn tại.");
        }

        if (await dbContext.TaiKhoans.AnyAsync(x => x.Email.ToLower() == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException($"Email '{dto.Email}' đã được đăng ký.");
        }

        if (!Enum.TryParse<VaiTroTaiKhoan>(dto.VaiTro, true, out var vaiTro) || !Enum.IsDefined(vaiTro))
        {
            throw new InvalidOperationException("Vai trò tài khoản không hợp lệ.");
        }

        var taiKhoan = new TaiKhoan
        {
            TenDangNhap = normalizedUsername,
            MatKhauHash = PasswordSecurity.HashPassword(dto.MatKhau),
            HoTen = dto.HoTen.Trim(),
            Email = normalizedEmail,
            SoDienThoai = dto.SoDienThoai.Trim(),
            VaiTro = vaiTro,
            TrangThai = TrangThaiTaiKhoan.HoatDong,
            NgayTao = DateTimeOffset.UtcNow
        };

        await dbContext.TaiKhoans.AddAsync(taiKhoan, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToDTO(taiKhoan);
    }

    public async Task<bool> CapNhatHoSoAsync(int taiKhoanId, CapNhatHoSoDTO dto, CancellationToken cancellationToken = default)
    {
        var taiKhoan = await dbContext.TaiKhoans
            .Include(x => x.KhachHang)
            .FirstOrDefaultAsync(x => x.Id == taiKhoanId, cancellationToken);

        if (taiKhoan == null)
        {
            return false;
        }

        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        if (await dbContext.TaiKhoans.AnyAsync(x => x.Id != taiKhoanId && x.Email.ToLower() == normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException($"Email '{dto.Email}' đã được sử dụng bởi tài khoản khác.");
        }

        taiKhoan.HoTen = dto.HoTen.Trim();
        taiKhoan.Email = normalizedEmail;
        taiKhoan.SoDienThoai = dto.SoDienThoai.Trim();

        if (taiKhoan.KhachHang != null)
        {
            taiKhoan.KhachHang.HoTen = taiKhoan.HoTen;
            taiKhoan.KhachHang.Email = taiKhoan.Email;
            taiKhoan.KhachHang.SoDienThoai = taiKhoan.SoDienThoai;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DoiMatKhauAsync(int taiKhoanId, DoiMatKhauDTO dto, CancellationToken cancellationToken = default)
    {
        var taiKhoan = await dbContext.TaiKhoans.FindAsync([taiKhoanId], cancellationToken);
        if (taiKhoan == null)
        {
            return false;
        }

        if (!PasswordSecurity.VerifyPassword(dto.MatKhauHienTai, taiKhoan.MatKhauHash))
        {
            throw new InvalidOperationException("Mật khẩu hiện tại không chính xác.");
        }

        taiKhoan.MatKhauHash = PasswordSecurity.HashPassword(dto.MatKhauMoi);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DatLaiMatKhauAsync(int taiKhoanId, string matKhauMoi, CancellationToken cancellationToken = default)
    {
        var taiKhoan = await dbContext.TaiKhoans.FindAsync([taiKhoanId], cancellationToken);
        if (taiKhoan == null)
        {
            return false;
        }

        taiKhoan.MatKhauHash = PasswordSecurity.HashPassword(matKhauMoi);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CapNhatTrangThaiAsync(int taiKhoanId, TrangThaiTaiKhoan trangThai, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(trangThai))
        {
            throw new InvalidOperationException("Trạng thái tài khoản không hợp lệ.");
        }

        var taiKhoan = await dbContext.TaiKhoans.FindAsync([taiKhoanId], cancellationToken);
        if (taiKhoan == null)
        {
            return false;
        }

        if (taiKhoan.VaiTro == VaiTroTaiKhoan.Admin
            && taiKhoan.TrangThai == TrangThaiTaiKhoan.HoatDong
            && trangThai == TrangThaiTaiKhoan.BiKhoa
            && await dbContext.TaiKhoans.CountAsync(
                x => x.VaiTro == VaiTroTaiKhoan.Admin && x.TrangThai == TrangThaiTaiKhoan.HoatDong,
                cancellationToken) <= 1)
        {
            throw new InvalidOperationException("Không thể khóa quản trị viên đang hoạt động cuối cùng.");
        }

        taiKhoan.TrangThai = trangThai;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CapNhatVaiTroAsync(int taiKhoanId, VaiTroTaiKhoan vaiTro, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(vaiTro))
        {
            throw new InvalidOperationException("Vai trò tài khoản không hợp lệ.");
        }

        var taiKhoan = await dbContext.TaiKhoans.FindAsync([taiKhoanId], cancellationToken);
        if (taiKhoan == null)
        {
            return false;
        }

        if (taiKhoan.VaiTro == VaiTroTaiKhoan.Admin
            && taiKhoan.TrangThai == TrangThaiTaiKhoan.HoatDong
            && vaiTro != VaiTroTaiKhoan.Admin
            && await dbContext.TaiKhoans.CountAsync(
                x => x.VaiTro == VaiTroTaiKhoan.Admin && x.TrangThai == TrangThaiTaiKhoan.HoatDong,
                cancellationToken) <= 1)
        {
            throw new InvalidOperationException("Không thể hạ quyền quản trị viên đang hoạt động cuối cùng.");
        }

        taiKhoan.VaiTro = vaiTro;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TaiKhoanDTO MapToDTO(TaiKhoan entity) => new()
    {
        Id = entity.Id,
        TenDangNhap = entity.TenDangNhap,
        HoTen = entity.HoTen,
        Email = entity.Email,
        SoDienThoai = entity.SoDienThoai,
        VaiTro = entity.VaiTro.ToString(),
        TrangThai = entity.TrangThai.ToString(),
        NgayTao = entity.NgayTao,
        LanDangNhapCuoi = entity.LanDangNhapCuoi,
        KhachHangId = entity.KhachHangId
    };
}
