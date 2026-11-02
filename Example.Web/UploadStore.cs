namespace Example.Web;

using System.Collections.Concurrent;

internal sealed class UploadStore
{
    private const int Limit = 100;

    private readonly ConcurrentQueue<UploadEntry> entries = new();

    public IReadOnlyList<UploadEntry> Entries => [.. entries.Reverse()];

    public void Add(UploadEntry entry)
    {
        entries.Enqueue(entry);
        while (entries.Count > Limit)
        {
            entries.TryDequeue(out _);
        }
    }

    public void Clear() => entries.Clear();
}
