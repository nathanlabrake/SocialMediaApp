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

app.MapGet("/api", () => Results.Ok(new
{
    name = "CircleHub API",
    endpoints = new[]
    {
        "POST /api/auth/register",
        "POST /api/auth/login",
        "POST /api/auth/logout",
        "GET /api/me",
        "GET /api/bootstrap",
        "GET /api/users?q=...",
        "POST /api/connections/request/{userId}",
        "POST /api/connections/{connectionId}/accept",
        "POST /api/connections/{connectionId}/decline",
        "GET /api/feed?q=...",
        "POST /api/posts",
        "GET /api/messages/{userId}",
        "POST /api/messages/{userId}"
    }
}));

app.MapPost("/api/auth/register", async (AppDbContext db, RegisterRequest req) =>
{
    var email = NormalizeEmail(req.Email);
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Name, email, and password are required." });

    if (await db.Users.AnyAsync(u => u.Email == email))
        return Results.BadRequest(new { error = "Email already registered." });

    var user = CreateUser(req.Name.Trim(), email, req.Password, req.Headline?.Trim());
    db.Users.Add(user);
    await db.SaveChangesAsync();

    var token = await CreateSession(db, user.Id);
    return Results.Ok(new { token, user = ShapeUser(user) });
});

app.MapPost("/api/auth/login", async (AppDbContext db, LoginRequest req) =>
{
    var email = NormalizeEmail(req.Email);
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null || HashPassword(req.Password, user.PasswordSalt) != user.PasswordHash)
        return Results.Unauthorized();

    var token = await CreateSession(db, user.Id);
    return Results.Ok(new { token, user = ShapeUser(user) });
});

app.MapPost("/api/auth/logout", async (HttpContext ctx, AppDbContext db) =>
{
    var token = GetBearerToken(ctx);
    if (string.IsNullOrWhiteSpace(token)) return Results.NoContent();

    var session = await db.SessionTokens.FirstOrDefaultAsync(s => s.Token == token);
    if (session is null) return Results.NoContent();

    db.SessionTokens.Remove(session);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/me", async (HttpContext ctx, AppDbContext db) =>
{
    var user = await GetCurrentUser(ctx, db);
    return user is null ? Results.Unauthorized() : Results.Ok(ShapeUser(user));
});

app.MapGet("/api/bootstrap", async (HttpContext ctx, AppDbContext db) =>
{
    var user = await GetCurrentUser(ctx, db);
    if (user is null) return Results.Unauthorized();

    var payload = await BuildBootstrap(db, user.Id, search: null);
    return Results.Ok(payload);
});

app.MapGet("/api/users", async (HttpContext ctx, AppDbContext db, string? q) =>
{
    var user = await GetCurrentUser(ctx, db);
    if (user is null) return Results.Unauthorized();

    var bootstrap = await BuildBootstrap(db, user.Id, q);
    return Results.Ok(bootstrap.discoverPeople);
});

app.MapPost("/api/connections/request/{userId:int}", async (HttpContext ctx, AppDbContext db, int userId) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();
    if (current.Id == userId) return Results.BadRequest(new { error = "You cannot connect to yourself." });
    if (!await db.Users.AnyAsync(u => u.Id == userId)) return Results.NotFound();

    var connection = await FindConnection(db, current.Id, userId);
    if (connection is null)
    {
        db.Connections.Add(new Connection
        {
            RequesterId = current.Id,
            AddresseeId = userId,
            Status = ConnectionStatuses.Pending
        });
        await db.SaveChangesAsync();
    }

    return Results.Ok();
});

app.MapPost("/api/connections/{connectionId:int}/accept", async (HttpContext ctx, AppDbContext db, int connectionId) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var connection = await db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId && c.AddresseeId == current.Id);
    if (connection is null) return Results.NotFound();

    connection.Status = ConnectionStatuses.Accepted;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/api/connections/{connectionId:int}/decline", async (HttpContext ctx, AppDbContext db, int connectionId) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var connection = await db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId && c.AddresseeId == current.Id);
    if (connection is null) return Results.NotFound();

    connection.Status = ConnectionStatuses.Declined;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapGet("/api/feed", async (HttpContext ctx, AppDbContext db, string? q) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();

    var visibleIds = await GetVisibleAuthorIds(db, current.Id);
    var query = db.Posts.Include(p => p.Author).Where(p => visibleIds.Contains(p.AuthorId));

    if (!string.IsNullOrWhiteSpace(q))
    {
        var term = q.Trim().ToLower();
        query = query.Where(p => p.Content.ToLower().Contains(term) || p.Author!.Name.ToLower().Contains(term));
    }

    var posts = await query
        .OrderByDescending(p => p.CreatedAt)
        .Select(p => new
        {
            p.Id,
            p.Content,
            p.CreatedAt,
            author = new { p.AuthorId, name = p.Author!.Name, headline = p.Author!.Headline },
            visibility = "connections"
        })
        .ToListAsync();

    return Results.Ok(posts);
});

