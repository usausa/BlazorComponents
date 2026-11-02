namespace BlazorComponents;

public sealed class SpeechSynthesizerTests : BunitContext
{
    private const string ModulePath = "./_content/BlazorComponents/speech-synthesizer.js";

    public SpeechSynthesizerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void CreatesSynthesizerOnFirstRender()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("create", static _ => true);

        var cut = Render<SpeechSynthesizer>();

        cut.WaitForAssertion(() => module.VerifyInvoke("create"));
        Assert.Empty(cut.Markup);
    }

    [Fact]
    public async Task SpeaksTextWithParameters()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var synthesizer = module.SetupModule("create", static _ => true);
        synthesizer.Setup<bool>("speak", static _ => true).SetResult(true);

        var cut = Render<SpeechSynthesizer>(static parameters => parameters
            .Add(static p => p.Lang, "ja-JP")
            .Add(static p => p.Voice, "Haruka")
            .Add(static p => p.Rate, 1.5)
            .Add(static p => p.Pitch, 0.8)
            .Add(static p => p.Volume, 0.5));

        var result = await cut.InvokeAsync(() => cut.Instance.SpeakAsync("新しい注文です。").AsTask());

        Assert.True(result);
        object?[] expected = ["新しい注文です。", "ja-JP", "Haruka", 1.5, 0.8, 0.5];
        Assert.Equal(expected, synthesizer.VerifyInvoke("speak").Arguments);
    }

    [Fact]
    public async Task NotifiesSpeakingUntilFinished()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var synthesizer = module.SetupModule("create", static _ => true);
        var speak = synthesizer.Setup<bool>("speak", static _ => true);
        var values = new List<bool>();

        var cut = Render<SpeechSynthesizer>(parameters => parameters
            .Add(static p => p.SpeakingChanged, values.Add));

        var task = cut.InvokeAsync(() => cut.Instance.SpeakAsync("text").AsTask());
        await cut.WaitForAssertionAsync(() => Assert.True(Assert.Single(values)));
        cut.Render(static parameters => parameters.Add(static p => p.Speaking, true));
        speak.SetResult(false);
        var result = await task.ConfigureAwait(true);

        Assert.False(result);
        bool[] expected = [true, false];
        Assert.Equal(expected, values);
    }

    [Fact]
    public async Task CancelsSpeech()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var synthesizer = module.SetupModule("create", static _ => true);
        synthesizer.SetupVoid("cancel").SetVoidResult();

        var cut = Render<SpeechSynthesizer>();
        await cut.InvokeAsync(() => cut.Instance.CancelAsync().AsTask());

        synthesizer.VerifyInvoke("cancel");
    }

    [Fact]
    public async Task ReturnsVoices()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var synthesizer = module.SetupModule("create", static _ => true);
        SpeechVoice[] voices = [new("Microsoft Haruka", "ja-JP", true, true), new("Google 日本語", "ja-JP", false, false)];
        synthesizer.Setup<SpeechVoice[]>("voices").SetResult(voices);

        var cut = Render<SpeechSynthesizer>();
        var result = await cut.InvokeAsync(() => cut.Instance.GetVoicesAsync().AsTask());

        Assert.Collection(
            result,
            static voice => Assert.Equal(("Microsoft Haruka", "ja-JP", true, true), (voice.Name, voice.Lang, voice.LocalService, voice.Default)),
            static voice => Assert.Equal(("Google 日本語", "ja-JP", false, false), (voice.Name, voice.Lang, voice.LocalService, voice.Default)));
    }

    [Fact]
    public async Task DisposeReleasesSynthesizer()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var synthesizer = module.SetupModule("create", static _ => true);
        synthesizer.SetupVoid("cancel").SetVoidResult();
        synthesizer.SetupVoid("dispose").SetVoidResult();

        var cut = Render<SpeechSynthesizer>();
        await cut.InvokeAsync(() => cut.Instance.CancelAsync().AsTask());

        await DisposeComponentsAsync();

        synthesizer.VerifyInvoke("dispose");
    }
}
