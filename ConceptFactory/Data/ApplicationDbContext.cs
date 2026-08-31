using Microsoft.EntityFrameworkCore;
using ConceptFactory.Models;

namespace ConceptFactory.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // ── Tables ──────────────────────────────────────────────────
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ProductService> ProductServices { get; set; }
        public DbSet<ProductColorImage> ProductColorImages { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<AdminUser> AdminUsers { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<ProductionTracking> ProductionTracking { get; set; }
        public DbSet<ReportLog> ReportLogs { get; set; }

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

            // ── Order → Customer (nullable — see Order.CustomerID) ────
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerID)
                .OnDelete(DeleteBehavior.SetNull);

            // ── AdminUser ────────────────────────────────────────────
            modelBuilder.Entity<AdminUser>(e =>
            {
                e.ToTable("AdminUsers");
                e.HasKey(x => x.AdminID);
            });

            // ── Role (lookup: Admin/Customer/Staff) ───────────────────
            modelBuilder.Entity<Role>(e =>
            {
                e.ToTable("Roles");
                e.HasKey(x => x.RoleID);
            });

            // ── User (currently: Staff accounts only — see User.cs) ───
            modelBuilder.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.UserID);
                e.Property(x => x.Status).HasDefaultValue("Active");
                e.Property(x => x.IsDeleted).HasDefaultValue(false);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");

                e.HasOne(u => u.Role)
                 .WithMany()
                 .HasForeignKey(u => u.RoleID)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ── Customer (storefront register/login) ─────────────────
            modelBuilder.Entity<Customer>(e =>
            {
                e.ToTable("Customers");
                e.HasKey(x => x.CustomerID);
                e.Property(x => x.FailedLoginAttempts).HasDefaultValue(0);
                e.Property(x => x.IsDeleted).HasDefaultValue(false);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
                e.HasIndex(x => x.Email).IsUnique();
            });

            // ── ActivityLog ──────────────────────────────────────────
            modelBuilder.Entity<ActivityLog>(e =>
            {
                e.ToTable("ActivityLogs");
                e.HasKey(x => x.LogID);
                e.Property(x => x.Timestamp).HasDefaultValueSql("GETDATE()");
            });

            // ── Notification ─────────────────────────────────────────
            modelBuilder.Entity<Notification>(e =>
            {
                e.ToTable("Notifications");
                e.HasKey(x => x.NotificationID);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
                e.Property(x => x.IsRead).HasDefaultValue(false);

                e.HasOne(n => n.Order)
                 .WithMany()
                 .HasForeignKey(n => n.OrderID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── Payment (audit trail — see Payment.cs) ────────────────
            modelBuilder.Entity<Payment>(e =>
            {
                e.ToTable("Payments");
                e.HasKey(x => x.PaymentID);
                e.Property(x => x.Amount).HasColumnType("decimal(10,2)");
                e.Property(x => x.CashAmountReceived).HasColumnType("decimal(10,2)");
                e.Property(x => x.PaymentStatus).HasDefaultValue("Waiting for Verification");
                e.Property(x => x.PaymentDate).HasDefaultValueSql("GETDATE()");

                e.HasOne(p => p.Order)
                 .WithMany()
                 .HasForeignKey(p => p.OrderID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── ProductionTracking (stage history — see ProductionTracking.cs) ─
            modelBuilder.Entity<ProductionTracking>(e =>
            {
                e.ToTable("ProductionTracking");
                e.HasKey(x => x.ProductionTrackingID);
                e.Property(x => x.StageStatus).HasDefaultValue("Reached");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETDATE()");

                e.HasOne(pt => pt.Order)
                 .WithMany()
                 .HasForeignKey(pt => pt.OrderID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── ReportLog (accountability trail — see ReportLog.cs) ───
            modelBuilder.Entity<ReportLog>(e =>
            {
                e.ToTable("ReportLogs");
                e.HasKey(x => x.ReportLogID);
                e.Property(x => x.GeneratedAt).HasDefaultValueSql("GETDATE()");
            });

            // ── CartItem / WishlistItem (account-tied storefront data — see
            // Models/CartItem.cs, Models/WishlistItem.cs) ─────────────
            modelBuilder.Entity<CartItem>(e =>
            {
                e.ToTable("CartItems");
                e.HasKey(x => x.CartItemID);
                e.Property(x => x.Price).HasColumnType("decimal(10,2)");
                e.Property(x => x.ServicePrice).HasColumnType("decimal(10,2)");
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
                e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETDATE()");

                e.HasOne(c => c.Customer)
                 .WithMany()
                 .HasForeignKey(c => c.CustomerID)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WishlistItem>(e =>
            {
                e.ToTable("WishlistItems");
                e.HasKey(x => x.WishlistItemID);
                e.Property(x => x.Price).HasColumnType("decimal(10,2)");
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");

                e.HasOne(w => w.Customer)
                 .WithMany()
                 .HasForeignKey(w => w.CustomerID)
                 .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}