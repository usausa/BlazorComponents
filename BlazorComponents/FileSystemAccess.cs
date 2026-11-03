namespace BlazorComponents;

using Microsoft.JSInterop;

internal sealed class FileSystemAccess : IFileSystemAccess, IAsyncDisposable
{
    private const string ModulePath = "./_content/Components.Blazor/file-system-access.js";

    private const int PickerCanceled = 0;

    private const int PickerUnavailable = -1;

    private readonly ScriptModule module;

    private bool disposed;

    public FileSystemAccess(IJSRuntime jsRuntime)
    {
        module = new ScriptModule(jsRuntime, ModulePath);
    }

    public async ValueTask<FileSaveResult> SaveAsync(string fileName, string contentType, byte[] bytes, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream(bytes, false);
        return await SaveAsync(fileName, contentType, stream, cancellationToken).ConfigureAwait(true);
    }

    public async ValueTask<FileSaveResult> SaveAsync(string fileName, string contentType, Stream stream, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var handle = await module.InvokeAsync<int>("pick", cancellationToken, fileName, contentType).ConfigureAwait(true);
        if (handle == PickerCanceled)
        {
            return FileSaveResult.Canceled;
        }

        using var streamReference = new DotNetStreamReference(stream, leaveOpen: true);
        if (handle == PickerUnavailable)
        {
            await module.InvokeVoidAsync("download", cancellationToken, fileName, contentType, streamReference).ConfigureAwait(true);
            return FileSaveResult.Downloaded;
        }

        await module.InvokeVoidAsync("write", cancellationToken, handle, streamReference).ConfigureAwait(true);
        return FileSaveResult.Saved;
    }

    public async ValueTask<FileSaveResult> SaveAsync(string fileName, string contentType, Uri url, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var result = await module.InvokeAsync<string>("saveUrl", cancellationToken, fileName, contentType, url.OriginalString).ConfigureAwait(true);
        return result switch
        {
            "saved" => FileSaveResult.Saved,
            "canceled" => FileSaveResult.Canceled,
            _ => FileSaveResult.Downloaded
        };
    }

    public ValueTask DisposeAsync()
    {
        disposed = true;
        return module.DisposeAsync();
    }
}
