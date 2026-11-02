namespace BlazorComponents;

public sealed record SpeechOptions
{
    public string? Lang { get; init; }

    public string? Voice { get; init; }

    public double Rate { get; init; } = 1;

    public double Pitch { get; init; } = 1;

    public double Volume { get; init; } = 1;
}
