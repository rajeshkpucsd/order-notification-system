namespace NotificationService.Models;

public class Notification
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }   // for idempotency
    public Guid OrderId { get; set; }
    public string Email { get; set; }
    public string Type { get; set; } = "ORDER_CREATED";
    public bool Delivered { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}