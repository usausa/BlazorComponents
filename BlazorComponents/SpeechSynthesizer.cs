namespace BlazorComponents;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed class SpeechSynthesizer : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/BlazorComponents/speech-synthesizer.js";

    private ScriptModule? module;

    private Task<IJSObjectReference>? creating;

    private int speaking;

    private bool disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string? Lang { get; set; }

    [Parameter]
    public string? Voice { get; set; }

    [Parameter]
    public double Rate { get; set; } = 1;

    [Parameter]
    public double Pitch { get; set; } = 1;

    [Parameter]
    public double Volume { get; set; } = 1;

    [Parameter]
    public bool Speaking { get; set; }

    [Parameter]
    public EventCallback<bool> SpeakingChanged { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await GetSynthesizerAsync().ConfigureAwait(true);
            }
            catch (ObjectDisposedException) when (disposed)
            {
            }
        }
    }

    public async ValueTask<bool> SpeakAsync(string text)
    {
        var synthesizer = await GetSynthesizerAsync().ConfigureAwait(true);

        speaking++;
        await NotifySpeakingAsync().ConfigureAwait(true);
        try
        {
            return await synthesizer.InvokeAsync<bool>("speak", CancellationToken.None, [text, Lang, Voice, Rate, Pitch, Volume]).ConfigureAwait(true);
        }
        finally
        {
            speaking--;
            await NotifySpeakingAsync().ConfigureAwait(true);
        }
    }

    public async ValueTask CancelAsync()
    {
        var synthesizer = await GetSynthesizerAsync().ConfigureAwait(true);
        await synthesizer.InvokeVoidAsync("cancel").ConfigureAwait(true);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties, typeof(SpeechVoice))]
    public async ValueTask<IReadOnlyList<SpeechVoice>> GetVoicesAsync()
    {
        var synthesizer = await GetSynthesizerAsync().ConfigureAwait(true);
        return await synthesizer.InvokeAsync<SpeechVoice[]>("voices").ConfigureAwait(true);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;

        if (creating is { IsCompletedSuccessfully: true })
        {
            var synthesizer = await creating.ConfigureAwait(true);
            await synthesizer.ReleaseAsync("dispose").ConfigureAwait(true);
        }

        if (module is not null)
        {
            await module.DisposeAsync().ConfigureAwait(true);
        }
    }

    private Task<IJSObjectReference> GetSynthesizerAsync() =>
        creating ??= CreateAsync();

    private async Task<IJSObjectReference> CreateAsync()
    {
        module = new ScriptModule(JSRuntime, ModulePath);
        var created = await module.InvokeAsync<IJSObjectReference>("create").ConfigureAwait(true);
        if (disposed)
        {
            await created.ReleaseAsync("dispose").ConfigureAwait(true);
        }

        ObjectDisposedException.ThrowIf(disposed, this);
        return created;
    }

    private Task NotifySpeakingAsync()
    {
        var value = speaking > 0;
        return (disposed || (value == Speaking)) ? Task.CompletedTask : SpeakingChanged.InvokeAsync(value);
    }
}
