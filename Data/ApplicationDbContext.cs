using Microsoft.EntityFrameworkCore;
using ConceptFactory.Models;

namespace ConceptFactory.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // ── Tables ──────────────────────────────────────────────────
        public DbSet<Category>       Categories       { get; set; }
        public DbSet<Product>        Products         { get; set; }
        public DbSet<Service>        Services         { get; set; }
        public DbSet<ProductService> ProductServices  { get; set; }
        public DbSet<ProductColorImage> ProductColorImages { get; set; }
        public DbSet<Order>          Orders           { get; set; }
        public DbSet<OrderDetail>    OrderDetails     { get; set; }
        public DbSet<AdminUser>      AdminUsers       { get; set; }
        public DbSet<ActivityLog>    ActivityLogs     { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Category ────────────────────────────────────────────
            modelBuilder.Entity<Category>(e =>
            {
                e.ToTable("Categories");
                e.HasKey(x => x.CategoryID);
                e.Property(x => x.CategoryName).IsRequired().HasMaxLength(100);
                e.HasIndex(x => x.CategoryName).IsUnique();

                e.HasOne(c => c.ParentCategory)
                 .WithMany(c => c.ChildCategories)
                 .HasForeignKey(c => c.ParentCategoryID)
                 .OnDelete(DeleteBehavior.Restrict); // never cascade-delete a whole category tree by accident
            });

            // ── Product ─────────────────────────────────────────────
            modelBuilder.Entity<Product>(e =>
            {
                e.ToTable("Products");
                e.HasKey(x => x.ProductID);
                e.Property(x => x.Price).HasColumnType("decimal(10,2)");
                e.Property(x => x.Status).HasDefaultValue("Active");
                e.Property(x => x.DateAdded).HasDefaultValueSql("GETDATE()");
                e.Property(x => x.IsDeleted).HasDefaultValue(false);

                e.HasOne(p => p.Category)
                 .WithMany(c => c.Products)
                 .HasForeignKey(p => p.CategoryID)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── ProductColorImages ───────────────────────────────────
            modelBuilder.Entity<ProductColorImage>(e =>
            {
                e.ToTable("ProductColorImages");
                e.HasKey(x => x.ProductColorImageID);
                e.Property(x => x.ColorHex).IsRequired().HasMaxLength(20);
                e.Property(x => x.DisplayOrder).HasDefaultValue(0);

                e.HasOne(pci => pci.Product)
                 .WithMany(p => p.ColorImages)
                 .HasForeignKey(pci => pci.ProductID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Service ─────────────────────────────────────────────
            modelBuilder.Entity<Service>(e =>
            {
                e.ToTable("Services");
                e.HasKey(x => x.ServiceID);
                e.Property(x => x.ServicePrice).HasColumnType("decimal(10,2)");
                e.Property(x => x.Status).HasDefaultValue("Active");
                e.Property(x => x.DateAdded).HasDefaultValueSql("GETDATE()");
                e.Property(x => x.IsDeleted).HasDefaultValue(false);
            });

            // ── ProductServices (junction) ───────────────────────────
            modelBuilder.Entity<ProductService>(e =>
            {
                e.ToTable("ProductServices");
                e.HasKey(x => x.ProductServiceID);

                e.HasIndex(x => new { x.ProductID, x.ServiceID }).IsUnique();

                e.HasOne(ps => ps.Product)
                 .WithMany(p => p.ProductServices)
                 .HasForeignKey(ps => ps.ProductID)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(ps => ps.Service)
                 .WithMany(s => s.ProductServices)
                 .HasForeignKey(ps => ps.ServiceID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Order ────────────────────────────────────────────────
            modelBuilder.Entity<Order>(e =>
            {
                e.ToTable("Orders");
                e.HasKey(x => x.OrderID);
                e.Property(x => x.TotalAmount).HasColumnType("decimal(10,2)");
                e.Property(x => x.OrderDate).HasDefaultValueSql("GETDATE()");
                e.Property(x => x.Status).HasDefaultValue("Pending");
                e.Property(x => x.PaymentStatus).HasDefaultValue("Waiting for Verification");
                e.Property(x => x.ProductionStage).HasDefaultValue(0);
                e.Property(x => x.DownPaymentAmount).HasColumnType("decimal(10,2)");
                e.Property(x => x.RemainingBalance).HasColumnType("decimal(10,2)");
            });

            // ── OrderDetail ──────────────────────────────────────────
            modelBuilder.Entity<OrderDetail>(e =>
            {
                e.ToTable("OrderDetails");
                e.HasKey(x => x.OrderDetailID);
                e.Property(x => x.UnitPrice).HasColumnType("decimal(10,2)");

                e.HasOne(od => od.Order)
                 .WithMany(o => o.OrderDetails)
                 .HasForeignKey(od => od.OrderID)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(od => od.Product)
                 .WithMany(p => p.OrderDetails)
                 .HasForeignKey(od => od.ProductID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(od => od.Service)
                 .WithMany(s => s.OrderDetails)
                 .HasForeignKey(od => od.ServiceID)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(od => od.Category)
                 .WithMany()
                 .HasForeignKey(od => od.CategoryID)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── AdminUser ────────────────────────────────────────────
            modelBuilder.Entity<AdminUser>(e =>
            {
                e.ToTable("AdminUsers");
                e.HasKey(x => x.AdminID);
            });

            // ── ActivityLog ──────────────────────────────────────────
            modelBuilder.Entity<ActivityLog>(e =>
            {
                e.ToTable("ActivityLogs");
                e.HasKey(x => x.LogID);
                e.Property(x => x.Timestamp).HasDefaultValueSql("GETDATE()");
            });
        }
    }
}
