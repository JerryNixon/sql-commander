namespace SqlCmdr.Models;

public record QueryRequest
{
    public string Sql { get; init; } = string.Empty;
    public int? ResultLimit { get; init; }
}

public record QueryResponse
{
    private readonly List<string> _messages = [];
    private readonly List<ResultSet> _resultSets = [];

    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Messages => _messages;
    public IReadOnlyList<ResultSet> ResultSets => _resultSets;
    public long ElapsedMilliseconds { get; init; }
    public int TotalRowsReturned { get; init; }
    public bool WasTruncated { get; init; }

    public List<string> MessagesInternal => _messages;
    public List<ResultSet> ResultSetsInternal => _resultSets;
}

public record ResultSet
{
    private readonly List<string> _columns = [];
    private readonly List<Dictionary<string, object?>> _rows = [];

    public IReadOnlyList<string> Columns => _columns;
    public IReadOnlyList<Dictionary<string, object?>> Rows => _rows;
    public int RowCount { get; init; }

    public List<string> ColumnsInternal => _columns;
    public List<Dictionary<string, object?>> RowsInternal => _rows;
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
