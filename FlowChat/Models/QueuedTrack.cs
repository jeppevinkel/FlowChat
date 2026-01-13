namespace FlowChat.Models;

public class QueuedTrack
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public TimeSpan? Duration { get; init; }
    public DateTimeOffset QueuedAt { get; init; } = DateTimeOffset.UtcNow;
}