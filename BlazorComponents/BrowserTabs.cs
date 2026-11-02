namespace BlazorComponents;

using Microsoft.JSInterop;

internal sealed class BrowserTabs : IBrowserTabs, IAsyncDisposable
{
    private const string ModulePath = "./_content/Components.Blazor/browser-tabs.js";

    private readonly List<Listener> listeners = [];

    private readonly ScriptObject<BrowserTabs> script;

    private bool disposed;

    public BrowserTabs(IJSRuntime jsRuntime)
    {
        script = new ScriptObject<BrowserTabs>(jsRuntime, ModulePath, this);
    }

    public async ValueTask RunLockedAsync(string name, Func<Task> action, TimeSpan? timeout = null) =>
        await RunLockedAsync(
            name,
            async () =>
            {
                await action().ConfigureAwait(true);
                return true;
            },
            timeout).ConfigureAwait(true);

    public async ValueTask<T> RunLockedAsync<T>(string name, Func<Task<T>> action, TimeSpan? timeout = null)
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        var locked = new LockedAction<T>(action);
        using var reference = DotNetObjectReference.Create(locked);
        var milliseconds = timeout is { } value ? (long)value.TotalMilliseconds : -1;
        var acquired = await controller.InvokeAsync<bool>("runLocked", CancellationToken.None, [name, milliseconds, reference]).ConfigureAwait(true);
        return acquired && (locked.Running is { } running)
            ? await running.ConfigureAwait(true)
            : await action().ConfigureAwait(true);
    }

    public async ValueTask<IAsyncDisposable> ListenAsync(string name, Func<string, Task> received)
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        var listener = new Listener(this, name, received);
        listeners.Add(listener);
        await controller.InvokeVoidAsync("listen", name).ConfigureAwait(true);
        return listener;
    }

    public async ValueTask TellAsync(string name, string message)
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        await controller.InvokeVoidAsync("tell", name, message).ConfigureAwait(true);
    }

    [JSInvokable]
    public async Task NotifyMessage(string name, string message)
    {
        foreach (var listener in listeners.Where(x => x.Name == name).ToArray())
        {
            await listener.Received(message).ConfigureAwait(true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        listeners.Clear();
        await script.DisposeAsync().ConfigureAwait(true);
    }

    private async ValueTask RemoveAsync(Listener listener)
    {
        if (!listeners.Remove(listener) || disposed || listeners.Exists(x => x.Name == listener.Name))
        {
            return;
        }

        try
        {
            var controller = await script.GetAsync().ConfigureAwait(true);
            await controller.InvokeVoidAsync("unlisten", listener.Name).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    internal sealed class LockedAction<T>
    {
        private readonly Func<Task<T>> action;

        public LockedAction(Func<Task<T>> action)
        {
            this.action = action;
        }

        public Task<T>? Running { get; private set; }

        [JSInvokable]
        public async Task RunAsync()
        {
            Running = InvokeAsync();
            await ((Task)Running).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        private async Task<T> InvokeAsync() => await action().ConfigureAwait(true);
    }

    private sealed class Listener : IAsyncDisposable
    {
        private readonly BrowserTabs owner;

        public Listener(BrowserTabs owner, string name, Func<string, Task> received)
        {
            this.owner = owner;
            Name = name;
            Received = received;
        }

        public string Name { get; }

        public Func<string, Task> Received { get; }

        public ValueTask DisposeAsync() => owner.RemoveAsync(this);
    }
}
