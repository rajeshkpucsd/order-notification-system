namespace OrderService.Events;

public class OrderEmailSentEvent
{
    public Guid EventId { get; set; }
    public Guid OrderId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
