namespace BlazorComponents;

public interface IScreen
{
    event EventHandler? ScreenKeptOnChanged;

    bool IsScreenKeptOn { get; }

    ValueTask KeepScreenOnAsync(bool value);

    ValueTask KeepFullscreenAsync(bool value);
}
