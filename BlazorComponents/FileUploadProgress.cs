namespace BlazorComponents;

public sealed record FileUploadProgress(int CompletedFiles, int TotalFiles, long LoadedBytes, long TotalBytes, string? CurrentPath);
