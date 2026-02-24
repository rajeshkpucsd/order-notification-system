namespace OrderService.Services;

public class OrderResult
{
    public Models.Order Order { get; set; }
    public bool EventPublished { get; set; }
}