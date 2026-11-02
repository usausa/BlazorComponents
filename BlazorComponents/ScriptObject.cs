namespace BlazorComponents;

using System.Diagnostics.CodeAnalysis;

using Microsoft.JSInterop;

internal sealed class ScriptObject<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)] TOwner> : IAsyncDisposable
    where TOwner : class
{
    private readonly ScriptModule module;

    private readonly TOwner owner;

    private DotNetObjectReference<TOwner>? reference;

    private Task<IJSObjectReference>? creating;

    private bool disposed;

    public ScriptObject(IJSRuntime runtime, string path, TOwner owner)
    {
        module = new ScriptModule(runtime, path);
        this.owner = owner;
    }

    public Task<IJSObjectReference> GetAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, owner);
        return creating ??= CreateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;

        if (creating is { IsCompletedSuccessfully: true })
        {
            var created = await creating.ConfigureAwait(true);
            await created.ReleaseAsync("dispose").ConfigureAwait(true);
        }

        reference?.Dispose();
        await module.DisposeAsync().ConfigureAwait(true);
    }

    private async Task<IJSObjectReference> CreateAsync()
    {
        reference = DotNetObjectReference.Create(owner);
        var created = await module.InvokeAsync<IJSObjectReference>("create", reference).ConfigureAwait(true);
        if (disposed)
        {
            await created.ReleaseAsync("dispose").ConfigureAwait(true);
        }

        ObjectDisposedException.ThrowIf(disposed, owner);
        return created;
    }
}
