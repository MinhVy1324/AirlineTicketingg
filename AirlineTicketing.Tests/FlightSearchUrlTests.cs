using AirlineTicketing.Web.Infrastructure;
using AirlineTicketing.Web.ViewModels;
using Microsoft.AspNetCore.WebUtilities;

namespace AirlineTicketing.Tests;

public sealed class FlightSearchUrlTests
{
    [Fact]
    public void DoiNgay_GiuNhieuLuaChon_VaDatLaiChiXoaBoLoc()
    {
        var model = new FlightResultsViewModel
        {
            Criteria = new FlightSearchCriteria
            {
                OriginCode = "DAD", DestinationCode = "PQC", DepartDateText = "20/11/2026",
                Passengers = 3, Cabin = "Phổ thông", TripTypeText = "Một chiều"
            },
            SortKey = FlightSort.Duration,
            Filters = FlightFilters.Parse(["VN", "VJ"], ["morning", "evening"], "500000", "3000000")
        };
        var changed = new Uri("http://localhost" + model.SearchUrl(new DateOnly(2026, 11, 25)));
        var query = QueryHelpers.ParseQuery(changed.Query);
        Assert.Equal(new[] { "VJ", "VN" }, query["airlines"].ToArray());
        Assert.Equal(new[] { "evening", "morning" }, query["times"].ToArray());
        Assert.Equal("25/11/2026", query["depart"].ToString());
        Assert.Equal("Phổ thông", query["cabin"].ToString());
        Assert.Equal("3", query["passengers"].ToString());
        Assert.Equal("500000", query["minPrice"].ToString());
        Assert.Equal("3000000", query["maxPrice"].ToString());
        var cleared = QueryHelpers.ParseQuery(new Uri("http://localhost" + model.SearchUrl(clearFilters: true)).Query);
        Assert.Equal("DAD", cleared["from"].ToString());
        Assert.Equal("PQC", cleared["to"].ToString());
        Assert.Equal("20/11/2026", cleared["depart"].ToString());
        Assert.Equal("3", cleared["passengers"].ToString());
        Assert.Equal("duration", cleared["sort"].ToString());
        Assert.False(cleared.ContainsKey("airlines"));
        Assert.False(cleared.ContainsKey("times"));
        Assert.False(cleared.ContainsKey("minPrice"));
        Assert.False(cleared.ContainsKey("maxPrice"));
    }
}
