using AirlineTicketing.BUS;
using AirlineTicketing.Domain;

namespace AirlineTicketing.Web.ViewModels;

public sealed record JourneySelection(int FlightId, int? ReturnFlightId, PassengerParty Party, string Cabin)
{
    public bool IsLegacy => ReturnFlightId is null && Party == new PassengerParty() && Cabin == "PhoThong";
    public int LegCount => ReturnFlightId.HasValue ? 2 : 1;
    public IReadOnlyList<int> FlightIds => ReturnFlightId.HasValue ? [FlightId, ReturnFlightId.Value] : [FlightId];
}
public sealed class JourneyPassengerForm
{
    public PassengerFormModel Person { get; set; } = new();
    public List<string> Seats { get; set; } = [];
    public List<string> Baggage { get; set; } = [];
    public int? CompanionIndex { get; set; }
}
public sealed class JourneyBookingForm
{
    public List<JourneyPassengerForm> People { get; set; } = [];
    public string ContactName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
}
public sealed record JourneyLegViewModel(string Label, FlightOffer Flight, IReadOnlyList<SeatRow> SeatRows);
public sealed record JourneyTicketViewModel(string Passenger, string Type, string FlightNumber, string Route,
    string Schedule, string Cabin, string Seat, string Baggage, string TicketNumber, decimal Total);
public sealed class JourneyBookingViewModel
{
    public JourneySelection Selection { get; init; } = null!;
    public JourneyBookingForm Form { get; init; } = new();
    public IReadOnlyList<JourneyLegViewModel> Legs { get; init; } = [];
    public IReadOnlyList<JourneyTicketViewModel> Tickets { get; init; } = [];
    public Dictionary<string, string> Errors { get; set; } = [];
    public PriceSummary Price { get; init; } = new();
    public string Pnr { get; init; } = "";
    public DateTimeOffset? HoldUntil { get; init; }
}
