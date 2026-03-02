using Microsoft.EntityFrameworkCore;
using SocialMediaApp.Models;

namespace SocialMediaApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<Suggestion> Suggestions => Set<Suggestion>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<EventItem> Events => Set<EventItem>();
    public DbSet<Trend> Trends => Set<Trend>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Message> Messages => Set<Message>();
}
