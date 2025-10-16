namespace SqlCmdr.Library.Models;

public record QueryRequest
{
    public string Sql { get; init; } = string.Empty;
    public int? ResultLimit { get; init; }
}

public record QueryResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> Messages { get; init; } = [];
    public List<ResultSet> ResultSets { get; init; } = [];
    public long ElapsedMilliseconds { get; init; }
    public int TotalRowsReturned { get; init; }
    public bool WasTruncated { get; init; }
}

public record ResultSet
{
    public List<string> Columns { get; init; } = [];
    public List<Dictionary<string, object?>> Rows { get; init; } = [];
    public int RowCount { get; init; }
}

public record ConnectionTestResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ServerVersion { get; init; }
    public string? DatabaseName { get; init; }
    public string? UserName { get; init; }
}

public enum QueryState
{
    Idle,
    Running,
    Cancelling,
    Cancelled,
    Completed,
    Failed
}

public record StatusUpdate
{
    public QueryState State { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public int? RowCount { get; init; }
    public bool WasTruncated { get; init; }
    public string? ErrorMessage { get; init; }
}
