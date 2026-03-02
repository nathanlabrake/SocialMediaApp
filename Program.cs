using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SocialMediaApp.Data;
using SocialMediaApp.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite("Data Source=circlehub.db"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    Seed(db);
}

app.MapPost("/api/auth/register", async (AppDbContext db, RegisterRequest req) =>
{
    var email = req.Email.Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Name, email, and password are required." });

    var exists = await db.Users.AnyAsync(u => u.Email == email);
    if (exists) return Results.BadRequest(new { error = "Email already registered." });

    var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    var hash = HashPassword(req.Password, salt);

    var user = new User { Name = req.Name.Trim(), Email = email, PasswordSalt = salt, PasswordHash = hash };
    db.Users.Add(user);
    await db.SaveChangesAsync();

    var token = await CreateSession(db, user.Id);
    return Results.Ok(new { token, user = new { user.Id, user.Name, user.Email } });
});

app.MapPost("/api/auth/login", async (AppDbContext db, LoginRequest req) =>
{
    var email = req.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null) return Results.Unauthorized();

    var hash = HashPassword(req.Password, user.PasswordSalt);
    if (hash != user.PasswordHash) return Results.Unauthorized();

    var token = await CreateSession(db, user.Id);
    return Results.Ok(new { token, user = new { user.Id, user.Name, user.Email } });
});

app.MapGet("/api/me", async (HttpContext ctx, AppDbContext db) =>
{
    var user = await GetCurrentUser(ctx, db);
    if (user is null) return Results.Unauthorized();
    return Results.Ok(new { user.Id, user.Name, user.Email });
});

app.MapGet("/api/users", async (HttpContext ctx, AppDbContext db, string? q) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var query = db.Users.Where(u => u.Id != current.Id);
    if (!string.IsNullOrWhiteSpace(q))
    {
        var term = q.Trim().ToLower();
        query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
    }

    var users = await query.OrderBy(u => u.Name).Take(20).Select(u => new { u.Id, u.Name, u.Email }).ToListAsync();
    return Results.Ok(users);
});

app.MapGet("/api/connections", async (HttpContext ctx, AppDbContext db) =>
{
    var user = await GetCurrentUser(ctx, db);
    if (user is null) return Results.Unauthorized();

    var pendingReceived = await db.Connections
        .Include(c => c.Requester)
        .Where(c => c.AddresseeId == user.Id && c.Status == "Pending")
        .Select(c => new { c.Id, fromUserId = c.RequesterId, fromName = c.Requester!.Name })
        .ToListAsync();

    var accepted = await db.Connections
        .Include(c => c.Requester)
        .Include(c => c.Addressee)
        .Where(c => c.Status == "Accepted" && (c.RequesterId == user.Id || c.AddresseeId == user.Id))
        .Select(c => new
        {
            userId = c.RequesterId == user.Id ? c.AddresseeId : c.RequesterId,
            name = c.RequesterId == user.Id ? c.Addressee!.Name : c.Requester!.Name
        }).ToListAsync();

    return Results.Ok(new { pendingReceived, accepted });
});

app.MapPost("/api/connections/request/{userId:int}", async (HttpContext ctx, AppDbContext db, int userId) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();
    if (current.Id == userId) return Results.BadRequest(new { error = "Cannot connect to yourself." });

    var targetExists = await db.Users.AnyAsync(u => u.Id == userId);
    if (!targetExists) return Results.NotFound();

    var exists = await db.Connections.AnyAsync(c =>
        (c.RequesterId == current.Id && c.AddresseeId == userId) ||
        (c.RequesterId == userId && c.AddresseeId == current.Id));

    if (!exists)
    {
        db.Connections.Add(new Connection { RequesterId = current.Id, AddresseeId = userId, Status = "Pending" });
        await db.SaveChangesAsync();
    }

    return Results.Ok();
});

app.MapPost("/api/connections/accept/{connectionId:int}", async (HttpContext ctx, AppDbContext db, int connectionId) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var connection = await db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId && c.AddresseeId == current.Id);
    if (connection is null) return Results.NotFound();

    connection.Status = "Accepted";
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/feed", async (HttpContext ctx, AppDbContext db) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var connectionIds = await db.Connections
        .Where(c => c.Status == "Accepted" && (c.RequesterId == current.Id || c.AddresseeId == current.Id))
        .Select(c => c.RequesterId == current.Id ? c.AddresseeId : c.RequesterId)
        .ToListAsync();

    connectionIds.Add(current.Id);

    var posts = await db.Posts
        .Include(p => p.Author)
        .Where(p => connectionIds.Contains(p.AuthorId))
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new { p.Id, p.Content, p.CreatedAt, author = p.Author!.Name, p.AuthorId })
        .ToListAsync();

    return Results.Ok(posts);
});

