using Microsoft.EntityFrameworkCore;
using SocialMediaApp.Models;

namespace SocialMediaApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<SessionToken> SessionTokens => Set<SessionToken>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<SessionToken>().HasIndex(t => t.Token).IsUnique();

        modelBuilder.Entity<Connection>()
            .HasOne(c => c.Requester)
            .WithMany()
            .HasForeignKey(c => c.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Connection>()
            .HasOne(c => c.Addressee)
            .WithMany()
            .HasForeignKey(c => c.AddresseeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
