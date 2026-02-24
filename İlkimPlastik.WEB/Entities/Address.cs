namespace ilkimPlastik.WEB.Entities
{
    public class Address
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string City { get; set; } = null!;
        public string District { get; set; } = null!;
        public string? Details { get; set; }
        public string? PostCode { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }
    }
}
