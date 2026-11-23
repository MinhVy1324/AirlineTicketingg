namespace AirlineTicketing.Domain;

public sealed class EmailDeliveryJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? BookingId { get; set; }
    public string? ProtectedMessage { get; set; }
    public string Actor { get; set; } = "system";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool Delivered { get; set; }
    public string? LastError { get; set; }
}
