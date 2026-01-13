namespace FlowChat.Models;

public class AudioSource : IDisposable
{
    private readonly Stream _stream;
    public byte[] Buffer { get; }
    public float Volume { get; set; }
    
    public AudioSource(Stream stream, float volume)
    {
        _stream = stream;
        Volume = volume;
        Buffer = new byte[3840]; // Match buffer size
    }
    
    public async Task<int> ReadAsync(int count, CancellationToken ct)
    {
        int totalRead = 0;
        int remaining = count;

        while (remaining > 0)
        {
            int bytesRead = await _stream.ReadAsync(Buffer.AsMemory(totalRead, remaining), ct);
            
            if (bytesRead == 0)
            {
                break; // End of stream
            }

            totalRead += bytesRead;
            remaining -= bytesRead;
        }

        return totalRead;
    }

    public void Dispose()
    {
        _stream?.Dispose();
    }
}