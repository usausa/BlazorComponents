namespace BlazorComponents;

using Microsoft.JSInterop;

internal sealed class Screen : IScreen, IAsyncDisposable
{
    private const string ModulePath = "./_content/Components.Blazor/screen.js";

    private readonly ScriptObject<Screen> script;

    private bool disposed;

    public Screen(IJSRuntime jsRuntime)
    {
        script = new ScriptObject<Screen>(jsRuntime, ModulePath, this);
    }

    public event EventHandler? ScreenKeptOnChanged;

    public bool IsScreenKeptOn { get; private set; }

    public async ValueTask KeepScreenOnAsync(bool value)
    {
        bool kept;
        try
        {
            var controller = await script.GetAsync().ConfigureAwait(true);
            kept = await controller.InvokeAsync<bool>("keepScreenOn", value).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
            kept = false;
        }

        NotifyScreenKeptOn(kept);
    }

    public async ValueTask KeepFullscreenAsync(bool value)
    {
        try
        {
            var controller = await script.GetAsync().ConfigureAwait(true);
            await controller.InvokeVoidAsync("keepFullscreen", value).ConfigureAwait(true);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    [JSInvokable]
    public void NotifyScreenKeptOn(bool value)
    {
        if (disposed || (value == IsScreenKeptOn))
        {
            return;
        }

        IsScreenKeptOn = value;
        ScreenKeptOnChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await script.DisposeAsync().ConfigureAwait(true);
    }
}
