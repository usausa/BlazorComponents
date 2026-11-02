namespace BlazorComponents;

using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

public sealed class FileUploadTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/file-upload.js";

    public FileUploadTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void RendersDivWithAttributesAndContent()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        var cut = Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload")
            .AddUnmatched("class", "drop")
            .AddChildContent("<p>files</p>"));

        var zone = cut.Find("div");
        Assert.Equal("drop", zone.GetAttribute("class"));
        Assert.Equal("files", zone.QuerySelector("p")?.TextContent);
    }

    [Fact]
    public void AttachesWithDefaultOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload"));

        var arguments = module.VerifyInvoke("attach").Arguments;
        Assert.Equal("drag-over,False,,True,False,False", string.Join(',', arguments.Skip(2)));
    }

    [Fact]
    public void AttachesWithOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload")
            .Add(static p => p.DragOverClass, "active")
            .Add(static p => p.Paste, true)
            .Add(static p => p.Accept, ".txt")
            .Add(static p => p.Multiple, false)
            .Add(static p => p.AllowFolders, true)
            .Add(static p => p.Disabled, true));

        var arguments = module.VerifyInvoke("attach").Arguments;
        Assert.Equal("active,True,.txt,False,True,True", string.Join(',', arguments.Skip(2)));
    }

    [Fact]
    public void UpdatesOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("update", static _ => true).SetVoidResult();

        var cut = Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload"));
        cut.WaitForAssertion(() => module.VerifyInvoke("attach"));

        cut.Render(static parameters => parameters.Add(static p => p.Disabled, true));

        cut.WaitForAssertion(() => Assert.Equal("drag-over,False,,True,False,True", string.Join(',', zone.VerifyInvoke("update").Arguments)));
        Assert.Single(module.Invocations["attach"]);
    }

    [Fact]
    public async Task StartingReadsEntriesAndNotifiesUploading()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.Setup<FileUploadEntry[]>("entries", static invocation => (int)invocation.Arguments[0]! == 0)
            .SetResult([new("photos", true, 300), new("a.txt", false, 10)]);
        zone.Setup<FileUploadEntry[]>("entries", static invocation => (int)invocation.Arguments[0]! == 2)
            .SetResult([new("b.txt", false, 20)]);
        FileUploadStartingEventArgs? starting = null;
        var values = new List<bool>();

        var cut = Render<FileUpload>(parameters => parameters
            .Add(static p => p.Url, "/upload")
            .Add(static p => p.OnStarting, e => starting = e)
            .Add(static p => p.UploadingChanged, values.Add));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        var start = await cut.InvokeAsync(() => cut.Instance.NotifyStarting(3, 12, 330));

        Assert.NotNull(starting);
        Assert.Collection(
            starting.Entries,
            static entry => Assert.Equal(("photos", true, 300L), (entry.Name, entry.IsFolder, entry.Size)),
            static entry => Assert.Equal(("a.txt", false, 10L), (entry.Name, entry.IsFolder, entry.Size)),
            static entry => Assert.Equal(("b.txt", false, 20L), (entry.Name, entry.IsFolder, entry.Size)));
        Assert.Equal((12, 330L), (starting.FileCount, starting.TotalBytes));
        Assert.NotNull(start);
        Assert.Equal("/upload,files,4", string.Join(',', start.Take(3)));
        Assert.Empty(Assert.IsType<Dictionary<string, string>>(start[3]));
        Assert.True(Assert.Single(values));
    }

    [Fact]
    public async Task StartingCanBeCanceled()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.Setup<FileUploadEntry[]>("entries", static _ => true).SetResult([new("a.txt", false, 10)]);
        var values = new List<bool>();

        var cut = Render<FileUpload>(parameters => parameters
            .Add(static p => p.Url, "/upload")
            .Add(static p => p.OnStarting, static e => e.Cancel = true)
            .Add(static p => p.UploadingChanged, values.Add));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        var start = await cut.InvokeAsync(() => cut.Instance.NotifyStarting(1, 1, 10));

        Assert.Null(start);
        Assert.Empty(values);
    }

    [Theory]
    [InlineData("/upload", "token")]
    [InlineData("https://other.example/upload", null)]
    public async Task AddsAntiforgeryTokenForSameOrigin(string address, string? expected)
    {
        Services.AddSingleton<AntiforgeryStateProvider>(new TokenStateProvider());
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.Setup<FileUploadEntry[]>("entries", static _ => true).SetResult([new("a.txt", false, 10)]);

        var cut = Render<FileUpload>(parameters => parameters
            .Add(static p => p.Url, address)
            .Add(static p => p.OnStarting, static e => e.Headers["X-Upload"] = "1"));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        var start = await cut.InvokeAsync(() => cut.Instance.NotifyStarting(1, 1, 10));

        var headers = Assert.IsType<Dictionary<string, string>>(start?[3]);
        Assert.Equal("1", headers["X-Upload"]);
        Assert.Equal(expected, headers.GetValueOrDefault("RequestVerificationToken"));
    }

    [Fact]
    public async Task KeepsAntiforgeryHeaderFromStarting()
    {
        Services.AddSingleton<AntiforgeryStateProvider>(new TokenStateProvider());
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.Setup<FileUploadEntry[]>("entries", static _ => true).SetResult([new("a.txt", false, 10)]);

        var cut = Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload")
            .Add(static p => p.OnStarting, static e => e.Headers["requestverificationtoken"] = "custom"));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        var start = await cut.InvokeAsync(() => cut.Instance.NotifyStarting(1, 1, 10));

        var headers = Assert.IsType<Dictionary<string, string>>(start?[3]);
        Assert.Equal("custom", Assert.Single(headers).Value);
    }

    [Fact]
    public async Task NotifiesProgress()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);
        FileUploadProgress? progress = null;

        var cut = Render<FileUpload>(parameters => parameters
            .Add(static p => p.Url, "/upload")
            .Add(static p => p.OnProgress, e => progress = e));

        await cut.InvokeAsync(() => cut.Instance.NotifyProgress(1, 3, 100, 300, "photos/a.jpg"));

        Assert.NotNull(progress);
        Assert.Equal((1, 3, 100L, 300L), (progress.CompletedFiles, progress.TotalFiles, progress.LoadedBytes, progress.TotalBytes));
        Assert.Equal("photos/a.jpg", progress.CurrentPath);
    }

    [Fact]
    public async Task CompletedReadsErrorsAndNotifiesUploading()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.Setup<FileUploadEntry[]>("entries", static _ => true).SetResult([new("a.txt", false, 10)]);
        zone.Setup<FileUploadError[]>("errors", static _ => true).SetResult([new("photos/b.jpg", 413)]);
        FileUploadResult? result = null;
        var values = new List<bool>();

        var cut = Render<FileUpload>(parameters => parameters
            .Add(static p => p.Url, "/upload")
            .Add(static p => p.UploadingChanged, values.Add)
            .Add(static p => p.OnCompleted, e => result = e));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));
        await cut.InvokeAsync(() => cut.Instance.NotifyStarting(1, 3, 30));
        cut.Render(static parameters => parameters.Add(static p => p.Uploading, true));

        await cut.InvokeAsync(() => cut.Instance.NotifyCompleted(2, 1, false, 1));

        Assert.NotNull(result);
        Assert.Equal((2, 1, false), (result.Succeeded, result.Failed, result.Canceled));
        var error = Assert.Single(result.Errors);
        Assert.Equal(("photos/b.jpg", 413), (error.Path, error.Status));
        bool[] expected = [true, false];
        Assert.Equal(expected, values);
    }

    [Fact]
    public async Task OpensPickers()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("open", static _ => true).SetVoidResult();

        var cut = Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload"));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        await cut.InvokeAsync(() => cut.Instance.OpenFilePickerAsync().AsTask());
        await cut.InvokeAsync(() => cut.Instance.OpenFolderPickerAsync().AsTask());

        Assert.Equal("False,True", string.Join(',', zone.VerifyInvoke("open", 2).Select(static x => x.Arguments[0])));
    }

    [Fact]
    public async Task CancelsUpload()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("cancel").SetVoidResult();

        var cut = Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload"));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        await cut.InvokeAsync(() => cut.Instance.CancelAsync().AsTask());

        zone.VerifyInvoke("cancel");
    }

    [Fact]
    public async Task DisposeReleasesZone()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("dispose").SetVoidResult();

        var cut = Render<FileUpload>(static parameters => parameters
            .Add(static p => p.Url, "/upload"));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        await DisposeComponentsAsync();

        zone.VerifyInvoke("dispose");
    }

    private sealed class TokenStateProvider : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken GetAntiforgeryToken() => new("token", "__RequestVerificationToken");
    }
}
