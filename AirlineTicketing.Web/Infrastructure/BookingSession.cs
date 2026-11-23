namespace AirlineTicketing.Web.Infrastructure;

/// <summary>Khóa session dùng chung giữa luồng đặt vé và trang tải vé điện tử.</summary>
public static class BookingSession
{
    public const string BookingId = "booking.bookingId";
    public const string FlightId = "booking.flightId";
    public const string Passenger = "booking.passenger";
    public const string BaggageCode = "booking.baggageCode";
    public const string Seat = "booking.seat";
    public const string Journey = "booking.journey";
    public const string JourneyForm = "booking.journeyForm";
}
