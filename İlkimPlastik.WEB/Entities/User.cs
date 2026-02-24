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


        public List<Address> Addresses { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
    }
}
