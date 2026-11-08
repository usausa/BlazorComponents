namespace BlazorComponents;

public sealed class SortableMoveEventArgs : EventArgs
{
    public string From { get; }

    public int OldIndex { get; }

    public string To { get; }

    public int NewIndex { get; }

    public SortableMoveEventArgs(string from, int oldIndex, string to, int newIndex)
    {
        From = from;
        OldIndex = oldIndex;
        To = to;
        NewIndex = newIndex;
    }
}
