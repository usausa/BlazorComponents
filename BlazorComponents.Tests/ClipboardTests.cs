namespace BlazorComponents;

using Microsoft.JSInterop;

public sealed class ClipboardTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/clipboard.js";

    public ClipboardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WritesWithClipboardApi()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", "text").SetVoidResult();
        var clipboard = new Clipboard(JSInterop.JSRuntime);

        var result = await clipboard.SetTextAsync("text");

        Assert.True(result);
        JSInterop.VerifyInvoke("navigator.clipboard.writeText");
        JSInterop.VerifyNotInvoke("import");
        await clipboard.DisposeAsync();
    }

    [Fact]
    public async Task CopiesWithFallbackWhenApiFails()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", "text").SetException(new JSException("Could not find 'navigator.clipboard.writeText'"));
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<bool>("copy", "text").SetResult(true);
        var clipboard = new Clipboard(JSInterop.JSRuntime);

        var result = await clipboard.SetTextAsync("text");

        Assert.True(result);
        module.VerifyInvoke("copy");
        await clipboard.DisposeAsync();
    }

    [Fact]
    public async Task ReturnsFalseWhenNotCopied()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", "text").SetException(new JSException("NotAllowedError"));
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<bool>("copy", "text").SetResult(false);
        var clipboard = new Clipboard(JSInterop.JSRuntime);

        var result = await clipboard.SetTextAsync("text");

        Assert.False(result);
        await clipboard.DisposeAsync();
    }

    [Fact]
    public async Task ThrowsAfterDispose()
    {
        var clipboard = new Clipboard(JSInterop.JSRuntime);

        await clipboard.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => clipboard.SetTextAsync("text").AsTask());
    }
}
