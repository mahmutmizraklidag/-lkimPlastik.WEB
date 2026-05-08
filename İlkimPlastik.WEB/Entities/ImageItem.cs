namespace ilkimPlastik.WEB.Entities
{
    public class ImageItem
    {
        public int Id { get; set; }
        public string Filename { get; set; }
        public int DisplayOrder { get; set; } = 0;

        public List<Product> Products { get; set; } = new List<Product>();
    }
}
