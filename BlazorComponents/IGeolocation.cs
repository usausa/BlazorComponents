namespace BlazorComponents;

public interface IGeolocation
{
    ValueTask<GeolocationResult> GetLocationAsync(bool highAccuracy = false, TimeSpan? timeout = null);
}
