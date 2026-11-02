namespace BlazorComponents;

using Microsoft.JSInterop;

internal sealed class Clipboard : IClipboard, IAsyncDisposable
{
    private const string ModulePath = "./_content/Components.Blazor/clipboard.js";

    private readonly IJSRuntime jsRuntime;

    private readonly ScriptModule module;

    private bool disposed;

    public Clipboard(IJSRuntime jsRuntime)
    {
        this.jsRuntime = jsRuntime;
        module = new ScriptModule(jsRuntime, ModulePath);
    }

    public async ValueTask<bool> SetTextAsync(string text)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        try
        {
            await jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", text).ConfigureAwait(true);
            return true;
        }
        catch (JSException)
        {
            return await module.InvokeAsync<bool>("copy", text).ConfigureAwait(true);
        }
    }

    public ValueTask DisposeAsync()
    {
        disposed = true;
        return module.DisposeAsync();
    }
}
