namespace OrderService.Models
{
    public class Order
    {
        public Guid Id { get; set; }

        public string CustomerEmail { get; set; } = string.Empty;

        public string ProductCode { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public string Status { get; set; } = "CREATED";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
