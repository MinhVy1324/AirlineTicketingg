namespace AirlineTicketing.Web.Infrastructure;

/// <summary>Khóa session dùng chung giữa luồng đặt vé và trang tải vé điện tử.</summary>
public static class BookingSession
{
    public const string BookingId = "booking.bookingId";
}
