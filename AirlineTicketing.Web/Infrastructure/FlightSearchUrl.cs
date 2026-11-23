using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using AirlineTicketing.Web.ViewModels;

namespace AirlineTicketing.Web.Infrastructure;

public static class FlightSearchUrl
{
    public static string Build(FlightResultsViewModel model, DateOnly? date = null, bool clearFilters = false) =>
        QueryHelpers.AddQueryString("/chuyen-bay", Parameters(model, date, clearFilters));

    public static string Select(FlightResultsViewModel model, int flightId)
    {
        var criteria = model.Criteria;
        if (criteria.TripTypeText == "Khứ hồi" && !criteria.SelectingReturn)
        {
            var query = Parameters(model);
            query["outboundId"] = flightId.ToString();
            return QueryHelpers.AddQueryString("/chuyen-bay", query);
        }
        var party = criteria.EffectiveParty;
        var booking = new Dictionary<string, string?>
        {
            ["flightId"] = (criteria.SelectedOutboundId ?? flightId).ToString(),
            ["cabin"] = criteria.Cabin, ["adults"] = party.Adults.ToString(),
            ["children"] = party.Children.ToString(), ["infants"] = party.Infants.ToString()
        };
        if (criteria.SelectingReturn) booking["returnFlightId"] = flightId.ToString();
        return QueryHelpers.AddQueryString("/dat-ve/hanh-khach", booking);
    }
    public static Dictionary<string, StringValues> Parameters(FlightResultsViewModel model, DateOnly? date = null, bool clearFilters = false)
    {
        var criteria = model.Criteria;
        var query = new Dictionary<string, StringValues>
        {
            ["from"] = criteria.JourneyOriginCode ?? (criteria.OriginCode == "---" ? criteria.OriginCity : criteria.OriginCode),
            ["to"] = criteria.JourneyDestinationCode ?? (criteria.DestinationCode == "---" ? criteria.DestinationCity : criteria.DestinationCode),
            ["depart"] = criteria.SelectingReturn ? criteria.OutboundDateText! : date.HasValue ? FlightMapping.DisplayDate(date.Value) : criteria.DepartDateText,
            ["passengers"] = criteria.Passengers.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cabin"] = criteria.Cabin,
            ["trip"] = criteria.TripTypeText == "Khứ hồi" ? "roundtrip" : "oneway",
            ["sort"] = model.SortKey
        };
        var party = criteria.EffectiveParty;
        query["adults"] = party.Adults.ToString();
        query["children"] = party.Children.ToString();
        query["infants"] = party.Infants.ToString();
        if (criteria.TripTypeText == "Khứ hồi")
            query["returnDate"] = criteria.SelectingReturn && date.HasValue ? FlightMapping.DisplayDate(date.Value) : criteria.ReturnDateText;
        if (criteria.SelectedOutboundId.HasValue) query["outboundId"] = criteria.SelectedOutboundId.Value.ToString();
        if (!clearFilters)
        {
            if (model.Filters.Airlines.Count > 0) query["airlines"] = new StringValues(model.Filters.Airlines.ToArray());
            if (model.Filters.Times.Count > 0) query["times"] = new StringValues(model.Filters.Times.ToArray());
            if (model.Filters.MinPriceText.Length > 0) query["minPrice"] = model.Filters.MinPriceText;
            if (model.Filters.MaxPriceText.Length > 0) query["maxPrice"] = model.Filters.MaxPriceText;
        }
        return query;
    }
}
