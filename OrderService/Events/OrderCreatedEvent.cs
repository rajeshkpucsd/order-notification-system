namespace OrderService.Events;

public class OrderCreatedEvent
{
    public Guid EventId { get; set; }
    public Guid OrderId { get; set; }
    public string Email { get; set; }
    public string ProductCode { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}