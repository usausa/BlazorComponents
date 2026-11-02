namespace BlazorComponents;

public sealed class FileDropZoneTests : BunitContext
{
    private const string ModulePath = "./_content/BlazorComponents/file-drop-zone.js";

    public FileDropZoneTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void RendersDivWithAttributesAndContent()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        var cut = Render<FileDropZone>(static parameters => parameters
            .AddUnmatched("class", "drop")
            .AddChildContent("<input type=\"file\" />"));

        var zone = cut.Find("div");
        Assert.Equal("drop", zone.GetAttribute("class"));
        Assert.NotNull(zone.QuerySelector("input[type=file]"));
    }

    [Fact]
    public void AttachesWithDefaultOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        Render<FileDropZone>();

        var arguments = module.VerifyInvoke("attach").Arguments;
        Assert.Equal("drag-over,False", string.Join(',', arguments.Skip(1)));
    }

    [Fact]
    public void AttachesWithOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        Render<FileDropZone>(static parameters => parameters
            .Add(static p => p.DragOverClass, "active")
            .Add(static p => p.Paste, true));

        var arguments = module.VerifyInvoke("attach").Arguments;
        Assert.Equal("active,True", string.Join(',', arguments.Skip(1)));
    }

    [Fact]
    public void UpdatesOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("update", static _ => true).SetVoidResult();

        var cut = Render<FileDropZone>();
        cut.WaitForAssertion(() => module.VerifyInvoke("attach"));

        cut.Render(static parameters => parameters.Add(static p => p.Paste, true));

        cut.WaitForAssertion(() => Assert.Equal("drag-over,True", string.Join(',', zone.VerifyInvoke("update").Arguments)));
        Assert.Single(module.Invocations["attach"]);
    }

    [Fact]
    public async Task OpensFilePicker()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);
        module.SetupVoid("open", static _ => true).SetVoidResult();

        var cut = Render<FileDropZone>();
        await cut.InvokeAsync(() => cut.Instance.OpenFilePickerAsync().AsTask());

        module.VerifyInvoke("open");
    }

    [Fact]
    public async Task DisposeReleasesZone()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("dispose").SetVoidResult();

        var cut = Render<FileDropZone>();
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        await DisposeComponentsAsync();

        zone.VerifyInvoke("dispose");
    }
}
