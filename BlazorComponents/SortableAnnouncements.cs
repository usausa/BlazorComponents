namespace BlazorComponents;

public sealed record SortableAnnouncements
{
    public static SortableAnnouncements Default { get; } = new();

    public string Grabbed { get; init; } = "Grabbed item {0} of {1}. Use the arrow keys to move it, Space or Enter to drop it, or Escape to cancel.";

    public string Moved { get; init; } = "Moved to position {0} of {1}.";

    public string Dropped { get; init; } = "Dropped at position {0} of {1}.";

    public string Canceled { get; init; } = "Move canceled.";
}
