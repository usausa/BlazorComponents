namespace BlazorComponents;

using Microsoft.JSInterop;

public static class ScriptExtensions
{
    public static ValueTask SetFocus(this IJSRuntime runtime, string id) =>
        runtime.InvokeVoidAsync("BlazorComponents.setFocus", id);

    public static ValueTask HistoryBack(this IJSRuntime runtime) =>
        runtime.InvokeVoidAsync("history.back");

    public static ValueTask HistoryForward(this IJSRuntime runtime) =>
        runtime.InvokeVoidAsync("history.forward");

    public static ValueTask HistoryGo(this IJSRuntime runtime, int delta) =>
        runtime.InvokeVoidAsync("history.go", delta);

    public static async ValueTask SaveAsFile(this IJSRuntime runtime, string filename, string contentType, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var streamReference = new DotNetStreamReference(stream, leaveOpen: true);
        await runtime.InvokeVoidAsync("BlazorComponents.saveAsFile", filename, contentType, streamReference).ConfigureAwait(false);
    }

    public static async ValueTask OpenNewWindow(this IJSRuntime runtime, string contentType, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var streamReference = new DotNetStreamReference(stream, leaveOpen: true);
        await runtime.InvokeVoidAsync("BlazorComponents.openNewWindow", contentType, streamReference).ConfigureAwait(false);
    }
}
