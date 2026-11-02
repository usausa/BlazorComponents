# BlazorComponents

| Package | Info |
|:-|:-|
| BlazorComponents | [![NuGet](https://img.shields.io/nuget/v/BlazorComponents.svg)](https://www.nuget.org/packages/BlazorComponents) |
| BlazorComponents.Bootstrap | [![NuGet](https://img.shields.io/nuget/v/BlazorComponents.Bootstrap.svg)](https://www.nuget.org/packages/BlazorComponents.Bootstrap) |
| BlazorComponents.QrCode | [![NuGet](https://img.shields.io/nuget/v/BlazorComponents.QrCode.svg)](https://www.nuget.org/packages/BlazorComponents.QrCode) |

Component library for Blazor applications.

## Features

* Script helper extensions for JavaScript interop
* `ScriptModule` — load a JavaScript module on the first call
* `MediaQuery` — bind the result of a CSS media query
* `HotKey` — call a method on a keyboard shortcut
* `NativeDialog` — open and close a native `<dialog>` by binding
* `SpeechSynthesizer` — read text aloud with the browser's speech synthesis
* `FileDropZone` — pass dropped or pasted files to the `<InputFile>` inside it
* `FileUpload` — upload dropped, pasted or picked files and folders to a URL from the browser

## Usage

### Script helper extensions

```html
<script src="_content/BlazorComponents/script.min.js"></script>
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
await module.DisposeAsync();
```

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

### SpeechSynthesizer

```razor
<SpeechSynthesizer @ref="speech" Lang="ja-JP" Rate="1.1" @bind-Speaking="speaking" />
```

```csharp
var completed = await speech.SpeakAsync("新しい注文です。");
await speech.CancelAsync();
var voices = await speech.GetVoicesAsync();
```

`SpeakAsync` returns `true` when the text is read to the end, and `false` when it is canceled or cannot be read. Without `Voice`, a voice on the device is preferred for `Lang`. The browser speaks only after the first click on the page.

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
