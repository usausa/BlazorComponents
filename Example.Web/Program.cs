using Example.Web;
using Example.Web.Components;

using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton(new UploadStore());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/upload/{**folder}", (string? folder, [FromForm] string relativePath, IFormFile files, UploadStore store) =>
{
    if (Path.IsPathRooted(relativePath) || relativePath.Split('/', '\\').Contains(".."))
    {
        return Results.BadRequest();
    }

    store.Add(new UploadEntry(folder ?? string.Empty, relativePath, files.Length));
    return Results.Ok();
});

app.Run();