app.MapPost("/api/posts", async (HttpContext ctx, AppDbContext db, CreatePostRequest req) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(req.Content)) return Results.BadRequest(new { error = "Post content is required." });

    var post = new Post { AuthorId = current.Id, Content = req.Content.Trim(), CreatedAt = DateTimeOffset.UtcNow };
    db.Posts.Add(post);
    await db.SaveChangesAsync();

    return Results.Ok(new { post.Id });
});

app.MapGet("/api/messages/{userId:int}", async (HttpContext ctx, AppDbContext db, int userId) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();
    if (!await AreConnected(db, current.Id, userId)) return Results.StatusCode(StatusCodes.Status403Forbidden);

    var otherUser = await db.Users.FindAsync(userId);
    if (otherUser is null) return Results.NotFound();

    var messages = await db.Messages
        .Where(m => (m.SenderId == current.Id && m.RecipientId == userId) || (m.SenderId == userId && m.RecipientId == current.Id))
        .OrderBy(m => m.SentAt)
        .Select(m => new
        {
            m.Id,
            m.SenderId,
            m.RecipientId,
            m.Content,
            m.SentAt,
            direction = m.SenderId == current.Id ? "outgoing" : "incoming"
        })
        .ToListAsync();

    return Results.Ok(new { conversationWith = ShapeUser(otherUser), messages });
});

