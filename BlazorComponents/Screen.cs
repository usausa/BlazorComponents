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
        var controller = await script.GetAsync().ConfigureAwait(true);
        NotifyScreenKeptOn(await controller.InvokeAsync<bool>("keepScreenOn", value).ConfigureAwait(true));
    }

    public async ValueTask KeepFullscreenAsync(bool value)
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        await controller.InvokeVoidAsync("keepFullscreen", value).ConfigureAwait(true);
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
