namespace BlazorComponents;

public sealed class SpeakerTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/speaker.js";

    public SpeakerTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task PrepareTakesReadyFromResult()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("ready").SetResult(true);
        var speaker = new Speaker(JSInterop.JSRuntime);
        var raised = 0;
        speaker.ReadyChanged += (_, _) => raised++;

        await speaker.PrepareAsync();
        await speaker.PrepareAsync();

        Assert.True(speaker.IsReady);
        Assert.Equal(1, raised);
        module.VerifyInvoke("create");
        await speaker.DisposeAsync();
    }

    [Fact]
    public async Task NotifiesReadyOnlyWhenChanged()
    {
        var speaker = new Speaker(JSInterop.JSRuntime);
        var values = new List<bool>();
        speaker.ReadyChanged += (_, _) => values.Add(speaker.IsReady);

        speaker.NotifyReady(false);
        speaker.NotifyReady(true);
        speaker.NotifyReady(true);
        speaker.NotifyReady(false);

        bool[] expected = [true, false];
        Assert.Equal(expected, values);
        await speaker.DisposeAsync();
    }

    [Fact]
    public async Task PlaysTones()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("play", static _ => true).SetResult(true);
        var speaker = new Speaker(JSInterop.JSRuntime);

        var result = await speaker.PlayAsync(new Tone(880, 200), new Tone(1175, 200, 50));

        Assert.True(result);
        int[][] expected = [[880, 200, 0], [1175, 200, 50]];
        Assert.Equal(expected, Assert.IsType<int[][]>(controller.VerifyInvoke("play").Arguments[0]));
        await speaker.DisposeAsync();
    }

    [Fact]
    public async Task SpeaksTextWithOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("speak", static _ => true).SetResult(true);
        var speaker = new Speaker(JSInterop.JSRuntime);

        var result = await speaker.SpeakAsync("新しい注文です。", new SpeechOptions { Lang = "ja-JP", Voice = "Haruka", Rate = 1.5, Pitch = 0.8, Volume = 0.5 });
        await speaker.SpeakAsync("text");

        Assert.True(result);
        var invocations = controller.VerifyInvoke("speak", 2);
        object?[] expected = ["新しい注文です。", "ja-JP", "Haruka", 1.5, 0.8, 0.5];
        Assert.Equal(expected, invocations[0].Arguments);
        object?[] defaults = ["text", null, null, 1.0, 1.0, 1.0];
        Assert.Equal(defaults, invocations[1].Arguments);
        await speaker.DisposeAsync();
    }

    [Fact]
    public async Task NotifiesSpeakingUntilFinished()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("ready").SetResult(true);
        var speak = controller.Setup<bool>("speak", static _ => true);
        var speaker = new Speaker(JSInterop.JSRuntime);
        var values = new List<bool>();
        speaker.SpeakingChanged += (_, _) => values.Add(speaker.IsSpeaking);
        await speaker.PrepareAsync();

        var task = speaker.SpeakAsync("text").AsTask();
        Assert.True(speaker.IsSpeaking);
        speak.SetResult(false);
        var result = await task.ConfigureAwait(true);

        Assert.False(result);
        bool[] expected = [true, false];
        Assert.Equal(expected, values);
        await speaker.DisposeAsync();
    }

    [Fact]
    public async Task CancelsOutput()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.SetupVoid("cancel").SetVoidResult();
        var speaker = new Speaker(JSInterop.JSRuntime);

        await speaker.CancelAsync();

        controller.VerifyInvoke("cancel");
        await speaker.DisposeAsync();
    }

    [Fact]
    public async Task ReturnsVoices()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        SpeechVoice[] voices = [new("Microsoft Haruka", "ja-JP", true, true), new("Google 日本語", "ja-JP", false, false)];
        controller.Setup<SpeechVoice[]>("voices").SetResult(voices);
        var speaker = new Speaker(JSInterop.JSRuntime);

        var result = await speaker.GetVoicesAsync();

        Assert.Collection(
            result,
            static voice => Assert.Equal(("Microsoft Haruka", "ja-JP", true, true), (voice.Name, voice.Lang, voice.LocalService, voice.Default)),
            static voice => Assert.Equal(("Google 日本語", "ja-JP", false, false), (voice.Name, voice.Lang, voice.LocalService, voice.Default)));
        await speaker.DisposeAsync();
    }

    [Fact]
    public async Task DisposeReleasesController()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var controller = module.SetupModule("create", static _ => true);
        controller.Setup<bool>("ready").SetResult(true);
        controller.SetupVoid("dispose").SetVoidResult();
        var speaker = new Speaker(JSInterop.JSRuntime);
        await speaker.PrepareAsync();
        var raised = 0;
        speaker.ReadyChanged += (_, _) => raised++;

        await speaker.DisposeAsync();
        speaker.NotifyReady(false);

        controller.VerifyInvoke("dispose");
        Assert.Equal(0, raised);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => speaker.PlayAsync(new Tone(880, 200)).AsTask());
    }
}
