namespace BlazorComponents;

using Microsoft.JSInterop;

public static class ScriptExtensions
{
    public static ValueTask SetFocus(this IJSRuntime runtime, string id) =>
        InvokeIgnoringDisconnectAsync(runtime, "BlazorComponents.setFocus", id);

    public static ValueTask HistoryBack(this IJSRuntime runtime) =>
        InvokeIgnoringDisconnectAsync(runtime, "history.back");

    public static ValueTask HistoryForward(this IJSRuntime runtime) =>
        InvokeIgnoringDisconnectAsync(runtime, "history.forward");

    public static ValueTask HistoryGo(this IJSRuntime runtime, int delta) =>
        InvokeIgnoringDisconnectAsync(runtime, "history.go", delta);

    public static async ValueTask SaveAsFile(this IJSRuntime runtime, string filename, string contentType, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var streamReference = new DotNetStreamReference(stream, leaveOpen: true);
        await InvokeIgnoringDisconnectAsync(runtime, "BlazorComponents.saveAsFile", filename, contentType, streamReference).ConfigureAwait(false);
    }

    public static async ValueTask OpenNewWindow(this IJSRuntime runtime, string contentType, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var streamReference = new DotNetStreamReference(stream, leaveOpen: true);
        await InvokeIgnoringDisconnectAsync(runtime, "BlazorComponents.openNewWindow", contentType, streamReference).ConfigureAwait(false);
    }

    private static async ValueTask InvokeIgnoringDisconnectAsync(IJSRuntime runtime, string identifier, params object?[] args)
    {
        try
        {
            await runtime.InvokeVoidAsync(identifier, args).ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
