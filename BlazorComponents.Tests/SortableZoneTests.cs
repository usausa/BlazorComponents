namespace BlazorComponents;

public sealed class SortableZoneTests : BunitContext
{
    private const string ModulePath = "./_content/Components.Blazor/sortable-zone.js";

    public SortableZoneTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void RendersDivWithAttributesAndContent()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        var cut = Render<SortableZone>(static parameters => parameters
            .AddUnmatched("class", "list")
            .AddChildContent("<div>A</div><div>B</div>"));

        var zone = cut.Find("div");
        Assert.Equal("list", zone.GetAttribute("class"));
        Assert.Equal(2, zone.Children.Length);
    }

    [Fact]
    public void AttachesWithDefaultOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        Render<SortableZone>();

        var arguments = module.VerifyInvoke("attach").Arguments;
        Assert.Matches("^[0-9a-f]{32}$", Assert.IsType<string>(arguments[2]));
        Assert.Equal(",True,False,False,,,250,150,,,", string.Join(',', arguments.Skip(3).Take(11)));
        var announcements = SortableAnnouncements.Default;
        object?[] expected = [announcements.Grabbed, announcements.Moved, announcements.Dropped, announcements.Canceled];
        Assert.Equal(expected, arguments.Skip(14));
    }

    [Fact]
    public void AttachesWithOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);

        Render<SortableZone>(static parameters => parameters
            .Add(static p => p.Name, "todo")
            .Add(static p => p.Group, "board")
            .Add(static p => p.Sort, false)
            .Add(static p => p.Disabled, true)
            .Add(static p => p.Orientation, SortableOrientation.Horizontal)
            .Add(static p => p.Handle, ".handle")
            .Add(static p => p.Filter, ".fixed")
            .Add(static p => p.TouchDelay, 0)
            .Add(static p => p.Animation, 0)
            .Add(static p => p.GhostClass, "ghost")
            .Add(static p => p.PlaceholderClass, "slot")
            .Add(static p => p.OverClass, "over")
            .Add(static p => p.Announcements, new SortableAnnouncements { Grabbed = "g", Moved = "m", Dropped = "d", Canceled = "c" }));

        var arguments = module.VerifyInvoke("attach").Arguments;
        Assert.Equal("todo,board,False,True,True,.handle,.fixed,0,0,ghost,slot,over,g,m,d,c", string.Join(',', arguments.Skip(2)));
    }

    [Fact]
    public void UpdatesOptions()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("update", static _ => true).SetVoidResult();

        var cut = Render<SortableZone>();
        cut.WaitForAssertion(() => module.VerifyInvoke("attach"));

        cut.Render(static parameters => parameters.Add(static p => p.Disabled, true));

        cut.WaitForAssertion(() => zone.VerifyInvoke("update"));
        var update = zone.VerifyInvoke("update").Arguments;
        Assert.Equal(module.VerifyInvoke("attach").Arguments[2], update[0]);
        Assert.True(Assert.IsType<bool>(update[3]));
        Assert.Single(module.Invocations["attach"]);
    }

    [Fact]
    public void DoesNotUpdateWithoutChanges()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("update", static _ => true).SetVoidResult();

        var cut = Render<SortableZone>(static parameters => parameters.Add(static p => p.Name, "list"));
        cut.WaitForAssertion(() => module.VerifyInvoke("attach"));

        cut.Render(static parameters => parameters.Add(static p => p.Name, "list"));

        Assert.Empty(zone.Invocations["update"]);
    }

    [Fact]
    public async Task NotifyMoveRaisesOnMove()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);
        SortableMoveEventArgs? moved = null;

        var cut = Render<SortableZone>(parameters => parameters
            .Add(static p => p.Name, "doing")
            .Add(static p => p.OnMove, e => moved = e));

        await cut.InvokeAsync(() => cut.Instance.NotifyMove("todo", 1, 2));

        Assert.NotNull(moved);
        Assert.Equal(("todo", 1, "doing", 2), (moved.From, moved.OldIndex, moved.To, moved.NewIndex));
    }

    [Fact]
    public async Task NotifyMoveUsesGeneratedName()
    {
        var module = JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", static _ => true);
        SortableMoveEventArgs? moved = null;

        var cut = Render<SortableZone>(parameters => parameters
            .Add(static p => p.OnMove, e => moved = e));
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));
        var name = Assert.IsType<string>(module.VerifyInvoke("attach").Arguments[2]);

        await cut.InvokeAsync(() => cut.Instance.NotifyMove(name, 0, 1));

        Assert.NotNull(moved);
        Assert.Equal((name, name), (moved.From, moved.To));
    }

    [Fact]
    public async Task DisposeReleasesZone()
    {
        var module = JSInterop.SetupModule(ModulePath);
        var zone = module.SetupModule("attach", static _ => true);
        zone.SetupVoid("dispose").SetVoidResult();

        var cut = Render<SortableZone>();
        await cut.WaitForAssertionAsync(() => module.VerifyInvoke("attach"));

        await DisposeComponentsAsync();

        zone.VerifyInvoke("dispose");
    }
}
