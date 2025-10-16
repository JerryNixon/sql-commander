using SqlCmdr.Helpers;

namespace SqlCmdr.Models;

public record DatabaseMetadata
{
    private readonly List<TableMetadata> _tables = [];
    private readonly List<ViewMetadata> _views = [];
    private readonly List<StoredProcedureMetadata> _storedProcedures = [];
    private readonly List<ForeignKeyMetadata> _foreignKeys = [];

    public IReadOnlyList<TableMetadata> Tables => _tables;
    public IReadOnlyList<ViewMetadata> Views => _views;
    public IReadOnlyList<StoredProcedureMetadata> StoredProcedures => _storedProcedures;
    public IReadOnlyList<ForeignKeyMetadata> ForeignKeys => _foreignKeys;

    public List<TableMetadata> TablesInternal => _tables;
    public List<ViewMetadata> ViewsInternal => _views;
    public List<StoredProcedureMetadata> StoredProceduresInternal => _storedProcedures;
    public List<ForeignKeyMetadata> ForeignKeysInternal => _foreignKeys;
}

public record ForeignKeyMetadata
{
    public required string Name { get; init; }
    public required string ParentSchema { get; init; }
    public required string ParentTable { get; init; }
    public required string ParentColumn { get; init; }
    public required string ReferencedSchema { get; init; }
    public required string ReferencedTable { get; init; }
    public required string ReferencedColumn { get; init; }
}

public record TableMetadata
{
    private readonly List<ColumnMetadata> _columns = [];

    public required string Schema { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<ColumnMetadata> Columns => _columns;
    public string FullName => $"[{Schema}].[{Name}]";

    public List<ColumnMetadata> ColumnsInternal => _columns;
}

public record ViewMetadata
{
    private readonly List<ColumnMetadata> _columns = [];

    public required string Schema { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<ColumnMetadata> Columns => _columns;
    public string FullName => $"[{Schema}].[{Name}]";

    public List<ColumnMetadata> ColumnsInternal => _columns;
}

public record StoredProcedureMetadata
{
    private readonly List<ParameterMetadata> _parameters = [];
    private readonly List<ColumnMetadata> _outputColumns = [];

    public required string Schema { get; init; }
    public required string Name { get; init; }
    public IReadOnlyList<ParameterMetadata> Parameters => _parameters;
    public IReadOnlyList<ColumnMetadata> OutputColumns => _outputColumns;
    public string? Definition { get; init; }
    public string FullName => $"[{Schema}].[{Name}]";

    public List<ParameterMetadata> ParametersInternal => _parameters;
    public List<ColumnMetadata> OutputColumnsInternal => _outputColumns;
}

public record ColumnMetadata
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool IsNullable { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public string DisplayType => TypeFormatter.FormatDataType(DataType, MaxLength, Precision, Scale);
}

public record ParameterMetadata
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public required string Direction { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public string DisplayType => TypeFormatter.FormatDataType(DataType, MaxLength, Precision, Scale);
}
