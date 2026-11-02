namespace BlazorComponents;

using System.Diagnostics.CodeAnalysis;

using Microsoft.JSInterop;

internal sealed class Speaker : ISpeaker, IAsyncDisposable
{
    private const string ModulePath = "./_content/Components.Blazor/speaker.js";

    private static readonly SpeechOptions DefaultOptions = new();

    private readonly ScriptObject<Speaker> script;

    private int speaking;

    private bool disposed;

    public Speaker(IJSRuntime jsRuntime)
    {
        script = new ScriptObject<Speaker>(jsRuntime, ModulePath, this);
    }

    public event EventHandler? ReadyChanged;

    public event EventHandler? SpeakingChanged;

    public bool IsReady { get; private set; }

    public bool IsSpeaking { get; private set; }

    public async ValueTask PrepareAsync()
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        NotifyReady(await controller.InvokeAsync<bool>("ready").ConfigureAwait(true));
    }

    public async ValueTask<bool> PlayAsync(params IEnumerable<Tone> tones)
    {
        int[][] values = [.. tones.Select(static int[] (t) => [t.Frequency, t.Duration, t.Delay])];
        var controller = await script.GetAsync().ConfigureAwait(true);
        return await controller.InvokeAsync<bool>("play", CancellationToken.None, [values]).ConfigureAwait(true);
    }

    public async ValueTask<bool> SpeakAsync(string text, SpeechOptions? options = null)
    {
        var settings = options ?? DefaultOptions;
        var controller = await script.GetAsync().ConfigureAwait(true);

        speaking++;
        UpdateSpeaking();
        try
        {
            return await controller.InvokeAsync<bool>("speak", CancellationToken.None, [text, settings.Lang, settings.Voice, settings.Rate, settings.Pitch, settings.Volume]).ConfigureAwait(true);
        }
        finally
        {
            speaking--;
            UpdateSpeaking();
        }
    }

    public async ValueTask CancelAsync()
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        await controller.InvokeVoidAsync("cancel").ConfigureAwait(true);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(SpeechVoice))]
    public async ValueTask<IReadOnlyList<SpeechVoice>> GetVoicesAsync()
    {
        var controller = await script.GetAsync().ConfigureAwait(true);
        return await controller.InvokeAsync<SpeechVoice[]>("voices").ConfigureAwait(true);
    }

    [JSInvokable]
    public void NotifyReady(bool value)
    {
        if (disposed || (value == IsReady))
        {
            return;
        }

        IsReady = value;
        ReadyChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await script.DisposeAsync().ConfigureAwait(true);
    }

    private void UpdateSpeaking()
    {
        var value = speaking > 0;
        if (disposed || (value == IsSpeaking))
        {
            return;
        }

        IsSpeaking = value;
        SpeakingChanged?.Invoke(this, EventArgs.Empty);
    }
}
