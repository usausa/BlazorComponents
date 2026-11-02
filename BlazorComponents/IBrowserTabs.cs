namespace BlazorComponents;

public interface IBrowserTabs
{
    ValueTask RunLockedAsync(string name, Func<Task> action, TimeSpan? timeout = null);

    ValueTask<T> RunLockedAsync<T>(string name, Func<Task<T>> action, TimeSpan? timeout = null);

    ValueTask<IAsyncDisposable> ListenAsync(string name, Func<string, Task> received);

    ValueTask TellAsync(string name, string message);
}
