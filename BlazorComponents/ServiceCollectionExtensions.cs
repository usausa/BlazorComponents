namespace BlazorComponents;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorComponents(this IServiceCollection services)
    {
        services.TryAddScoped<IScreen, Screen>();
        services.TryAddScoped<ISpeaker, Speaker>();
        services.TryAddScoped<IBrowserTabs, BrowserTabs>();
        services.TryAddScoped<IClipboard, Clipboard>();
        services.TryAddScoped<IGeolocation, Geolocation>();
        return services;
    }
}