app.MapPost("/api/messages/{userId:int}", async (HttpContext ctx, AppDbContext db, int userId, SendMessageRequest req) =>
{
    var current = await GetCurrentUser(ctx, db);
    if (current is null) return Results.Unauthorized();
    if (!await AreConnected(db, current.Id, userId)) return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (string.IsNullOrWhiteSpace(req.Content)) return Results.BadRequest(new { error = "Message content is required." });

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

static async Task<object> BuildBootstrap(AppDbContext db, int currentUserId, string? search)
{
    var me = await db.Users.FindAsync(currentUserId);
    var discoverPeople = await GetDiscoverPeople(db, currentUserId, search);
    var pendingRequests = await db.Connections
        .Include(c => c.Requester)
        .Where(c => c.AddresseeId == currentUserId && c.Status == ConnectionStatuses.Pending)
        .OrderByDescending(c => c.CreatedAt)
        .Select(c => new
        {
            c.Id,
            requester = new { c.RequesterId, name = c.Requester!.Name, headline = c.Requester!.Headline },
            status = c.Status
        })
        .ToListAsync();

    var connections = await db.Connections
        .Include(c => c.Requester)
        .Include(c => c.Addressee)
        .Where(c => c.Status == ConnectionStatuses.Accepted && (c.RequesterId == currentUserId || c.AddresseeId == currentUserId))
        .OrderByDescending(c => c.CreatedAt)
        .Select(c => c.RequesterId == currentUserId
            ? new { id = c.AddresseeId, name = c.Addressee!.Name, headline = c.Addressee!.Headline }
            : new { id = c.RequesterId, name = c.Requester!.Name, headline = c.Requester!.Headline })
        .ToListAsync();

    return new
    {
        me = me is null ? null : ShapeUser(me),
        pendingRequests,
        connections,
        discoverPeople
    };
}

static async Task<List<object>> GetDiscoverPeople(AppDbContext db, int currentUserId, string? search)
{
    var query = db.Users.Where(u => u.Id != currentUserId);

    if (!string.IsNullOrWhiteSpace(search))
    {
        var term = search.Trim().ToLower();
        query = query.Where(u => u.Name.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
    }

    var users = await query.OrderBy(u => u.Name).Take(30).ToListAsync();
    var people = new List<object>();

    foreach (var user in users)
    {
        var connection = await FindConnection(db, currentUserId, user.Id);
        var relationship = connection?.Status switch
        {
            ConnectionStatuses.Accepted => "Connected",
            ConnectionStatuses.Pending when connection.RequesterId == currentUserId => "Request sent",
            ConnectionStatuses.Pending => "Respond to request",
            ConnectionStatuses.Declined => "Not connected",
            _ => "Not connected"
        };

        people.Add(new
        {
            id = user.Id,
            name = user.Name,
            email = user.Email,
            headline = user.Headline,
            relationship,
            connectionId = connection?.Id,
            incomingRequest = connection is not null && connection.RequesterId == user.Id && connection.Status == ConnectionStatuses.Pending
        });
    }

    return people;
}

static async Task<Connection?> FindConnection(AppDbContext db, int userA, int userB) =>
    await db.Connections.FirstOrDefaultAsync(c =>
        (c.RequesterId == userA && c.AddresseeId == userB) ||
        (c.RequesterId == userB && c.AddresseeId == userA));

static async Task<List<int>> GetVisibleAuthorIds(AppDbContext db, int currentUserId)
{
    var visibleIds = await db.Connections
        .Where(c => c.Status == ConnectionStatuses.Accepted && (c.RequesterId == currentUserId || c.AddresseeId == currentUserId))
        .Select(c => c.RequesterId == currentUserId ? c.AddresseeId : c.RequesterId)
        .ToListAsync();

    visibleIds.Add(currentUserId);
    return visibleIds;
}

static async Task<bool> AreConnected(AppDbContext db, int a, int b) =>
    await db.Connections.AnyAsync(c =>
        c.Status == ConnectionStatuses.Accepted &&
        ((c.RequesterId == a && c.AddresseeId == b) || (c.RequesterId == b && c.AddresseeId == a)));

static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

static string? GetBearerToken(HttpContext ctx)
{
    var auth = ctx.Request.Headers.Authorization.ToString();
    return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..] : null;
}

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
    var token = GetBearerToken(ctx);
    if (string.IsNullOrWhiteSpace(token)) return null;

    var session = await db.SessionTokens.FirstOrDefaultAsync(s => s.Token == token && s.ExpiresAt > DateTimeOffset.UtcNow);
    if (session is null) return null;

    return await db.Users.FindAsync(session.UserId);
}

static object ShapeUser(User user) => new { user.Id, user.Name, user.Email, user.Headline, user.CreatedAt };

static void Seed(AppDbContext db)
{
    if (db.Users.Any()) return;

    var alex = CreateUser("Alex Morgan", "alex@circlehub.dev", "password123", "Designer building cozy digital communities");
    var sasha = CreateUser("Sasha Lee", "sasha@circlehub.dev", "password123", "Frontend engineer sharing UI experiments");
    var derek = CreateUser("Derek Shah", "derek@circlehub.dev", "password123", "Growth strategist for creator teams");
    var priya = CreateUser("Priya N.", "priya@circlehub.dev", "password123", "Community operator focused on belonging");

    db.Users.AddRange(alex, sasha, derek, priya);
    db.SaveChanges();

    db.Connections.AddRange(
        new Connection { RequesterId = alex.Id, AddresseeId = sasha.Id, Status = ConnectionStatuses.Accepted },
        new Connection { RequesterId = priya.Id, AddresseeId = alex.Id, Status = ConnectionStatuses.Pending }
    );

    db.Posts.AddRange(
        new Post { AuthorId = alex.Id, Content = "Welcome to CircleHub. Everything you post here is visible only to you and your accepted connections." },
        new Post { AuthorId = sasha.Id, Content = "Spent the morning tightening up a motion prototype for our onboarding flow." },
        new Post { AuthorId = derek.Id, Content = "Testing some creator growth loops today." }
    );

    db.Messages.Add(new Message
    {
        SenderId = sasha.Id,
        RecipientId = alex.Id,
        Content = "Want to review the landing page after lunch?",
        SentAt = DateTimeOffset.UtcNow.AddMinutes(-20)
    });

    db.SaveChanges();
}

static User CreateUser(string name, string email, string password, string? headline = null)
{
    var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    return new User
    {
        Name = name,
        Email = NormalizeEmail(email),
        Headline = string.IsNullOrWhiteSpace(headline) ? "Building thoughtful communities" : headline,
        PasswordSalt = salt,
        PasswordHash = HashPassword(password, salt)
    };
}

record RegisterRequest(string Name, string Email, string Password, string? Headline);
record LoginRequest(string Email, string Password);
record CreatePostRequest(string Content);
record SendMessageRequest(string Content);
