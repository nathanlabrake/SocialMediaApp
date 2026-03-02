namespace SocialMediaApp.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "Alex Morgan";
    public string Headline { get; set; } = "Product designer • Austin";
    public int ConnectionCount { get; set; } = 128;
}

public class Suggestion
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool Connected { get; set; }
}

public class Community
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class EventItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Trend
{
    public int Id { get; set; }
    public string Tag { get; set; } = string.Empty;
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Mood { get; set; } = "📢 Sharing";
    public int Likes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Comment> Comments { get; set; } = [];
}

public class Comment
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public Post? Post { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Message
{
    public int Id { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
