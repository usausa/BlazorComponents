namespace BlazorComponents;

public sealed class GeolocationTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/geolocation.js";

    public GeolocationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task ReturnsPosition()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<double[]>("getLocation", static _ => true).SetResult([0, 35.68, 139.76, 12.5]);
        var geolocation = new Geolocation(JSInterop.JSRuntime);

        var result = await geolocation.GetLocationAsync(true, TimeSpan.FromSeconds(3));

        Assert.Equal(new GeolocationResult(GeolocationStatus.Success, new GeolocationPosition(35.68, 139.76, 12.5)), result);
        object?[] expected = [true, 3000L];
        Assert.Equal(expected, controller.VerifyInvoke("getLocation").Arguments);
        await geolocation.DisposeAsync();
    }

    [Theory]
    [InlineData(1, GeolocationStatus.Denied)]
    [InlineData(2, GeolocationStatus.Unavailable)]
    [InlineData(3, GeolocationStatus.Timeout)]
    [InlineData(4, GeolocationStatus.NotSupported)]
    public async Task ReturnsStatusWithoutPosition(int code, GeolocationStatus status)
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<double[]>("getLocation", static _ => true).SetResult([code]);
        var geolocation = new Geolocation(JSInterop.JSRuntime);

        var result = await geolocation.GetLocationAsync();

        Assert.Equal(status, result.Status);
        Assert.Null(result.Position);
        object?[] expected = [false, -1L];
        Assert.Equal(expected, controller.VerifyInvoke("getLocation").Arguments);
        await geolocation.DisposeAsync();
    }

    [Fact]
    public async Task DisposeReleasesController()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<double[]>("getLocation", static _ => true).SetResult([4]);
        controller.SetupVoid("dispose").SetVoidResult();
        var geolocation = new Geolocation(JSInterop.JSRuntime);
        await geolocation.GetLocationAsync();

        await geolocation.DisposeAsync();

        controller.VerifyInvoke("dispose");
        await Assert.ThrowsAsync<ObjectDisposedException>(() => geolocation.GetLocationAsync().AsTask());
    }
}
