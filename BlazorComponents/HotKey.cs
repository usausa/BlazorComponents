namespace BlazorComponents;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

public sealed class HotKey : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/BlazorComponents/hot-key.js";

    private ScriptModule? module;

    private DotNetObjectReference<HotKey>? reference;

    private IJSObjectReference? registration;

    private Options current;

    private Options registered;

    private bool disposed;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public string? Key { get; set; }

    [Parameter]
    public string? Code { get; set; }

    [Parameter]
    public EventCallback OnPressed { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool PreventDefault { get; set; } = true;

    [Parameter]
    public bool AllowInInput { get; set; }

    [Parameter]
    public bool AllowRepeat { get; set; }

    protected override void OnParametersSet()
    {
        current = Options.Create(Code ?? Key ?? string.Empty, Code is not null, PreventDefault, AllowInInput, AllowRepeat, Disabled);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            module = new ScriptModule(JSRuntime, ModulePath);
            reference = DotNetObjectReference.Create(this);
            registered = current;

            IJSObjectReference created;
            try
            {
                created = await module.InvokeAsync<IJSObjectReference>("register", [reference, .. registered.ToArguments()]).ConfigureAwait(true);
            }
            catch (ObjectDisposedException) when (disposed)
            {
                return;
            }

            if (disposed)
            {
                await created.ReleaseAsync("dispose").ConfigureAwait(true);
                return;
            }

            registration = created;
        }

        if ((registration is null) || disposed || (current == registered))
        {
            return;
        }

        registered = current;
        await registration.InvokeVoidAsync("update", registered.ToArguments()).ConfigureAwait(true);
    }

    [JSInvokable]
    public Task NotifyPressed() =>
        disposed ? Task.CompletedTask : OnPressed.InvokeAsync();

    public async ValueTask DisposeAsync()
    {
        disposed = true;

        if (registration is not null)
        {
            await registration.ReleaseAsync("dispose").ConfigureAwait(true);
            registration = null;
        }

        reference?.Dispose();

        if (module is not null)
        {
            await module.DisposeAsync().ConfigureAwait(true);
        }
    }

    private readonly record struct Options(
        string Key,
        bool ByCode,
        bool Ctrl,
        bool Alt,
        bool? Shift,
        bool Meta,
        bool PreventDefault,
        bool AllowInInput,
        bool AllowRepeat,
        bool Disabled)
    {
        public static Options Create(string value, bool byCode, bool preventDefault, bool allowInInput, bool allowRepeat, bool disabled)
        {
            var parts = value.Split('+', StringSplitOptions.TrimEntries);
            var ctrl = false;
            var alt = false;
            var shift = false;
            var meta = false;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].ToUpperInvariant())
                {
                    case "CTRL":
                        ctrl = true;
                        break;
                    case "ALT":
                        alt = true;
                        break;
                    case "SHIFT":
                        shift = true;
                        break;
                    case "META":
                        meta = true;
                        break;
                    default:
                        throw new ArgumentException($"Unsupported key combination. value=[{value}]", nameof(value));
                }
            }

            var key = parts[^1];
            if (key.Length == 0)
            {
                throw new ArgumentException($"Unsupported key combination. value=[{value}]", nameof(value));
            }

            key = key.ToUpperInvariant();
            if (!byCode)
            {
                key = key switch
                {
                    "SPACE" => " ",
                    "PLUS" => "+",
                    _ => key
                };
            }

            var symbol = !byCode && (key.Length == 1) && !Char.IsLetterOrDigit(key[0]) && !Char.IsWhiteSpace(key[0]);
            return new Options(key, byCode, ctrl, alt, symbol ? null : shift, meta, preventDefault, allowInInput, allowRepeat, disabled);
        }

        public object?[] ToArguments() =>
            [Key, ByCode, Ctrl, Alt, Shift, Meta, PreventDefault, AllowInInput, AllowRepeat, Disabled];
    }
}
