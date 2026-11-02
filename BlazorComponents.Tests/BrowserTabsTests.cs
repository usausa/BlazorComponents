namespace BlazorComponents;

using Microsoft.JSInterop;

public sealed class BrowserTabsTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/browser-tabs.js";

    public BrowserTabsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task RunsActionInsideLock()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("tell", static _ => true).SetVoidResult();
        var runLocked = controller.Setup<bool>("runLocked", static _ => true);
        var tabs = new BrowserTabs(JSInterop.JSRuntime);
        await tabs.TellAsync("prepare", "created");
        var runs = 0;

        var task = tabs.RunLockedAsync("lock", () => Task.FromResult(++runs * 42), TimeSpan.FromSeconds(1)).AsTask();
        var invocation = controller.VerifyInvoke("runLocked");
        var reference = Assert.IsType<DotNetObjectReference<BrowserTabs.LockedAction<int>>>(invocation.Arguments[2]);
        await reference.Value.RunAsync();
        runLocked.SetResult(true);
        var result = await task.ConfigureAwait(true);

        Assert.Equal(42, result);
        Assert.Equal(1, runs);
        Assert.Equal("lock", invocation.Arguments[0]);
        Assert.Equal(1000L, invocation.Arguments[1]);
        await tabs.DisposeAsync();
    }

    [Fact]
    public async Task ThrowsExceptionOfAction()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("tell", static _ => true).SetVoidResult();
        var runLocked = controller.Setup<bool>("runLocked", static _ => true);
        var tabs = new BrowserTabs(JSInterop.JSRuntime);
        await tabs.TellAsync("prepare", "created");

        var task = tabs.RunLockedAsync<int>("lock", static () => throw new InvalidOperationException("failed")).AsTask();
        var reference = Assert.IsType<DotNetObjectReference<BrowserTabs.LockedAction<int>>>(controller.VerifyInvoke("runLocked").Arguments[2]);
        await reference.Value.RunAsync();
        runLocked.SetResult(true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
        Assert.Equal("failed", exception.Message);
        await tabs.DisposeAsync();
    }

    [Fact]
    public async Task RunsWithoutLockWhenNotTaken()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("runLocked", static _ => true).SetResult(false);
        var tabs = new BrowserTabs(JSInterop.JSRuntime);
        var runs = 0;

        var result = await tabs.RunLockedAsync("lock", () => Task.FromResult(++runs));
        await tabs.RunLockedAsync("lock", () =>
        {
            runs++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, result);
        Assert.Equal(2, runs);
        Assert.All(controller.VerifyInvoke("runLocked", 2), static invocation => Assert.Equal(-1L, invocation.Arguments[1]));
        await tabs.DisposeAsync();
    }

    [Fact]
    public async Task ReceivesMessagesOfNameUntilDisposed()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("listen", static _ => true).SetVoidResult();
        controller.SetupVoid("unlisten", static _ => true).SetVoidResult();
        var tabs = new BrowserTabs(JSInterop.JSRuntime);
        var received = new List<string>();

        var listening = await tabs.ListenAsync("session", message =>
        {
            received.Add(message);
            return Task.CompletedTask;
        });
        await tabs.NotifyMessage("session", "logged-out");
        await tabs.NotifyMessage("other", "ignored");
        await listening.DisposeAsync();
        await tabs.NotifyMessage("session", "after");

        string[] expected = ["logged-out"];
        Assert.Equal(expected, received);
        Assert.Equal("session", controller.VerifyInvoke("listen").Arguments[0]);
        Assert.Equal("session", controller.VerifyInvoke("unlisten").Arguments[0]);
        await tabs.DisposeAsync();
    }

    [Fact]
    public async Task StopsListeningAfterLastListener()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("listen", static _ => true).SetVoidResult();
        controller.SetupVoid("unlisten", static _ => true).SetVoidResult();
        var tabs = new BrowserTabs(JSInterop.JSRuntime);

        var first = await tabs.ListenAsync("session", static _ => Task.CompletedTask);
        var second = await tabs.ListenAsync("session", static _ => Task.CompletedTask);
        await first.DisposeAsync();
        await first.DisposeAsync();
        controller.VerifyNotInvoke("unlisten");
        await second.DisposeAsync();

        controller.VerifyInvoke("unlisten");
        await tabs.DisposeAsync();
    }

    [Fact]
    public async Task TellsMessage()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("tell", static _ => true).SetVoidResult();
        var tabs = new BrowserTabs(JSInterop.JSRuntime);

        await tabs.TellAsync("session", "logged-out");

        object?[] expected = ["session", "logged-out"];
        Assert.Equal(expected, controller.VerifyInvoke("tell").Arguments);
        await tabs.DisposeAsync();
    }

    [Fact]
    public async Task DisposeReleasesController()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("listen", static _ => true).SetVoidResult();
        controller.SetupVoid("dispose").SetVoidResult();
        var tabs = new BrowserTabs(JSInterop.JSRuntime);
        var received = 0;
        var listening = await tabs.ListenAsync("session", _ =>
        {
            received++;
            return Task.CompletedTask;
        });

        await tabs.DisposeAsync();
        await tabs.NotifyMessage("session", "after");
        await listening.DisposeAsync();

        controller.VerifyInvoke("dispose");
        Assert.Equal(0, received);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => tabs.TellAsync("session", "after").AsTask());
    }
}
