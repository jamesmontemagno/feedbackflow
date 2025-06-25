namespace SharedDump.Models.HackerNews;

public class HackerNewsItem
{
    public int? MainStoryId { get; set; }
    public required int Id { get; set; }
    public bool? Deleted { get; set; }
    public string? Type { get; set; }
    public string? By { get; set; }
    public long Time { get; set; }
    public string? Text { get; set; }
    public bool? Dead { get; set; }
    public int? Parent { get; set; }
    public int? Poll { get; set; }
    public List<int> Kids { get; set; } = [];
    public string? Url { get; set; }
    public int? Score { get; set; }
    public string? Title { get; set; }
    public List<int> Parts { get; set; } = [];
    public int? Descendants { get; set; }
}

public class HackerNewsItemBasicInfo
{
    public required int Id { get; set; }
    public required string Title { get; set; }
    public required string By { get; set; }
    public required long Time { get; set; }
    public string? Url { get; set; }
    public int Score { get; set; }
    public int Descendants { get; set; }
}

public class HackerNewsStory
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }
    public int Score { get; set; }
    public string By { get; set; } = string.Empty;
    public long Time { get; set; }
    public int Descendants { get; set; }
    public string Type { get; set; } = "story";
    public int[]? Kids { get; set; }
}

public class HackerNewsComment
{
    public int Id { get; set; }
    public string By { get; set; } = string.Empty;
    public int Parent { get; set; }
    public string Text { get; set; } = string.Empty;
    public long Time { get; set; }
    public string Type { get; set; } = "comment";
    public int[]? Kids { get; set; }
}