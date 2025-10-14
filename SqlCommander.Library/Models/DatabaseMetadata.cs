namespace SqlCommander.Library.Models;

public record DatabaseMetadata
{
    public List<TableMetadata> Tables { get; init; } = [];
    public List<ViewMetadata> Views { get; init; } = [];
    public List<StoredProcedureMetadata> StoredProcedures { get; init; } = [];
    public List<ForeignKeyMetadata> ForeignKeys { get; init; } = [];
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
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public List<ColumnMetadata> Columns { get; init; } = [];
    public string FullName => $"[{Schema}].[{Name}]";
}

public record ViewMetadata
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public List<ColumnMetadata> Columns { get; init; } = [];
    public string FullName => $"[{Schema}].[{Name}]";
}

public record StoredProcedureMetadata
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public List<ParameterMetadata> Parameters { get; init; } = [];
    public List<ColumnMetadata> OutputColumns { get; init; } = [];
    public string? Definition { get; init; }
    public string FullName => $"[{Schema}].[{Name}]";
}

public record ColumnMetadata
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public bool IsNullable { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public string DisplayType
    {
        get
        {
            var baseType = DataType.ToLower();
            return baseType switch
            {
                "varchar" or "char" or "nvarchar" or "nchar" when MaxLength > 0 => $"{DataType}({(MaxLength == -1 ? "max" : MaxLength.ToString())})",
                "decimal" or "numeric" when Precision.HasValue => $"{DataType}({Precision},{Scale ?? 0})",
                _ => DataType
            };
        }
    }
}

public record ParameterMetadata
{
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public required string Direction { get; init; }
    public int? MaxLength { get; init; }
    public int? Precision { get; init; }
    public int? Scale { get; init; }
    public string DisplayType
    {
        get
        {
            var baseType = DataType.ToLower();
            return baseType switch
            {
                "varchar" or "char" or "nvarchar" or "nchar" when MaxLength > 0 => $"{DataType}({(MaxLength == -1 ? "max" : MaxLength.ToString())})",
                "decimal" or "numeric" when Precision.HasValue => $"{DataType}({Precision},{Scale ?? 0})",
                _ => DataType
            };
        }
    }
}
