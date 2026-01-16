using FlowChat.Enums;

namespace FlowChat.Models;

public class Memory
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ulong UserId { get; set; } // Discord user ID
    public List<string> Keywords { get; set; } = new();
    public MemoryType Type { get; set; }
    public int ImportanceScore { get; set; } = 5; // 1-10 scale
    public bool IsPrivate { get; set; } = false;
}