app.MapPost("/api/posts", async (HttpContext ctx, AppDbContext db, CreatePostRequest req) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(req.Content)) return Results.BadRequest(new { error = "Content required." });

    db.Posts.Add(new Post { AuthorId = current.Id, Content = req.Content.Trim() });
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/messages/{userId:int}", async (HttpContext ctx, AppDbContext db, int userId) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var isConnected = await AreConnected(db, current.Id, userId);
    if (!isConnected) return Results.StatusCode(403);

    var messages = await db.Messages
        .Where(m => (m.SenderId == current.Id && m.RecipientId == userId) || (m.SenderId == userId && m.RecipientId == current.Id))
        .OrderBy(m => m.SentAt)
        .Select(m => new { m.Id, m.SenderId, m.RecipientId, m.Content, m.SentAt })
        .ToListAsync();

    return Results.Ok(messages);
});

app.MapPost("/api/messages/{userId:int}", async (HttpContext ctx, AppDbContext db, int userId, SendMessageRequest req) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var isConnected = await AreConnected(db, current.Id, userId);
    if (!isConnected) return Results.StatusCode(403);

    if (string.IsNullOrWhiteSpace(req.Content)) return Results.BadRequest(new { error = "Content required." });

    db.Messages.Add(new Message
    {
        SenderId = current.Id,
        RecipientId = userId,
        Content = req.Content.Trim(),
        SentAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.Run();

static async Task<bool> AreConnected(AppDbContext db, int a, int b) =>
    await db.Connections.AnyAsync(c => c.Status == "Accepted" &&
        ((c.RequesterId == a && c.AddresseeId == b) || (c.RequesterId == b && c.AddresseeId == a)));

static string HashPassword(string password, string salt)
{
    var bytes = Encoding.UTF8.GetBytes(password + salt);
    return Convert.ToBase64String(SHA256.HashData(bytes));
}

static async Task<string> CreateSession(AppDbContext db, int userId)
{
    var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    db.SessionTokens.Add(new SessionToken
    {
        UserId = userId,
        Token = token,
        CreatedAt = DateTimeOffset.UtcNow,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
    });
    await db.SaveChangesAsync();
    return token;
}

static async Task<User?> GetCurrentUser(HttpContext ctx, AppDbContext db)
{
    var auth = ctx.Request.Headers.Authorization.ToString();
    if (!auth.StartsWith("Bearer ")) return null;
    var token = auth[7..];

    var session = await db.SessionTokens
        .Where(s => s.Token == token && s.ExpiresAt > DateTimeOffset.UtcNow)
        .Select(s => new { s.UserId })
        .FirstOrDefaultAsync();

    if (session is null) return null;
    return await db.Users.FindAsync(session.UserId);
}

static void Seed(AppDbContext db)
{
    if (db.Users.Any()) return;

    var users = new[]
    {
        CreateUser("Alex Morgan", "alex@circlehub.dev", "password123"),
        CreateUser("Sasha Lee", "sasha@circlehub.dev", "password123"),
        CreateUser("Derek Shah", "derek@circlehub.dev", "password123")
    };

    db.Users.AddRange(users);
    db.SaveChanges();

    db.Connections.Add(new Connection { RequesterId = users[0].Id, AddresseeId = users[1].Id, Status = "Accepted" });
    db.Posts.AddRange([
        new Post { AuthorId = users[0].Id, Content = "Welcome to CircleHub. This feed is only visible to your accepted connections." },
        new Post { AuthorId = users[1].Id, Content = "Excited to collaborate with my network this week!" }
    ]);
    db.SaveChanges();
}

static User CreateUser(string name, string email, string password)
{
    var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    return new User
    {
        Name = name,
        Email = email,
        PasswordSalt = salt,
        PasswordHash = HashPassword(password, salt)
    };
}

record RegisterRequest(string Name, string Email, string Password);
record LoginRequest(string Email, string Password);
record CreatePostRequest(string Content);
record SendMessageRequest(string Content);
