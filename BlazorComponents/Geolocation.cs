namespace BlazorComponents;

using Microsoft.JSInterop;

internal sealed class Geolocation : IGeolocation, IAsyncDisposable
{
    private const string ModulePath = "./_content/Components.Blazor/geolocation.js";

    private readonly ScriptObject<Geolocation> script;

    public Geolocation(IJSRuntime jsRuntime)
    {
        script = new ScriptObject<Geolocation>(jsRuntime, ModulePath, this);
    }

    public async ValueTask<GeolocationResult> GetLocationAsync(bool highAccuracy = false, TimeSpan? timeout = null)
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        var milliseconds = timeout is { } value ? (long)value.TotalMilliseconds : -1;
        var values = await controller.InvokeAsync<double[]>("getLocation", CancellationToken.None, [highAccuracy, milliseconds]).ConfigureAwait(true);
        var status = (GeolocationStatus)(int)values[0];
        return new GeolocationResult(status, status == GeolocationStatus.Success ? new GeolocationPosition(values[1], values[2], values[3]) : null);
    }

    public ValueTask DisposeAsync() => script.DisposeAsync();
}
