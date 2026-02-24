namespace OrderService.DTOs;

public class CreateOrderDto
{
    public string CustomerEmail { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
}