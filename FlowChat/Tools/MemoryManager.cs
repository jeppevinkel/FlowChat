using System.Text.Json;
using Anthropic.SDK.Common;
using Discord.WebSocket;
using FlowChat.Enums;
using FlowChat.Models;

namespace FlowChat.Tools;

public class MemoryManager
{
    private readonly SocketMessage _message;
    private readonly ISocketMessageChannel _channel;
    private readonly List<Memory> _memories;
    private bool _initialized = false;
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private string PrivateFilePath => $"./memories/private/{_message.Author.Id}.json";
    private string PublicFilePath => $"./memories/public/global.json";

    /// <summary>
    /// Initializes a new instance of the MemoryManager class with the specified message and an empty list of memories.
    /// </summary>
    /// <param name="message">The message associated with the memory manager context.</param>
    public MemoryManager(SocketMessage message) : this(message, [])
    {
    }

    /// <summary>
    /// Initializes a new instance of the MemoryManager class with the specified message and memories.
    /// </summary>
    /// <param name="message">The message associated with the memory manager context.</param>
    /// <param name="memories">The initial list of memories for the memory manager.</param>
    public MemoryManager(SocketMessage message, IEnumerable<Memory> memories) : this(message, memories.ToList())
    {
    }

    /// <summary>
    /// Initializes a new instance of the MemoryManager class with the specified message and memories.
    /// </summary>
    /// <param name="message">The message associated with the memory manager context.</param>
    /// <param name="memories">The initial list of memories for the memory manager.</param>
    public MemoryManager(SocketMessage message, List<Memory> memories)
    {
        _message = message;
        _channel = message.Channel;
        _memories = memories;
    }

    [Function(
        "Store a memory with automatic keyword extraction and categorization. Use this for important information you want to remember.")]
    public async Task<string> StoreMemory(
        [FunctionParameter("Content of the memory", true)] string content,
        [FunctionParameter("Type of memory (MusicPreference, UserFact, Experience, Dislike, General)", false)] MemoryType memoryType = MemoryType.General,
        [FunctionParameter("Importance level 1-10, where 10 is extremely important", false)] int importance = 5,
        [FunctionParameter("Set this to true to make the memory private (only visible when chatting with this person)", false)] bool isPrivate = false)
    {
        await EnsureInitialized();

        var memory = new Memory()
        {
            Content = content,
            UserId = _message.Author.Id,
            Keywords = ExtractKeywords(content),
            Type = memoryType,
            ImportanceScore = Math.Clamp(importance, 1, 10),
            IsPrivate = isPrivate
        };

        _memories.Add(memory);
        await SaveMemories();
    
        return $"Memory stored with {memory.Keywords.Count} keywords extracted.";
    }
    
    [Function(
        "Search for memories related to the current conversation context. Use this automatically when discussing topics that might have relevant stored information.")]
    public async Task<string> SearchRelevantMemories(
        [FunctionParameter("Keywords or topics from the current conversation", true)] string searchContext,
        [FunctionParameter("Type of memory to focus on (optional)", false)] MemoryType? memoryType = null)
    {
        await EnsureInitialized();

        var keywords = searchContext.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var relevantMemories = new List<(Memory memory, int score)>();

        foreach (var memory in _memories)
        {
            int score = CalculateRelevanceScore(memory, keywords, memoryType);
            if (score > 0)
            {
                relevantMemories.Add((memory, score));
            }
        }

        // Return top 5 most relevant memories
        var topMemories = relevantMemories
            .OrderByDescending(x => x.score)
            .Take(5)
            .Select(x => x.memory)
            .ToList();

        if (topMemories.Count == 0)
        {
            return "No relevant memories found.";
        }

        return JsonSerializer.Serialize(topMemories, _jsonOptions);
    }
    
