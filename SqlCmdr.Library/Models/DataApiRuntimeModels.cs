namespace SqlCmdr.Models;

public record DataApiBuilderStartRequest
{
    public IReadOnlyList<DataApiBuilderSelection> Selections { get; init; } = [];
    public bool? RestEnabled { get; init; }
    public bool? GraphQLEnabled { get; init; }
    public bool? McpEnabled { get; init; }
}

public record DataApiRuntimeResponse
{
    public bool Success { get; init; } = true;
    public string State { get; init; } = "stopped";
    public bool Running { get; init; }
    public string? BaseUrl { get; init; }
    public string? HealthUrl { get; init; }
    public string? SwaggerUrl { get; init; }
    public string? NitroUrl { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Diagnostics { get; init; }
    public string? ConfigFileName { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
}
