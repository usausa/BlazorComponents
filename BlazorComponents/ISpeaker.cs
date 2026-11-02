namespace BlazorComponents;

public interface ISpeaker
{
    event EventHandler? ReadyChanged;

    event EventHandler? SpeakingChanged;

    bool IsReady { get; }

    bool IsSpeaking { get; }

    ValueTask PrepareAsync();

    ValueTask<bool> PlayAsync(params IEnumerable<Tone> tones);

    ValueTask<bool> SpeakAsync(string text, SpeechOptions? options = null);

    ValueTask CancelAsync();

    ValueTask<IReadOnlyList<SpeechVoice>> GetVoicesAsync();
}
