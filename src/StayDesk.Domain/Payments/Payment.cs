namespace StayDesk.Domain.Payments;

public enum PaymentStatus
{
    Succeeded,
    Refunded,
}

public enum PaymentProvider
{
    Mock,
    Desk,
}

public class Payment
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid BookingId { get; private init; }
    public decimal Amount { get; private init; }
    public PaymentStatus Status { get; private set; }
    public PaymentProvider Provider { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public Payment(Guid bookingId, decimal amount, PaymentProvider provider, DateTimeOffset createdAt)
    {
        BookingId = bookingId;
        Amount = amount;
        Provider = provider;
        CreatedAt = createdAt;
        Status = PaymentStatus.Succeeded;
    }

    // The refunded amount is on the booking (RefundAmount); it may be less than Amount.
    public void MarkRefunded() => Status = PaymentStatus.Refunded;
}
