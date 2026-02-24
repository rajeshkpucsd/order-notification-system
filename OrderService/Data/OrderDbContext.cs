using Microsoft.EntityFrameworkCore;
using OrderService.Models;

namespace OrderService.Data;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.CustomerEmail)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(o => o.ProductCode)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(o => o.Quantity)
                .IsRequired();
        });
    }
}