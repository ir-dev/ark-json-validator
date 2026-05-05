using Microsoft.EntityFrameworkCore;
using McpServiceHub.Models.Entities;

namespace McpServiceHub.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<McpService> McpServices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique constraint on email
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Configure relationships
        modelBuilder.Entity<McpService>()
            .HasOne(m => m.Owner)
            .WithMany(u => u.McpServices)
            .HasForeignKey(m => m.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}