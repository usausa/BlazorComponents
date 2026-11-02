namespace BlazorComponents;

public sealed class ScreenTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/screen.js";

    public ScreenTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task KeepScreenOnTakesStateFromResult()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("keepScreenOn", static invocation => invocation.Arguments[0] is true).SetResult(true);
        controller.Setup<bool>("keepScreenOn", static invocation => invocation.Arguments[0] is false).SetResult(false);
        var screen = new Screen(JSInterop.JSRuntime);
        var values = new List<bool>();
        screen.ScreenKeptOnChanged += (_, _) => values.Add(screen.IsScreenKeptOn);

        await screen.KeepScreenOnAsync(true);
        await screen.KeepScreenOnAsync(true);
        await screen.KeepScreenOnAsync(false);

        bool[] expected = [true, false];
        Assert.Equal(expected, values);
        module.VerifyInvoke("create");
        await screen.DisposeAsync();
    }

    [Fact]
    public async Task NotifiesOnlyWhenStateChanges()
    {
        var screen = new Screen(JSInterop.JSRuntime);
        var values = new List<bool>();
        screen.ScreenKeptOnChanged += (_, _) => values.Add(screen.IsScreenKeptOn);

        screen.NotifyScreenKeptOn(false);
        screen.NotifyScreenKeptOn(true);
        screen.NotifyScreenKeptOn(true);
        screen.NotifyScreenKeptOn(false);

        bool[] expected = [true, false];
        Assert.Equal(expected, values);
        await screen.DisposeAsync();
    }

    [Fact]
    public async Task KeepFullscreenPassesValue()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("keepFullscreen", static _ => true).SetVoidResult();
        var screen = new Screen(JSInterop.JSRuntime);

        await screen.KeepFullscreenAsync(true);
        await screen.KeepFullscreenAsync(false);

        Assert.Collection(
            controller.VerifyInvoke("keepFullscreen", 2),
            static invocation => Assert.True(invocation.Arguments[0] is true),
            static invocation => Assert.True(invocation.Arguments[0] is false));
        await screen.DisposeAsync();
    }

    [Fact]
    public async Task DisposeReleasesController()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("keepScreenOn", static _ => true).SetResult(true);
        controller.SetupVoid("dispose").SetVoidResult();
        var screen = new Screen(JSInterop.JSRuntime);
        await screen.KeepScreenOnAsync(true);
        var raised = 0;
        screen.ScreenKeptOnChanged += (_, _) => raised++;

        await screen.DisposeAsync();
        screen.NotifyScreenKeptOn(false);

        controller.VerifyInvoke("dispose");
        Assert.Equal(0, raised);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => screen.KeepScreenOnAsync(true).AsTask());
    }
}
