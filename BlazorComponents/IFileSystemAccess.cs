namespace BlazorComponents;

public interface IFileSystemAccess
{
    ValueTask<FileSaveResult> SaveAsync(string fileName, string contentType, byte[] bytes, CancellationToken cancellationToken = default);

    ValueTask<FileSaveResult> SaveAsync(string fileName, string contentType, Stream stream, CancellationToken cancellationToken = default);

    ValueTask<FileSaveResult> SaveAsync(string fileName, string contentType, Uri url, CancellationToken cancellationToken = default);
}
