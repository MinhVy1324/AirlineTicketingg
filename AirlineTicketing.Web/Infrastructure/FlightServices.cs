using AirlineTicketing.BUS;
using AirlineTicketing.DAL;
using AirlineTicketing.DAL.Repositories;

namespace AirlineTicketing.Web.Infrastructure;

/// <summary>
/// Dựng các lớp nghiệp vụ từ <see cref="AppDbContext"/> đã đăng ký sẵn trong DI.
/// Cách này giữ nguyên Program.cs dùng chung của nhóm trong khi vẫn tách BUS/DAL đúng kiến trúc.
/// </summary>
public static class FlightServices
{
    public static ChuyenBayBUS ChuyenBay(AppDbContext dbContext) => new(
        new ChuyenBayDAL(dbContext),
        new SanBayDAL(dbContext),
        new MayBayDAL(dbContext),
        new TuyenBayDAL(dbContext));

    public static DanhMucBUS DanhMuc(AppDbContext dbContext) => new(
        new SanBayDAL(dbContext),
        new MayBayDAL(dbContext),
        new TuyenBayDAL(dbContext),
        new ChuyenBayDAL(dbContext));
}
