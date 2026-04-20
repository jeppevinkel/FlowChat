using System.Text;
using Discord;
using FlowChat.Models;

namespace FlowChat.Helpers;

public static class EmbedFactory
{
    private static readonly Color ColorNowPlaying = new(0x1DB954);
    private static readonly Color ColorQueue      = new(0x3498DB);
    private static readonly Color ColorDiceNormal = new(0x9B59B6);
    private static readonly Color ColorDiceNat20  = new(0xF1C40F);
    private static readonly Color ColorDiceFail   = new(0xE74C3C);
    private static readonly Color ColorSuccess    = new(0x2ECC71);
    private static readonly Color ColorError      = new(0xE74C3C);
    private static readonly Color ColorInfo       = new(0x3498DB);

    // ── Music ─────────────────────────────────────────────────────────────────

    public static Embed CreateNowPlayingEmbed(QueuedTrack track, TimeSpan progress, QueuedTrack? nextTrack = null)
    {
        var progressBar = BuildProgressBar(progress, track.Duration);

        var builder = new EmbedBuilder()
            .WithTitle("Now Playing")
            .WithDescription($"[{track.Title}]({track.Url})\n{progressBar}")
            .WithColor(ColorNowPlaying);

        if (nextTrack is not null)
            builder.WithFooter($"Next up: {nextTrack.Title}");

        return builder.Build();
    }

    public static Embed CreateQueueEmbed(
        QueuedTrack? current,
        TimeSpan currentProgress,
        IEnumerable<QueuedTrack> queue,
        TimeSpan totalRemaining)
    {
        var queueList = queue.ToList();

        var description = new StringBuilder();

        if (current is not null)
        {
            var progressBar = BuildProgressBar(currentProgress, current.Duration);
            description.AppendLine("**▶ Now Playing**");
            description.AppendLine($"[{current.Title}]({current.Url})");
            description.AppendLine(progressBar);
        }

        if (queueList.Count > 0)
        {
            description.AppendLine();
            description.AppendLine("**Up Next**");
            for (int i = 0; i < Math.Min(queueList.Count, 10); i++)
            {
                var track = queueList[i];
                var duration = track.Duration.HasValue ? $" — {FormatDuration(track.Duration.Value)}" : "";
                description.AppendLine($"{i + 1}. [{track.Title}]({track.Url}){duration}");
            }

            if (queueList.Count > 10)
                description.AppendLine($"*...and {queueList.Count - 10} more*");
        }
        else if (current is null)
        {
            description.AppendLine("The queue is empty.");
        }

        var builder = new EmbedBuilder()
            .WithTitle("Music Queue")
            .WithDescription(description.ToString().TrimEnd())
            .WithColor(ColorQueue);

        var footerParts = new List<string>();
        if (queueList.Count > 0)
            footerParts.Add($"{queueList.Count} track{(queueList.Count == 1 ? "" : "s")} in queue");
        if (totalRemaining > TimeSpan.Zero)
            footerParts.Add($"{FormatDuration(totalRemaining)} total remaining");

        if (footerParts.Count > 0)
            builder.WithFooter(string.Join(" · ", footerParts));

        return builder.Build();
    }

    public static Embed CreateTrackAddedEmbed(QueuedTrack track, int queuePosition, int totalInQueue)
    {
        var playingNow = queuePosition == 0;

        var builder = new EmbedBuilder()
            .WithTitle(playingNow ? "Now Playing" : "Added to Queue")
            .WithDescription($"[{track.Title}]({track.Url})")
            .WithColor(ColorNowPlaying);

        if (track.Duration.HasValue)
            builder.AddField("Duration", FormatDuration(track.Duration.Value), inline: true);

        if (!playingNow)
            builder.AddField("Position", $"#{queuePosition}", inline: true);

        builder.WithFooter(totalInQueue == 1 ? "1 track in queue" : $"{totalInQueue} tracks in queue");

        return builder.Build();
    }

    // ── Dice ──────────────────────────────────────────────────────────────────

    /// <param name="diceLabel">Display name of the die, e.g. "d20".</param>
    /// <param name="result">The final result after applying advantage/disadvantage.</param>
    /// <param name="diceMax">The maximum value of the die, used for nat-20/nat-1 detection.</param>
    /// <param name="firstRoll">First roll, when rolling with advantage or disadvantage.</param>
    /// <param name="secondRoll">Second roll, when rolling with advantage or disadvantage.</param>
    /// <param name="rollTypeName">Display name of the roll type, e.g. "Advantage".</param>
    /// <param name="reason">Optional reason for the roll.</param>
    /// <param name="rollerName">Optional display name of the user who rolled.</param>
    public static Embed CreateDiceRollEmbed(
        string diceLabel,
        int result,
        int diceMax,
        int? firstRoll = null,
        int? secondRoll = null,
        string? rollTypeName = null,
        string? reason = null,
        string? rollerName = null)
    {
        Color color;
        string title;

        if (diceMax == 20 && result == 20)
        {
            color = ColorDiceNat20;
            title = "Natural 20!";
        }
        else if (diceMax == 20 && result == 1)
        {
            color = ColorDiceFail;
            title = "Critical Fail";
        }
        else
        {
            color = ColorDiceNormal;
            title = rollTypeName is not null ? $"{diceLabel} · {rollTypeName}" : diceLabel;
        }

        var builder = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(color);

        builder.AddField("Result", $"**{result}**", inline: true);

        if (firstRoll.HasValue && secondRoll.HasValue)
            builder.AddField("Rolls", $"({firstRoll}, {secondRoll}) → **{result}**", inline: true);

        if (!string.IsNullOrWhiteSpace(reason))
            builder.AddField("For", reason, inline: false);

        if (!string.IsNullOrWhiteSpace(rollerName))
            builder.WithFooter(rollerName);

        return builder.Build();
    }

    // ── General ───────────────────────────────────────────────────────────────

    public static Embed CreateSuccessEmbed(string title, string? description = null) =>
        new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorSuccess)
            .Build();

    public static Embed CreateErrorEmbed(string title, string? description = null) =>
        new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorError)
            .Build();

    public static Embed CreateInfoEmbed(string title, string? description = null) =>
        new EmbedBuilder()
            .WithTitle(title)
            .WithDescription(description)
            .WithColor(ColorInfo)
            .Build();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildProgressBar(TimeSpan progress, TimeSpan? duration, int barLength = 16)
    {
        if (duration is null || duration == TimeSpan.Zero)
            return FormatDuration(progress);

        double ratio = Math.Clamp(progress.TotalSeconds / duration.Value.TotalSeconds, 0.0, 1.0);
        int filled = (int)(ratio * barLength);

        return $"{new string('█', filled)}{new string('░', barLength - filled)} {FormatDuration(progress)} / {FormatDuration(duration.Value)}";
    }

    private static string FormatDuration(TimeSpan duration) =>
        duration.Hours > 0
            ? $"{duration.Hours}:{duration.Minutes:D2}:{duration.Seconds:D2}"
            : $"{duration.Minutes}:{duration.Seconds:D2}";
}
