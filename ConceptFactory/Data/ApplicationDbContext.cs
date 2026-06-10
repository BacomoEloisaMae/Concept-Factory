using Microsoft.EntityFrameworkCore;
using ConceptFactory.Models;

namespace ConceptFactory.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");
                entity.HasKey(e => e.CategoryID);
                entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(100);
            });

            // Product
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(e => e.ProductID);
                entity.Property(e => e.Price).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Status).HasDefaultValue("Active");
                entity.Property(e => e.DateAdded).HasDefaultValueSql("GETDATE()");

                entity.HasOne(p => p.Category)
                      .WithMany(c => c.Products)
                      .HasForeignKey(p => p.CategoryID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Service
            modelBuilder.Entity<Service>(entity =>
            {
                entity.ToTable("Services");
                entity.HasKey(e => e.ServiceID);
                entity.Property(e => e.ServicePrice).HasColumnType("decimal(10,2)");
                entity.Property(e => e.Status).HasDefaultValue("Active");
                entity.Property(e => e.DateAdded).HasDefaultValueSql("GETDATE()");
            });
        }
    }
}
