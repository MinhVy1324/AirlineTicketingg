using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AirlineTicketing.DAL;
using AirlineTicketing.Domain;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketing.Tests;

[Collection(PostgresCollection.Name)]
public sealed class FlightFilteringHttpTests
{
    [PostgresFact]
    public async Task LocKetHop_DungHangThucTe_KhungGio_VaGiaDaGomThuePhi()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var data = await SeedAsync(db, host);
        using var client = host.CreateClient();
        var html = await client.GetStringAsync(data.Url + $"&airlines={data.AirlineA}&times=morning&minPrice=1500000&maxPrice=1500000");
        Assert.Equal(new[] { data.Flights["morningA"] }, FlightIds(html));
        Assert.Contains(data.NameA, WebUtility.HtmlDecode(html));
        Assert.Contains("data-result-count=\"1\"", html);
        Assert.Contains("data-available-count=\"8\"", html);
        Assert.Contains("1.500.000", html);
    }

    [PostgresFact]
    public async Task NhieuHangVaNhieuKhungGio_DungPhepHoac_ThuTuSapXepKhongMatBoLoc()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var data = await SeedAsync(db, host);
        using var client = host.CreateClient();
        var query = $"&airlines={data.AirlineA.ToLowerInvariant()}&airlines={data.AirlineB}&times=night&times=evening";
        var byPrice = await client.GetStringAsync(data.Url + query + "&sort=price");
        Assert.Equal(new[] { data.Flights["nightA"], data.Flights["eveningB"], data.Flights["eveningA"], data.Flights["nightB"] }, FlightIds(byPrice));
        var byDeparture = await client.GetStringAsync(data.Url + query + "&sort=departure");
        Assert.Equal(new[] { data.Flights["nightA"], data.Flights["nightB"], data.Flights["eveningA"], data.Flights["eveningB"] }, FlightIds(byDeparture));
        Assert.Contains("form=\"flight-filter-form\"", byDeparture);
        Assert.Equal(2, Facets(byDeparture, "airlines").Count(pair => pair.Value.Selected));
        Assert.Equal(2, Facets(byDeparture, "times").Count(pair => pair.Value.Selected));
    }

    [PostgresFact]
    public async Task SoDemBoLoc_LayTuDuLieu_VaTinhTheoCacDieuKienKhac()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var data = await SeedAsync(db, host);
        using var client = host.CreateClient();
        var html = await client.GetStringAsync(data.Url + $"&airlines={data.AirlineA}&times=morning");
        var airlines = Facets(html, "airlines");
        Assert.Equal((1, true), airlines[data.AirlineA]);
        Assert.Equal((1, false), airlines[data.AirlineB]);
        var times = Facets(html, "times");
        Assert.Equal((1, true), times["morning"]);
        Assert.Equal((1, false), times["night"]);
        Assert.Equal((1, false), times["afternoon"]);
        Assert.Equal((1, false), times["evening"]);
    }

    [PostgresFact]
    public async Task DoiNgayTuan_GiuLuaChon_VaGiaTheoNgayTinhDungSoKhach()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var data = await SeedAsync(db, host);
        using var client = host.CreateClient();
        var url = data.Url.Replace("passengers=1", "passengers=2")
            + $"&airlines={data.AirlineA}&times=morning&minPrice=1000000&maxPrice=1750000&sort=price";
        var html = await client.GetStringAsync(url);
        var dayLinks = Regex.Matches(html, "<a class=\"price-day[^\"]*\"[\\s\\S]*?</a>").Select(match => match.Value).ToArray();
        Assert.Equal(7, dayLinks.Length);
        foreach (var tag in dayLinks.Concat(Regex.Matches(html, "<a class=\"price-week__arrow\"[\\s\\S]*?</a>").Select(match => match.Value)))
        {
            var query = QueryHelpers.ParseQuery(new Uri("http://localhost" + Href(tag)).Query);
            Assert.Equal(data.AirlineA, query["airlines"].ToString());
            Assert.Equal("morning", query["times"].ToString());
            Assert.Equal("2", query["passengers"].ToString());
            Assert.Equal("1000000", query["minPrice"].ToString());
            Assert.Equal("1750000", query["maxPrice"].ToString());
            Assert.Equal("price", query["sort"].ToString());
        }
        var next = dayLinks.Single(tag => QueryHelpers.ParseQuery(new Uri("http://localhost" + Href(tag)).Query)["depart"] == data.Day.AddDays(1).ToString("dd/MM/yyyy"));
        Assert.Contains("1.700.000", next);
        var nextHtml = await client.GetStringAsync(Href(next));
        Assert.Equal(new[] { data.Flights["nextA"] }, FlightIds(nextHtml));
    }

    [PostgresFact]
    public async Task KhongCoKetQua_VaDatLai_KhongLamMatTuyenNgaySoKhach()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var data = await SeedAsync(db, host);
        using var client = host.CreateClient();
        var html = await client.GetStringAsync(data.Url + $"&airlines={data.AirlineA}&maxPrice=0&sort=departure");
        Assert.Empty(FlightIds(html));
        Assert.Contains("data-result-count=\"0\"", html);
        Assert.Contains("data-available-count=\"8\"", html);
        Assert.Equal((0, true), Facets(html, "airlines")[data.AirlineA]);
        var clearTag = Regex.Match(html, "<a[^>]*href=\"([^\"]+)\"[^>]*>Đặt lại</a>").Value;
        var clearUrl = Href(clearTag);
        var query = QueryHelpers.ParseQuery(new Uri("http://localhost" + clearUrl).Query);
        Assert.Equal(data.From, query["from"].ToString());
        Assert.Equal(data.To, query["to"].ToString());
        Assert.Equal(data.Day.ToString("dd/MM/yyyy"), query["depart"].ToString());
        Assert.Equal("1", query["passengers"].ToString());
        Assert.Equal("departure", query["sort"].ToString());
        Assert.False(query.ContainsKey("airlines"));
        Assert.False(query.ContainsKey("maxPrice"));
        Assert.Equal(8, FlightIds(await client.GetStringAsync(clearUrl)).Length);
    }

    [PostgresFact]
    public async Task NgayKhongCoChuyen_VanGiuHangDaChonDeKhachCoTheBoChon()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var data = await SeedAsync(db, host);
        using var client = host.CreateClient();
        var html = await client.GetStringAsync(data.Url.Replace(data.Day.ToString("yyyy-MM-dd"), data.Day.AddDays(2).ToString("yyyy-MM-dd"))
            + $"&airlines={data.AirlineA}");
        Assert.Empty(FlightIds(html));
        Assert.Equal((0, true), Facets(html, "airlines")[data.AirlineA]);
        Assert.Contains(data.NameA, WebUtility.HtmlDecode(html));
    }

    [PostgresFact]
    public async Task GiaHoacKhungGioSai_HienLoi_KhongTraKetQuaNgoaiYKhach()
    {
        await using var host = await PostgresTestHost.CreateAsync();
        await using var db = Database();
        var data = await SeedAsync(db, host);
        using var client = host.CreateClient();
        foreach (var (query, field) in new[] { ("minPrice=2000000&maxPrice=1000000", "priceRange"),
            ("minPrice=abc", "minPrice"), ("maxPrice=-1", "maxPrice"), ("times=unknown", "times"),
            ("airlines=%3Cscript%3Ealert(1)%3C%2Fscript%3E", "airlines") })
        {
            using var response = await client.GetAsync(data.Url + "&" + query);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Empty(FlightIds(html));
            Assert.Contains("data-validation-summary", html);
            Assert.Contains($"href=\"#{field}\"", html);
            Assert.DoesNotContain("<SCRIPT>ALERT(1)</SCRIPT>", html);
        }
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(PostgresTestConnection.Require()).Options);

    private static int[] FlightIds(string html) => Regex.Matches(html, "data-flight-id=\"(\\d+)\"")
        .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)).ToArray();
    private static string Href(string html)
    {
        var value = Regex.Match(html, "href=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(value);
        return WebUtility.HtmlDecode(value);
    }
    private static Dictionary<string, (int Count, bool Selected)> Facets(string html, string name) =>
        Regex.Matches(html, "<label class=\"av-check\">[\\s\\S]*?</label>")
            .Select(match => match.Value).Where(tag => tag.Contains($"name=\"{name}\""))
            .ToDictionary(tag => Regex.Match(tag, "value=\"([^\"]+)\"").Groups[1].Value,
                tag => (int.Parse(Regex.Match(tag, "filter-count\">\\((\\d+)\\)").Groups[1].Value, CultureInfo.InvariantCulture),
                    Regex.IsMatch(tag, "<input[^>]*\\bchecked(?:=|\\s|/|>)")));

    private static async Task<SearchData> SeedAsync(AppDbContext db, PostgresTestHost host)
    {
        var original = await db.ChuyenBays.Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDi)
            .Include(x => x.TuyenBay).ThenInclude(x => x.SanBayDen).Include(x => x.MayBay)
            .SingleAsync(x => x.Ves.Any(v => v.DatVe.MaDatVe == host.Data.PnrA));
        var marker = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        var a = new HangHangKhong { MaHang = "A" + marker, TenHang = "Hang Binh Minh " + marker, QuocGia = "VN" };
        var b = new HangHangKhong { MaHang = "B" + marker, TenHang = "Hang Hoang Hon " + marker, QuocGia = "VN" };
        var day = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date).AddDays(50);
        day = day.AddDays((9 - (int)day.DayOfWeek) % 7); // Thứ Ba, để ngày kế tiếp cùng tuần.
        var flights = new Dictionary<string, ChuyenBay>();
        void Add(string key, int dayOffset, int hour, int minute, decimal fare, HangHangKhong airline, bool oneSeat = false)
        {
            var dep = new DateTimeOffset(day.AddDays(dayOffset).ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(7));
            flights[key] = new ChuyenBay
            {
                SoHieu = "XX" + marker + flights.Count, TuyenBay = original.TuyenBay,
                MayBay = oneSeat ? new MayBay { MaMayBay = "ONE" + marker, TenMayBay = "One seat", HangSanXuat = "Test", SoGhe = 1 } : original.MayBay,
                HangHangKhong = airline, GioKhoiHanh = dep.ToUniversalTime(), GioHaCanh = dep.AddHours(2).ToUniversalTime(),
                GiaCoBan = fare, TrangThai = TrangThaiChuyenBay.DuKien
            };
        }
        Add("nightA", 0, 0, 30, 950_000m, a);
        Add("nightB", 0, 4, 59, 1_500_000m, b);
        Add("morningA", 0, 5, 0, 1_050_000m, a);
        Add("morningB", 0, 11, 59, 800_000m, b);
        Add("afternoonA", 0, 12, 0, 1_200_000m, a);
        Add("afternoonB", 0, 17, 59, 1_300_000m, b);
        Add("eveningA", 0, 18, 0, 1_400_000m, a);
        Add("eveningB", 0, 23, 59, 1_000_000m, b);
        Add("nextA", 1, 5, 0, 1_250_000m, a);
        Add("nextB", 1, 9, 0, 400_000m, b);
        Add("nextLimited", 1, 5, 10, 100_000m, a, oneSeat: true);
        db.ChuyenBays.AddRange(flights.Values);
        await db.SaveChangesAsync();
        return new(day, original.TuyenBay.SanBayDi.MaSanBay, original.TuyenBay.SanBayDen.MaSanBay,
            a.MaHang, b.MaHang, a.TenHang, flights.ToDictionary(pair => pair.Key, pair => pair.Value.Id));
    }

    private sealed record SearchData(DateOnly Day, string From, string To, string AirlineA, string AirlineB,
        string NameA, Dictionary<string, int> Flights)
    {
        public string Url => $"/chuyen-bay?from={From}&to={To}&depart={Day:yyyy-MM-dd}&passengers=1";
    }
}
