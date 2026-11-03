# BlazorComponents

| Package | Info |
|:-|:-|
| Components.Blazor | [![NuGet](https://img.shields.io/nuget/v/Components.Blazor.svg)](https://www.nuget.org/packages/Components.Blazor) |

Component library for Blazor applications.

## Features

* Script helper extensions for JavaScript interop
* `ScriptModule` — load a JavaScript module on the first call
* `MediaQuery` — bind the result of a CSS media query
* `HotKey` — call a method on a keyboard shortcut
* `NativeDialog` — open and close a native `<dialog>` by binding
* `FileDropZone` — pass dropped or pasted files to the `<InputFile>` inside it
* `FileUpload` — upload dropped, pasted or picked files and folders to a URL from the browser
* `IScreen` — keep the screen on and keep the page fullscreen from code
* `ISpeaker` — play tones and read text aloud from code
* `IBrowserTabs` — run actions one tab at a time and send messages to the other tabs
* `IClipboard` — copy text to the clipboard from code
* `IGeolocation` — get the current position of the device from code
* `IFileSystemAccess` — save a file where the user picks, or download it, from code

## Usage

### Script helper extensions

```html
<script src="_content/Components.Blazor/script.min.js"></script>
```

```csharp
await JSRuntime.SetFocus("name");
await JSRuntime.SaveAsFile("report.pdf", "application/pdf", bytes);
await JSRuntime.OpenNewWindow("application/pdf", bytes);
await JSRuntime.HistoryBack();
```

### ScriptModule

```csharp
module = new ScriptModule(JSRuntime, "./js/chart.js");
await module.InvokeVoidAsync("draw", element, values);
var answer = await module.InvokeAsync<string>("ask", cancellationToken, question);
await module.DisposeAsync();
```

As with `IJSObjectReference`, a call with a `CancellationToken` waits without the default timeout of JavaScript calls.

### MediaQuery

```razor
<MediaQuery Query="(max-width: 600px)" @bind-Matches="narrow" />
```

### HotKey

```razor
<HotKey Key="Ctrl+S" AllowInInput="true" OnPressed="SaveAsync" />
<HotKey Key="F2" Disabled="busy" OnPressed="Edit" />
<HotKey Key="?" OnPressed="ShowHelp" />
<HotKey Code="Shift+Digit1" OnPressed="SelectFirst" />
```

`Key` is modifiers (`Ctrl`, `Alt`, `Shift`, `Meta`) and a `KeyboardEvent.key` name joined by `+`. Write `Space` and `Plus` for the space and `+` keys. For a symbol such as `?` or `@`, `Shift` is not compared, so the same `Key` works on any keyboard layout.

`Code` is the same form with a `KeyboardEvent.code` name, and matches the physical key. Use it for `Shift` with a digit, or for `Alt` with a letter on macOS. When both are set, `Code` is used.

`Disabled` stops the shortcut without removing it.

### NativeDialog

```razor
<NativeDialog @bind-Open="open" OnClose="OnClose" class="confirm">
    <form method="dialog">
        <p>Delete?</p>
        <button value="ok">OK</button>
        <button value="cancel">Cancel</button>
    </form>
</NativeDialog>
```

`OnClose` receives the `value` of the button that closed the dialog.

### FileDropZone

```razor
<FileDropZone @ref="zone" class="drop" Paste="true">
    <InputFile OnChange="OnChange" accept=".zip,.csv" multiple />
</FileDropZone>
<button @onclick="() => zone.OpenFilePickerAsync()">Select</button>
```

While files are dragged over it, the zone has the `DragOverClass` class (`drag-over` by default). Dropped files are passed to the `<input type="file">` inside the zone. Files that do not match `accept` and folders are skipped, only the first file is passed when `multiple` is not set, and nothing is passed when the input is `disabled`. With `Paste`, files pasted while the focus is in the zone are passed too; add `tabindex="0"` to let the zone itself take the focus.

### FileUpload

```razor
<FileUpload @ref="upload" class="drop" Url="@($"api/upload/{folder}")" AllowFolders="true" @bind-Uploading="uploading"
            OnStarting="OnStarting" OnProgress="OnProgress" OnCompleted="OnCompleted">
    ...
</FileUpload>
<button @onclick="() => upload.OpenFilePickerAsync()">Files</button>
<button @onclick="() => upload.OpenFolderPickerAsync()">Folder</button>
<button @onclick="() => upload.CancelAsync()" disabled="@(!uploading)">Cancel</button>
```

```csharp
private async Task OnStarting(FileUploadStartingEventArgs e)
{
    if (e.Entries.Any(x => existingNames.Contains(x.Name)))
    {
        e.Cancel = !await ConfirmOverwriteAsync();
    }
}
```

Files dropped on the zone, pasted with `Paste`, or picked with `OpenFilePickerAsync()` and `OpenFolderPickerAsync()` are posted to `Url` from the browser, one file per `multipart/form-data` request and up to `Concurrency` (4) requests at a time. Each request has `relativePath`, the path relative to the dropped or picked folder (the file name for a file), followed by the file in `FieldName` (`files`). The `Url` at the start is used until the end. Folders are taken only with `AllowFolders`; `Accept`, `Multiple`, `Disabled` and `DragOverClass` work as in `FileDropZone`. Drops while uploading are ignored, and disposing the component aborts the upload.

`OnStarting` receives the top-level files and folders (`Entries`), `FileCount` and `TotalBytes`; set `Cancel` to stop, or add `Headers`. When `Url` is on the same origin, the antiforgery token is sent in the `RequestVerificationToken` header, so the endpoint can keep antiforgery validation. `OnProgress` is raised every 200 ms with the counts, the bytes and `CurrentPath`, the path of the file started last. `OnCompleted` receives the counts and the first 100 failures with their HTTP status (`0` when no response was received).

The endpoint is up to the application. Check `relativePath` before using it:

```csharp
app.MapPost("/api/upload/{**folder}", async (string? folder, [FromForm] string relativePath, IFormFile files) =>
{
    var target = Path.GetFullPath(Path.Combine(root, folder ?? string.Empty, relativePath));
    if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    {
        return Results.BadRequest();
    }

    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    await using var stream = File.Create(target);
    await files.CopyToAsync(stream);
    return Results.Ok();
});
```

For large files, raise the request size limits of the server: Kestrel's `MaxRequestBodySize` (about 28.6 MB by default; over it, the connection is closed and the status is `0`) and `FormOptions.MultipartBodyLengthLimit` (128 MB by default).

### Services

```csharp
builder.Services.AddBlazorComponents();
```

`AddBlazorComponents()` registers the services of this library, `IScreen`, `ISpeaker`, `IBrowserTabs`, `IClipboard`, `IGeolocation` and `IFileSystemAccess`, as scoped services, so each circuit of Blazor Server has its own. Inject them into components or other services. Call their methods after the first render or in an event handler, because JavaScript is not available while prerendering, and update a component from their events, such as `ScreenKeptOnChanged`, with `InvokeAsync(StateHasChanged)`. When prerendered WebAssembly components inject the services, call `AddBlazorComponents()` in the server project too. The components need no registration.

### IScreen

```csharp
[Inject]
private IScreen Screen { get; set; } = default!;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        await Screen.KeepScreenOnAsync(true);
        await Screen.KeepFullscreenAsync(true);
    }
}
```

`KeepScreenOnAsync(true)` keeps the screen on with a screen wake lock. The browser releases the lock while the page is hidden, and it is taken again when the page is shown. `IsScreenKeptOn` and `ScreenKeptOnChanged` tell whether the screen is kept on now. It is not on an insecure origin (`http` other than `localhost`), on a browser without the Screen Wake Lock API, or when the device refuses it, for example in battery saver.

`KeepFullscreenAsync(true)` makes the page fullscreen. The browser allows it only right after a user operation, so when the page is not fullscreen, it becomes fullscreen at the next touch or click. `KeepFullscreenAsync(false)` stops it and leaves fullscreen.

### ISpeaker

```csharp
await Speaker.PrepareAsync();
var played = await Speaker.PlayAsync(new Tone(880, 200), new Tone(1175, 200, 50));
var completed = await Speaker.SpeakAsync("新しい注文です。", new SpeechOptions { Lang = "ja-JP", Rate = 1.1 });
await Speaker.CancelAsync();
var voices = await Speaker.GetVoicesAsync();
```

The browser outputs sound only after the user operates the page. `PrepareAsync()` sets up the output, and `IsReady` becomes `true` when sound can be played: at once on a browser that allows autoplay or on a page the user has already operated, otherwise at the first touch, click or key press. `ReadyChanged` is raised when it changes, so a kiosk can ask the user to touch the screen until then. The other methods set up the output on the first call, too.

`PlayAsync` plays sine tones one after another. `Tone` is the frequency in Hz, the duration in milliseconds, and the delay in milliseconds after the previous tone. It returns `true` when the tones are played to the end, and `false` when sound cannot be played yet or the tones are canceled.

`SpeakAsync` reads the text sentence by sentence, and returns `true` when it is read to the end and `false` when it is canceled or cannot be read. `SpeechOptions` sets `Lang`, `Voice`, `Rate`, `Pitch` and `Volume` for each call; without `Voice`, a voice on the device is preferred for `Lang`. `IsSpeaking` and `SpeakingChanged` tell whether text is being read. `CancelAsync()` stops both the speech and the tones.

### IBrowserTabs

```csharp
var token = await Tabs.RunLockedAsync("token-refresh", RefreshAsync, TimeSpan.FromSeconds(40));

listening = await Tabs.ListenAsync("session", reason => InvokeAsync(() => OnSessionEnded(reason)));
await Tabs.TellAsync("session", "logged-out");
await listening.DisposeAsync();
```

`RunLockedAsync` runs the action while the tab holds the lock of the name, so the actions with the same name run one at a time in all the tabs of the origin, including in the same tab. It returns the result of the action, and an exception of the action is thrown as it is. The browser releases the lock when the tab that holds it is closed or crashes, so without `timeout` the action waits until the lock is free. With `timeout`, the action runs without the lock when the lock is not taken in that time, and it also runs without the lock on a browser without the Web Locks API. In every case the action runs once.

`TellAsync` sends a string to the other tabs of the origin, and `ListenAsync` receives the strings sent with the name until the returned object is disposed. The tab that tells does not receive its own message. On a browser without `BroadcastChannel`, nothing is sent or received.

### IClipboard

```csharp
var copied = await Clipboard.SetTextAsync(url);
```

`SetTextAsync` writes the text with the Clipboard API of the browser. When the API is not available, as on an insecure origin (`http` other than `localhost`), or the browser refuses it, the text is copied through a hidden text area instead. It returns `true` when the text is copied, and `false` when it is not.

Browsers allow writing to the clipboard only during or shortly after a user operation, so call `SetTextAsync` from a click or key event handler. With Blazor WebAssembly, the text is written while the event is handled. With Blazor Server, the handler runs after a round trip to the server; Chrome, Edge and Firefox allow it, but Safari may refuse it.

### IGeolocation

```csharp
var result = await Geolocation.GetLocationAsync(highAccuracy: true, timeout: TimeSpan.FromSeconds(3));
if (result.Position is { } position)
{
    Record(position.Latitude, position.Longitude, position.Accuracy);
}
```

`GetLocationAsync` gets the current position of the device: the latitude and the longitude in degrees, and the accuracy in meters. A cached position is not used. `Status` tells the result: `Success`, `Denied` when the user or the browser denies it, `Unavailable` when the device cannot tell the position, `Timeout`, and `NotSupported` on a browser without the Geolocation API or on an insecure origin (`http` other than `localhost`). `Position` is `null` unless `Status` is `Success`.

`timeout` limits the whole wait, including the time while the browser asks the user for the permission, so the caller can go on without the position after a few seconds. Without `timeout`, it waits until the position is taken. `highAccuracy` asks for a more accurate position, for example with GPS, which can take longer.

### IFileSystemAccess

```csharp
var result = await FileSystemAccess.SaveAsync("report.pdf", "application/pdf", bytes);
var result = await FileSystemAccess.SaveAsync("report.pdf", "application/pdf", stream);
var result = await FileSystemAccess.SaveAsync("report.pdf", "application/pdf", new Uri("files/report.pdf", UriKind.Relative));
```

`SaveAsync` lets the user pick where to save the file with the File System Access API, suggesting the file name and the file type from its extension and `contentType`. It returns `Saved` when the file is written, and `Canceled` when the user closes the picker. When the picker is not available, as on Firefox and Safari, or the browser refuses it, the file is downloaded, and it returns `Downloaded`.

Bytes and a stream are sent to the browser only after the user picks where to save, and written as they arrive, so the browser does not hold the whole file; nothing is sent when the user closes the picker. A stream is read from its current position and is not disposed. A download, without the picker, needs the whole file in the browser, as `SaveAsFile` does.

For large files, such as 250 MB or more, pass the URL of an endpoint that returns the file. The browser fetches it after the user picks and writes it as it arrives, or downloads it from the URL when the picker is not available, so the file goes neither through the circuit nor into memory. The URL must be on the same origin as the page, because browsers do not download from other origins by a link.

`SaveAsync` waits while the user picks, without the default timeout of JavaScript calls (one minute on Blazor Server); pass `cancellationToken` to stop waiting.

Browsers show the picker only shortly after a user operation, so call `SaveAsync` from a click or key event handler with the bytes or the stream at hand. With Blazor Server, the handler runs after a round trip to the server; when preparing the bytes takes more than a few seconds, the picker is refused and the file is downloaded. A stream and a URL are read only after the user picks, so open them first and make the content while reading.
