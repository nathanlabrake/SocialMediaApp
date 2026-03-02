using Microsoft.EntityFrameworkCore;
using SocialMediaApp.Data;
using SocialMediaApp.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=circlehub.db"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    SeedDatabase(db);
}

app.MapGet("/api/bootstrap", async (AppDbContext db) =>
{
    var profile = await db.Profiles.FirstAsync();
    var suggestions = await db.Suggestions.OrderBy(s => s.Id).ToListAsync();
    var communities = await db.Communities.OrderBy(c => c.Id).Select(c => c.Name).ToListAsync();
    var eventsList = await db.Events.OrderBy(e => e.Id).Select(e => e.Name).ToListAsync();
    var trends = await db.Trends.OrderBy(t => t.Id).Select(t => t.Tag).ToListAsync();
    var messages = await db.Messages.OrderByDescending(m => m.SentAt).Take(20)
        .Select(m => new { id = m.Id, to = m.Recipient, text = m.Content, sentAt = m.SentAt }).ToListAsync();

    return Results.Ok(new
    {
        profile,
        suggestions,
        communities,
        events = eventsList,
        trends,
        messages
    });
});

app.MapGet("/api/posts", async (AppDbContext db, string? q) =>
{
    var query = db.Posts.Include(p => p.Comments).AsQueryable();

    if (!string.IsNullOrWhiteSpace(q))
    {
        query = query.Where(p =>
            EF.Functions.Like(p.Title, $"%{q}%") ||
            EF.Functions.Like(p.Content, $"%{q}%") ||
            EF.Functions.Like(p.Mood, $"%{q}%"));
    }

    var posts = await query
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new
        {
            id = p.Id,
            title = p.Title,
            content = p.Content,
            mood = p.Mood,
            likes = p.Likes,
            time = p.CreatedAt,
            comments = p.Comments.OrderByDescending(c => c.CreatedAt).Select(c => c.Content).ToList()
        })
        .ToListAsync();

    return Results.Ok(posts);
});

app.MapPost("/api/posts", async (AppDbContext db, CreatePostRequest request) =>
{
    var post = new Post
    {
        Title = request.Title.Trim(),
        Content = request.Content.Trim(),
        Mood = request.Mood.Trim(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.Posts.Add(post);
    await db.SaveChangesAsync();

    return Results.Created($"/api/posts/{post.Id}", new { id = post.Id });
});

app.MapPost("/api/posts/{id:int}/like", async (AppDbContext db, int id) =>
{
    var post = await db.Posts.FindAsync(id);
    if (post is null) return Results.NotFound();

    post.Likes += 1;
    await db.SaveChangesAsync();

    return Results.Ok(new { likes = post.Likes });
});

app.MapPost("/api/posts/{id:int}/comments", async (AppDbContext db, int id, CreateCommentRequest request) =>
{
    var postExists = await db.Posts.AnyAsync(p => p.Id == id);
    if (!postExists) return Results.NotFound();

    var comment = new Comment
    {
        PostId = id,
        Content = request.Content.Trim(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.Comments.Add(comment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/posts/{id}/comments/{comment.Id}", new { id = comment.Id });
});

app.MapPost("/api/suggestions/{id:int}/connect", async (AppDbContext db, int id) =>
{
    var suggestion = await db.Suggestions.FindAsync(id);
    var profile = await db.Profiles.FirstAsync();

    if (suggestion is null) return Results.NotFound();
    if (!suggestion.Connected)
    {
        suggestion.Connected = true;
        profile.ConnectionCount += 1;
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { connected = suggestion.Connected, connectionCount = profile.ConnectionCount });
});

app.MapPost("/api/messages", async (AppDbContext db, CreateMessageRequest request) =>
{
    var message = new Message
    {
        Recipient = request.To.Trim(),
        Content = request.Text.Trim(),
        SentAt = DateTimeOffset.UtcNow
    };

    db.Messages.Add(message);
    await db.SaveChangesAsync();

    return Results.Created($"/api/messages/{message.Id}", new { id = message.Id });
});

app.Run();

static void SeedDatabase(AppDbContext db)
{
    if (!db.Profiles.Any())
    {
        db.Profiles.Add(new UserProfile());
    }

    if (!db.Suggestions.Any())
    {
        db.Suggestions.AddRange([
            new Suggestion { Name = "Sasha Lee", Role = "Frontend engineer" },
            new Suggestion { Name = "Derek Shah", Role = "Growth strategist" },
            new Suggestion { Name = "Priya N.", Role = "Community manager" }
        ]);
    }

    if (!db.Communities.Any())
    {
        db.Communities.AddRange([
            new Community { Name = "Design Critique Club" },
            new Community { Name = "Remote Builders" },
            new Community { Name = "Sustainable Living" }
        ]);
    }

    if (!db.Events.Any())
    {
        db.Events.AddRange([
            new EventItem { Name = "Creator Meetup - Fri" },
            new EventItem { Name = "Product Workshop - Tue" },
            new EventItem { Name = "AI Ethics Panel - Sat" }
        ]);
    }

    if (!db.Trends.Any())
    {
        db.Trends.AddRange([
            new Trend { Tag = "#BuildInPublic" },
            new Trend { Tag = "#RemoteWork" },
            new Trend { Tag = "#ClimateTech" },
            new Trend { Tag = "#CreatorEconomy" }
        ]);
    }

    if (!db.Posts.Any())
    {
        db.Posts.Add(new Post
        {
            Title = "Launching my side project",
            Content = "After 3 months of work, I finally released an MVP. I'd love feedback from builders.",
            Mood = "🎉 Excited",
            Likes = 3,
            CreatedAt = DateTimeOffset.UtcNow,
            Comments =
            [
                new Comment { Content = "Big milestone, congrats!", CreatedAt = DateTimeOffset.UtcNow },
                new Comment { Content = "Share the link 👀", CreatedAt = DateTimeOffset.UtcNow }
            ]
        });
    }

    db.SaveChanges();
}

record CreatePostRequest(string Title, string Content, string Mood);
record CreateCommentRequest(string Content);
record CreateMessageRequest(string To, string Text);
