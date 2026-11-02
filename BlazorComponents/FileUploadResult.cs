namespace BlazorComponents;

public sealed record FileUploadResult(int Succeeded, int Failed, bool Canceled, IReadOnlyList<FileUploadError> Errors);
