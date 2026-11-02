namespace BlazorComponents;

using System.ComponentModel;

public sealed class FileUploadStartingEventArgs : CancelEventArgs
{
    public FileUploadStartingEventArgs(IReadOnlyList<FileUploadEntry> entries, int fileCount, long totalBytes)
    {
        Entries = entries;
        FileCount = fileCount;
        TotalBytes = totalBytes;
    }

    public IReadOnlyList<FileUploadEntry> Entries { get; }

    public int FileCount { get; }

    public long TotalBytes { get; }

    public IDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
