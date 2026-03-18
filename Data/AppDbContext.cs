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
        modelBuilder.Entity<Post>().HasIndex(p => new { p.AuthorId, p.CreatedAt });
        modelBuilder.Entity<Message>().HasIndex(m => new { m.SenderId, m.RecipientId, m.SentAt });
        modelBuilder.Entity<Connection>().HasIndex(c => new { c.RequesterId, c.AddresseeId, c.Status });

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

        modelBuilder.Entity<Post>()
            .HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.Recipient)
            .WithMany()
            .HasForeignKey(m => m.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
