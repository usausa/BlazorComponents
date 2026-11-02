namespace BlazorComponents;

public interface IClipboard
{
    ValueTask<bool> SetTextAsync(string text);
}
