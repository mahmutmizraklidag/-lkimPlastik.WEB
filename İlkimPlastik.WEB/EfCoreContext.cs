using ilkimPlastik.WEB.Entities;
using Microsoft.EntityFrameworkCore;

namespace ilkimPlastik.WEB
{
    public class EfCoreContext:DbContext
    {
        public EfCoreContext(DbContextOptions<EfCoreContext> options) : base(options)
        {
        }


        public DbSet<User> Users { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<SubCategory> SubCategories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductFeature> ProductFeatures { get; set; }
        public DbSet<ProductSize> ProductSizes { get; set; }
        public DbSet<ProductDevice> ProductDevices { get; set; }
        public DbSet<ImageItem> ImageItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderProduct> OrderProducts { get; set; }
        public DbSet<SiteSettings> SiteSettings { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<Slider> Sliders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Para alanları (uyarıları kapatır + truncation riskini bitirir)
            modelBuilder.Entity<Product>()
                .Property(x => x.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderProduct>()
                .Property(x => x.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(x => x.TotalPrice)
                .HasPrecision(18, 2);

            // Eğer başka decimal alanların varsa aynı şekilde ekle:
            // modelBuilder.Entity<...>().Property(x => x....).HasPrecision(18, 2);
        }
    }
}
