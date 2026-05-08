namespace ilkimPlastik.WEB.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Phone { get; set; }
        public bool IsAdmin { get; set; } = false;   // ✅ EKLE
        public string? NotificationToken { get; set; }

        // ✅ YENİ ALANLAR
        public bool IsCorporate { get; set; } = false;

        public string? CompanyName { get; set; }
        public string? TaxNumber { get; set; }
        public string? TaxOfficeName { get; set; }

        public List<Address> Addresses { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
    }
}
