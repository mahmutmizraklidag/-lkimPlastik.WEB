namespace ilkimPlastik.WEB.Entities
{
    public class Cart
    {
         public int Id { get; set; }
        public int RefId { get; set; } // kullanıcıId veya cookieId

        public List<CartItem> Items { get; set; } = new();
    }
}