    private int CalculateRelevanceScore(Memory memory, string[] keywords, MemoryType? memoryType = null)
    {
        int score = 0;

        if (memory.IsPrivate && memory.UserId != _message.Author.Id)
        {
            return 0;
        }
    
        // Check content relevance
        var memoryContent = memory.Content.ToLower();
        foreach (var keyword in keywords)
        {
            if (memoryContent.Contains(keyword))
            {
                score += 3;
            }
        }
    
        // Check keyword tags
        foreach (var keyword in keywords)
        {
            if (memory.Keywords.Any(k => k.ToLower().Contains(keyword)))
            {
                score += 2;
            }
        }
    
        // Type matching bonus
        if (memoryType is not null && memory.Type == memoryType)
        {
            score += 5;
        }
    
        // Prioritize the user's own memories
        if (memory.UserId == _message.Author.Id)
        {
            score += 1;
        }
    
        // Recency bonus (newer memories get slight boost)
        var daysSince = (DateTime.UtcNow - memory.Timestamp).TotalDays;
        if (daysSince < 7) score += 2;
        else if (daysSince < 30) score += 1;
    
        // Importance bonus
        score += memory.ImportanceScore;
    
        return score;
    }
    
    private List<string> ExtractKeywords(string content)
    {
        // Simple keyword extraction - you could make this more sophisticated
        var words = content.ToLower()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3) // Filter short words
            .Where(word => !IsStopWord(word)) // Filter common words
            .Distinct()
            .ToList();
    
        return words;
    }

    private bool IsStopWord(string word)
    {
        var stopWords = new HashSet<string> { "this", "that", "with", "from", "they", "them", "their", "there", "where", "when", "what", "have", "been", "were", "said", "each", "which", "about", "other", "more", "very", "what", "know", "just", "first", "also", "after", "back", "good", "come", "could", "make", "time", "only", "right", "into" };
        return stopWords.Contains(word);
    }

    private async Task EnsureInitialized()
    {
        if (!_initialized)
        {
            await LoadMemories();
            _initialized = true;
        }
    }

    private async Task SaveMemories()
    {
        var privateMemories = _memories.Where(m => m.IsPrivate && m.UserId == _message.Author.Id).ToList();
        var publicMemories = _memories.Where(m => !m.IsPrivate).ToList();
        
        // Save private memories to user-specific file
        if (privateMemories.Count != 0)
        {
            Directory.CreateDirectory("./memories/private/");
            var privateJson = JsonSerializer.Serialize(privateMemories, _jsonOptions);
            await File.WriteAllTextAsync(PrivateFilePath, privateJson);
        }
        
        // Merge with existing public memories and save
        if (publicMemories.Count != 0)
        {
            var existingPublic = new List<Memory>();
            if (File.Exists(PublicFilePath))
            {
                var existingJson = await File.ReadAllTextAsync(PublicFilePath);
                existingPublic = JsonSerializer.Deserialize<List<Memory>>(existingJson) ?? [];
            }
        
            // Remove old memories from same user, add new ones
            existingPublic.RemoveAll(m => publicMemories.Any(pm => pm.Id == m.Id));
            existingPublic.AddRange(publicMemories);
        
            Directory.CreateDirectory("./memories/public/");
            var publicJson = JsonSerializer.Serialize(existingPublic, _jsonOptions);
            await File.WriteAllTextAsync(PublicFilePath, publicJson);
        }
    }

    private async Task LoadMemories()
    {
        // Load user's private memories
        if (File.Exists(PrivateFilePath))
        {
            var privateJson = await File.ReadAllTextAsync(PrivateFilePath);
            var privateMemories = JsonSerializer.Deserialize<List<Memory>>(privateJson) ?? [];

            _memories.AddRange(privateMemories);
        }
        
        // Load relevant public memories (filter by context/relevance)
        if (File.Exists(PublicFilePath))
        {
            var publicJson = await File.ReadAllTextAsync(PublicFilePath);
            var allPublicMemories = JsonSerializer.Deserialize<List<Memory>>(publicJson) ?? [];
            
            _memories.AddRange(allPublicMemories);
        }
    }
}