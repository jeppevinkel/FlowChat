namespace FlowChat.Services.Interfaces;

public interface IBackgroundTaskQueue
{
    ValueTask QueueBackgroundWorkItemAsync(Func<CancellationToken, ValueTask> workItem);

    ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken = default);

    int Count();
}