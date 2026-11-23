using AirlineTicketing.Web.Infrastructure;

namespace AirlineTicketing.Tests;

public sealed class FlightFiltersTests
{
    [Theory]
    [InlineData(0, 0, "night")]
    [InlineData(4, 59, "night")]
    [InlineData(5, 0, "morning")]
    [InlineData(11, 59, "morning")]
    [InlineData(12, 0, "afternoon")]
    [InlineData(17, 59, "afternoon")]
    [InlineData(18, 0, "evening")]
    [InlineData(23, 59, "evening")]
    public void KhungGio_DungRanhGioVietNam_KhongBoSotHoacChongLan(int hour, int minute, string expected)
    {
        var departureUtc = new DateTimeOffset(2026, 12, 1, hour, minute, 0, TimeSpan.FromHours(7)).ToUniversalTime();
        Assert.Equal(expected, Assert.Single(FlightFilters.Windows, window => window.Contains(departureUtc)).Key);
    }

    [Theory]
    [InlineData("-1", null, "minPrice")]
    [InlineData(null, "abc", "maxPrice")]
    [InlineData("1.500.000", null, "minPrice")]
    [InlineData("10000000000", null, "minPrice")]
    [InlineData("2000000", "1000000", "priceRange")]
    [InlineData("1e20", null, "minPrice")]
    public void GiaKhongHopLe_ThongBaoLoi_KhongTuDoiYKhach(string? min, string? max, string field)
    {
        var filters = FlightFilters.Parse(minPrice: min, maxPrice: max);
        Assert.False(filters.IsValid);
        Assert.Contains(field, filters.Errors.Keys);
        Assert.Equal(min ?? "", filters.MinPriceText);
        Assert.Equal(max ?? "", filters.MaxPriceText);
    }

    [Fact]
    public void LuaChonLapVaRong_DuocChuanHoa_GiaKhongBangVoiBoGioiHan()
    {
        var filters = FlightFilters.Parse([" vn ", "VN,VJ", ""], ["MORNING", "morning", ""], maxPrice: "0");
        Assert.True(filters.IsValid);
        Assert.Equal(new[] { "VJ", "VN" }, filters.Airlines);
        Assert.Equal(new[] { "morning" }, filters.Times);
        Assert.Equal(0m, filters.MaxPrice);
    }
}
