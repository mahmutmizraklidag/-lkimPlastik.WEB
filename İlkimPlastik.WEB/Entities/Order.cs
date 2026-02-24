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

        // basit durum alanı
        public bool IsPay { get; set; } = false; // Pending / Paid / Shipped / Cancelled
        public string Status { get; set; } = "Pending"; // Pending / Paid / Shipped / Cancelled

        public decimal TotalPrice { get; set; }

        public List<OrderProduct> OrderProducts { get; set; } = new();

    }
}
