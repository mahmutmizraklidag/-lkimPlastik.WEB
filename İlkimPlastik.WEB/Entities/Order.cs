namespace ilkimPlastik.WEB.Entities
{
    public class Order
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string City { get; set; } = null!;
        public string District { get; set; } = null!;
        public string? Details { get; set; }
        public string? PostCode { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsPay { get; set; } = false;
        public string Status { get; set; } = "Pending";

        public decimal TotalPrice { get; set; }

        public decimal DiscountTotal { get; set; }
        public decimal SubTotalBeforeDiscount { get; set; }

        public string? PaymentConversationId { get; set; }
        public string? PaymentRaw { get; set; }
        public string? PaymentId { get; set; }
        public string? AuthCode { get; set; }
        public string? Rrn { get; set; }
        public string? MpiTransactionId { get; set; }

        public List<OrderProduct> OrderProducts { get; set; } = new();
    }
}