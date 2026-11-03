namespace BlazorComponents;

using Microsoft.Extensions.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests : BunitContext
{
    [Fact]
    public void AddBlazorComponentsRegistersScopedServicesOnce()
    {
        var services = new ServiceCollection();

        services.AddBlazorComponents();
        services.AddBlazorComponents();

        (Type Service, Type Implementation)[] expected =
        [
            (typeof(IScreen), typeof(Screen)),
            (typeof(ISpeaker), typeof(Speaker)),
            (typeof(IBrowserTabs), typeof(BrowserTabs)),
            (typeof(IClipboard), typeof(Clipboard)),
            (typeof(IGeolocation), typeof(Geolocation)),
            (typeof(IFileSystemAccess), typeof(FileSystemAccess))
        ];
        Assert.Equal(expected, services.Select(static d => (d.ServiceType, d.ImplementationType!)));
        Assert.All(services, static d => Assert.Equal(ServiceLifetime.Scoped, d.Lifetime));
    }

    [Fact]
    public void ResolvesServicesWithJSRuntime()
    {
        Services.AddBlazorComponents();

        Assert.IsType<Screen>(Services.GetRequiredService<IScreen>());
        Assert.IsType<Speaker>(Services.GetRequiredService<ISpeaker>());
        Assert.IsType<BrowserTabs>(Services.GetRequiredService<IBrowserTabs>());
        Assert.IsType<Clipboard>(Services.GetRequiredService<IClipboard>());
        Assert.IsType<Geolocation>(Services.GetRequiredService<IGeolocation>());
        Assert.IsType<FileSystemAccess>(Services.GetRequiredService<IFileSystemAccess>());
    }
}
