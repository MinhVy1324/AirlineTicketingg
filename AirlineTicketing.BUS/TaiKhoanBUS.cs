using AirlineTicketing.DAL;
<<<<<<< Updated upstream
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
=======
using AirlineTicketing.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AirlineTicketing.BUS;

public sealed record KetQuaDangNhap(TaiKhoan? TaiKhoan = null, bool BiKhoa = false);
public sealed record HoSoTaiKhoan(string HoTen, string Email, string? SoDienThoai,
    string? GmailLienHe, bool UsernameAccount, bool NeedsProfile);

public sealed class TaiKhoanBUS(AppDbContext db, TimeProvider clock)
{
    public Task<TaiKhoan?> TimTheoEmailAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.TaiKhoans.AsNoTracking().FirstOrDefaultAsync(a => a.Email.ToLower() == normalized, ct);
    }

    public async Task<bool> TaoKhachHangAsync(string tenDangNhap, string matKhau, CancellationToken ct)
    {
        var username = tenDangNhap.ToLowerInvariant();
        if (await db.TaiKhoans.AnyAsync(a => a.TenDangNhap.ToLower() == username, ct)) return false;
        db.TaiKhoans.Add(new TaiKhoan
        {
            TenDangNhap = username,
            Email = Guid.NewGuid().ToString("N") + TaiKhoan.InternalEmailSuffix,
            HoTen = username,
            MatKhauHash = PasswordHelper.Hash(matKhau),
            VaiTro = VaiTro.KhachHang,
            TrangThai = TrangThaiTaiKhoan.HoatDong,
            NgayTao = clock.GetUtcNow()
        });
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    public async Task<bool> DatLaiMatKhauAsync(TaiKhoan account, string matKhau, CancellationToken ct)
    {
        var hash = PasswordHelper.Hash(matKhau);
        return await ActiveCustomer(account.Id)
            .Where(a => a.MatKhauHash == account.MatKhauHash && a.Email == account.Email)
            .ExecuteUpdateAsync(update => update.SetProperty(a => a.MatKhauHash, hash), ct) == 1;
    }

    public async Task<KetQuaDangNhap> DangNhapAsync(string tenDangNhap, string matKhau, string? diaChiIp, CancellationToken ct)
    {
        var identifier = tenDangNhap.ToLowerInvariant();
        var account = await db.TaiKhoans.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenDangNhap.ToLower() == identifier
                || (!a.Email.EndsWith(TaiKhoan.InternalEmailSuffix) && a.Email.ToLower() == identifier), ct);
        if (account is null || !PasswordHelper.Verify(matKhau, account.MatKhauHash))
        {
            await GhiNhatKyAsync(null, tenDangNhap, "Khách", "Đăng nhập thất bại", tenDangNhap,
                KetQuaNhatKy.ThatBai, diaChiIp, "Sai tên đăng nhập hoặc mật khẩu.", ct);
            return new();
        }
        if (account.TrangThai == TrangThaiTaiKhoan.BiKhoa)
        {
            await GhiNhatKyAsync(account.Id, account.HoTen, account.VaiTro.ToString(), "Đăng nhập bị chặn", account.TenDangNhap,
                KetQuaNhatKy.ThatBai, diaChiIp, "Tài khoản đã bị khóa.", ct);
            return new(BiKhoa: true);
        }

        var customerId = account.KhachHangId;
        if (account.VaiTro == VaiTro.KhachHang && !customerId.HasValue && !account.UsesUsernameRegistration())
        {
            var email = account.Email.ToLowerInvariant();
            customerId = await db.KhachHangs.AsNoTracking().Where(k => k.Email.ToLower() == email)
                .OrderBy(k => k.Id).Select(k => (int?)k.Id).FirstOrDefaultAsync(ct);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var updated = await db.TaiKhoans.Where(a => a.Id == account.Id
                && a.TrangThai == TrangThaiTaiKhoan.HoatDong && a.VaiTro == account.VaiTro
                && a.MatKhauHash == account.MatKhauHash && a.Email == account.Email)
            .ExecuteUpdateAsync(update => update
                .SetProperty(a => a.LanDangNhapCuoi, a => a.LanDangNhapCuoi > now ? a.LanDangNhapCuoi : now)
                .SetProperty(a => a.KhachHangId, a => a.KhachHangId ?? customerId), ct);
        if (updated == 0) return new();

        await GhiNhatKyAsync(account.Id, account.HoTen, account.VaiTro.ToString(), "Đăng nhập", account.TenDangNhap,
            KetQuaNhatKy.ThanhCong, diaChiIp, null, ct);
        var signedIn = await db.TaiKhoans.AsNoTracking().SingleAsync(a => a.Id == account.Id, ct);
        await transaction.CommitAsync(ct);
        return new(signedIn);
    }

    public async Task<HoSoTaiKhoan?> LayHoSoAsync(int id, CancellationToken ct)
    {
        var account = await ActiveCustomer(id).AsNoTracking().SingleOrDefaultAsync(ct);
        return account is null ? null : new(account.NeedsProfile() ? "" : account.HoTen, account.Email,
            account.SoDienThoai, account.GmailLienHe, account.UsesUsernameRegistration(), account.NeedsProfile());
    }

    public async Task<bool> CapNhatHoSoAsync(int id, string hoTen, string? soDienThoai, string? gmail, string? diaChiIp, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var account = await ActiveCustomer(id).AsNoTracking().SingleOrDefaultAsync(ct);
        if (account is null) return false;
        // An unverified contact address never identifies a booking owner.
        int? customerId = account.KhachHangId;
        if (account.NeedsProfile())
        {
            var customer = new KhachHang { HoTen = hoTen, Email = account.Email, SoDienThoai = soDienThoai!, NgayTao = clock.GetUtcNow() };
            db.KhachHangs.Add(customer);
            await db.SaveChangesAsync(ct);
            customerId = customer.Id;
        }
        var updated = await ActiveCustomer(id).Where(a => a.KhachHangId == account.KhachHangId)
            .ExecuteUpdateAsync(update => update
            .SetProperty(a => a.HoTen, hoTen)
            .SetProperty(a => a.SoDienThoai, soDienThoai)
            .SetProperty(a => a.GmailLienHe, a => account.UsesUsernameRegistration() ? gmail : a.GmailLienHe)
            .SetProperty(a => a.KhachHangId, a => a.KhachHangId ?? customerId), ct);
        if (updated == 0) return false;
        await GhiNhatKyAsync(id, hoTen, VaiTro.KhachHang.ToString(), "Cập nhật hồ sơ", "Hồ sơ cá nhân",
            KetQuaNhatKy.ThanhCong, diaChiIp, null, ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    public Task GhiNhatKyAsync(int? id, string hoTen, string vaiTro, string hanhDong, string doiTuong,
        KetQuaNhatKy ketQua, string? diaChiIp, string? chiTiet, CancellationToken ct)
    {
        db.NhatKyHoatDongs.Add(new NhatKyHoatDong
        {
            MaNhatKy = NhatKyHoatDong.TaoMa(),
            TaiKhoanId = id,
            TenNguoiDung = GioiHan(hoTen, 150),
            VaiTro = vaiTro,
            HanhDong = hanhDong,
            DoiTuong = GioiHan(doiTuong, 100),
            KetQua = ketQua,
            ChiTiet = chiTiet,
            DiaChiIp = diaChiIp,
            ThoiGian = clock.GetUtcNow()
        });
        return db.SaveChangesAsync(ct);
    }

    private IQueryable<TaiKhoan> ActiveCustomer(int id) => db.TaiKhoans.Where(a =>
        a.Id == id && a.VaiTro == VaiTro.KhachHang && a.TrangThai == TrangThaiTaiKhoan.HoatDong);

    private static string GioiHan(string value, int length)
    {
        var clean = value.Replace("\0", "");
        return string.Concat(clean.EnumerateRunes().Take(length).Select(rune => rune.ToString()));
    }
>>>>>>> Stashed changes
}
