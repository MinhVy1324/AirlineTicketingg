namespace AirlineTicketing.Web.ViewModels;

public sealed record FlightDisruptionNotice(int Id, string Title, string OldSchedule, string? NewSchedule,
    string? Result, bool CanAccept);

public sealed record FlightDisruptionRow(int Id, string Pnr, string Flight, string Name, string Route,
    bool Cancelled, string OldSchedule, string? NewSchedule, string? Result);

public sealed record DisruptionAlternative(int Id, string Description);

public sealed record FlightDisruptionsViewModel(IReadOnlyList<FlightDisruptionRow> Rows, FlightDisruptionRow? Selected,
    IReadOnlyList<DisruptionAlternative> Alternatives, bool CanRebook, bool CanRefundDemo, string RefundAmount,
    bool History, string? Message, bool Error);
