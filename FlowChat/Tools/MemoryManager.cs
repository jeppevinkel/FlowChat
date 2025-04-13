using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using FlowChat.Models;

namespace FlowChat.Tools;

public class MemoryManager
{
    public readonly ulong Id;
    public readonly List<Memory> Memories;
    private bool initialized = false;

    private string FilePath => $"./memories/{Id}.json";

    public MemoryManager(ulong id) : this(id, [])
    {
    }

    public MemoryManager(ulong id, IEnumerable<Memory> memories) : this(id, memories.ToList())
    {
    }

    public MemoryManager(ulong id, List<Memory> memories)
    {
        Id = id;
        Memories = memories;
    }

    [Function(
        "Store a memory from the conversation for later retrieval. This is a way for the assistant to keep track of key information")]
    public async Task<string> StoreMemory([FunctionParameter("Content of the memory", true)] string content)
    {
        await EnsureInitialized();

        Memories.Add(new Memory()
        {
            Content = content
        });

        await SaveMemories();
        return "Memory stored successfully.";
    }

    [Function(
        "Read the currently stored memories. This is a way for the assistant to keep track of key information")]
    public async Task<string> ReadMemories()
    {
        await EnsureInitialized();

        return JsonSerializer.Serialize(Memories);
        ;
    }

    private async Task EnsureInitialized()
    {
        if (!initialized)
        {
            await LoadMemories();
            initialized = true;
        }
    }

    private async Task SaveMemories()
    {
        var json = JsonSerializer.Serialize(Memories, new JsonSerializerOptions()
        {
            WriteIndented = true,
        });
        Directory.CreateDirectory("./memories/");

        await File.WriteAllTextAsync(FilePath, json);
    }

    private async Task LoadMemories()
    {
        if (File.Exists(FilePath))
        {
            var json = await File.ReadAllTextAsync(FilePath);
            Memories.AddRange(JsonSerializer.Deserialize<List<Memory>>(json) ?? []);
        }
    }

    public static void UpdateMemory(AnthropicClient client)
    {
        
    }
}