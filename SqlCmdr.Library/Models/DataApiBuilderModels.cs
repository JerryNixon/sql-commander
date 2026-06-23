namespace SqlCmdr.Models;

public record DataApiBuilderGenerateRequest
{
    public IReadOnlyList<DataApiBuilderSelection> Selections { get; init; } = [];
    public bool? RestEnabled { get; init; }
    public bool? GraphQLEnabled { get; init; }
    public bool? McpEnabled { get; init; }
}

public record DataApiBuilderSelection
{
    public string Type { get; init; } = string.Empty;
    public string Schema { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<string> KeyFields { get; init; } = [];
}

public record DataApiBuilderGenerateResponse
{
    public bool Success { get; init; }
    public string? ConfigJson { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Diagnostics { get; init; }
    public string FileName { get; init; } = "data-api.generated.json";
}
