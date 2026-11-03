namespace BlazorComponents;

using Microsoft.JSInterop;

using TestContext = Xunit.TestContext;

public sealed class FileSystemAccessTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/file-system-access.js";

    public FileSystemAccessTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WritesStreamWhereUserPicks()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<int>("pick", static _ => true).SetResult(3);
        var access = new FileSystemAccess(JSInterop.JSRuntime);
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await access.SaveAsync("report.pdf", "application/pdf", stream, TestContext.Current.CancellationToken);

        Assert.Equal(FileSaveResult.Saved, result);
        var pick = module.VerifyInvoke("pick");
        Assert.Equal("report.pdf", pick.Arguments[0]);
        Assert.Equal("application/pdf", pick.Arguments[1]);
        var write = module.VerifyInvoke("write");
        Assert.Equal(3, write.Arguments[0]);
        Assert.Same(stream, Assert.IsType<DotNetStreamReference>(write.Arguments[1]).Stream);
        Assert.True(stream.CanRead);
        module.VerifyNotInvoke("download");
        await access.DisposeAsync();
    }

    [Fact]
    public async Task SendsNothingWhenUserCancels()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<int>("pick", static _ => true).SetResult(0);
        var access = new FileSystemAccess(JSInterop.JSRuntime);

        var result = await access.SaveAsync("report.pdf", "application/pdf", [1, 2, 3], TestContext.Current.CancellationToken);

        Assert.Equal(FileSaveResult.Canceled, result);
        module.VerifyNotInvoke("write");
        module.VerifyNotInvoke("download");
        await access.DisposeAsync();
    }

    [Fact]
    public async Task DownloadsWhenPickerIsUnavailable()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<int>("pick", static _ => true).SetResult(-1);
        var access = new FileSystemAccess(JSInterop.JSRuntime);

        var result = await access.SaveAsync("report.pdf", "application/pdf", [1, 2, 3], TestContext.Current.CancellationToken);

        Assert.Equal(FileSaveResult.Downloaded, result);
        var download = module.VerifyInvoke("download");
        Assert.Equal("report.pdf", download.Arguments[0]);
        Assert.Equal("application/pdf", download.Arguments[1]);
        Assert.IsType<DotNetStreamReference>(download.Arguments[2]);
        module.VerifyNotInvoke("write");
        await access.DisposeAsync();
    }

    [Theory]
    [InlineData("saved", FileSaveResult.Saved)]
    [InlineData("canceled", FileSaveResult.Canceled)]
    [InlineData("downloaded", FileSaveResult.Downloaded)]
    public async Task SavesUrl(string value, FileSaveResult expected)
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<string>("saveUrl", static _ => true).SetResult(value);
        var access = new FileSystemAccess(JSInterop.JSRuntime);

        var result = await access.SaveAsync("report.pdf", "application/pdf", new Uri("files/report.pdf", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
        var invocation = module.VerifyInvoke("saveUrl");
        Assert.Equal("report.pdf", invocation.Arguments[0]);
        Assert.Equal("application/pdf", invocation.Arguments[1]);
        Assert.Equal("files/report.pdf", invocation.Arguments[2]);
        await access.DisposeAsync();
    }

    [Fact]
    public async Task PassesCancellationToken()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.Setup<int>("pick", static _ => true).SetResult(1);
        module.Setup<string>("saveUrl", static _ => true).SetResult("saved");
        var access = new FileSystemAccess(JSInterop.JSRuntime);
        using var source = new CancellationTokenSource();

        await access.SaveAsync("report.pdf", "application/pdf", [1], source.Token);
        await access.SaveAsync("report.pdf", "application/pdf", new Uri("files/report.pdf", UriKind.Relative), source.Token);

        Assert.Equal(source.Token, module.VerifyInvoke("pick").CancellationToken);
        Assert.Equal(source.Token, module.VerifyInvoke("write").CancellationToken);
        Assert.Equal(source.Token, module.VerifyInvoke("saveUrl").CancellationToken);
        await access.DisposeAsync();
    }

    [Fact]
    public async Task ThrowsAfterDispose()
    {
        var access = new FileSystemAccess(JSInterop.JSRuntime);

        await access.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => access.SaveAsync("report.pdf", "application/pdf", [1], TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => access.SaveAsync("report.pdf", "application/pdf", new Uri("files/report.pdf", UriKind.Relative), TestContext.Current.CancellationToken).AsTask());
    }
}
