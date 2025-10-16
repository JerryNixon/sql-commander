using Microsoft.Data.SqlClient;
using SqlCmdr.Library.Models;
using System.Data;
using Microsoft.Extensions.Logging;
using SqlCmdr.Library.Abstractions;

namespace SqlCmdr.Library.Services;

public class MetadataService : IMetadataService
{
    readonly ILogger<MetadataService> _logger;
    
    public MetadataService(ILogger<MetadataService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            var serverVersion = connection.ServerVersion;
            var database = connection.Database;
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYSTEM_USER";
            var userName = (await command.ExecuteScalarAsync())?.ToString() ?? "Unknown";
            return new ConnectionTestResult { Success = true, ServerVersion = serverVersion, DatabaseName = database, UserName = userName };
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<DatabaseMetadata> GetMetadataAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        var metadata = new DatabaseMetadata();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return metadata with { Tables = await GetTablesAsync(connection), Views = await GetViewsAsync(connection), StoredProcedures = await GetStoredProceduresAsync(connection), ForeignKeys = await GetForeignKeysAsync(connection) };
    }

    async Task<List<ForeignKeyMetadata>> GetForeignKeysAsync(SqlConnection connection)
    {
        var fks = new List<ForeignKeyMetadata>();
        const string sql = @"
            SELECT 
                fk.name AS FkName,
                ps.name AS ParentSchema,
                pt.name AS ParentTable,
                pc.name AS ParentColumn,
                rs.name AS ReferencedSchema,
                rt.name AS ReferencedTable,
                rc.name AS ReferencedColumn
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
            INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
            INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
            INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
            INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
            INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
            WHERE pt.is_ms_shipped = 0 AND rt.is_ms_shipped = 0
            ORDER BY ps.name, pt.name, fk.name";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fks.Add(new ForeignKeyMetadata { Name = reader["FkName"].ToString()!, ParentSchema = reader["ParentSchema"].ToString()!, ParentTable = reader["ParentTable"].ToString()!, ParentColumn = reader["ParentColumn"].ToString()!, ReferencedSchema = reader["ReferencedSchema"].ToString()!, ReferencedTable = reader["ReferencedTable"].ToString()!, ReferencedColumn = reader["ReferencedColumn"].ToString()! });
        }
        return fks;
    }

    async Task<List<TableMetadata>> GetTablesAsync(SqlConnection connection)
    {
        var tables = new List<TableMetadata>();
        const string sql = @"
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                c.name AS ColumnName,
                ty.name AS DataType,
                c.is_nullable AS IsNullable,
                c.max_length AS MaxLength,
                c.precision AS Precision,
                c.scale AS Scale
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.columns c ON t.object_id = c.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name, c.column_id";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        string? currentSchema = null;
        string? currentTable = null;
        TableMetadata? current = null;
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var table = reader.GetString(1);
            if (currentSchema != schema || currentTable != table)
            {
                if (current is not null) tables.Add(current);
                current = new TableMetadata { Schema = schema, Name = table };
                currentSchema = schema;
                currentTable = table;
            }
            current!.Columns.Add(new ColumnMetadata { Name = reader.GetString(2), DataType = reader.GetString(3), IsNullable = reader.GetBoolean(4), MaxLength = reader.IsDBNull(5) ? null : reader.GetInt16(5), Precision = reader.IsDBNull(6) ? null : reader.GetByte(6), Scale = reader.IsDBNull(7) ? null : reader.GetByte(7) });
        }
        if (current is not null) tables.Add(current);
        return tables;
    }

    async Task<List<ViewMetadata>> GetViewsAsync(SqlConnection connection)
    {
        var views = new List<ViewMetadata>();
        const string sql = @"
            SELECT 
                s.name AS SchemaName,
                v.name AS ViewName,
                c.name AS ColumnName,
                ty.name AS DataType,
                c.is_nullable AS IsNullable,
                c.max_length AS MaxLength,
                c.precision AS Precision,
                c.scale AS Scale
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            INNER JOIN sys.columns c ON v.object_id = c.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            WHERE v.is_ms_shipped = 0
            ORDER BY s.name, v.name, c.column_id";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        string? currentSchema = null;
        string? currentView = null;
        ViewMetadata? current = null;
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var view = reader.GetString(1);
            if (currentSchema != schema || currentView != view)
            {
                if (current is not null) views.Add(current);
                current = new ViewMetadata { Schema = schema, Name = view };
                currentSchema = schema;
                currentView = view;
            }
            current!.Columns.Add(new ColumnMetadata { Name = reader.GetString(2), DataType = reader.GetString(3), IsNullable = reader.GetBoolean(4), MaxLength = reader.IsDBNull(5) ? null : reader.GetInt16(5), Precision = reader.IsDBNull(6) ? null : reader.GetByte(6), Scale = reader.IsDBNull(7) ? null : reader.GetByte(7) });
        }
        if (current is not null) views.Add(current);
        return views;
    }

    async Task<List<StoredProcedureMetadata>> GetStoredProceduresAsync(SqlConnection connection)
    {
        var procedures = new List<StoredProcedureMetadata>();
        const string sql = @"
            SELECT 
                s.name AS SchemaName,
                p.name AS ProcedureName,
                pm.name AS ParameterName,
                ty.name AS DataType,
                CASE WHEN pm.is_output = 1 THEN 'Output' ELSE 'Input' END AS Direction,
                pm.max_length AS MaxLength,
                pm.precision AS Precision,
                pm.scale AS Scale,
                OBJECT_DEFINITION(p.object_id) AS Definition
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            LEFT JOIN sys.parameters pm ON p.object_id = pm.object_id
            LEFT JOIN sys.types ty ON pm.user_type_id = ty.user_type_id
            WHERE p.is_ms_shipped = 0
            ORDER BY s.name, p.name, pm.parameter_id";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        string? currentSchema = null;
        string? currentProc = null;
        StoredProcedureMetadata? current = null;
        while (await reader.ReadAsync())
        {
            var schema = reader.GetString(0);
            var proc = reader.GetString(1);
            if (currentSchema != schema || currentProc != proc)
            {
                if (current is not null) procedures.Add(current);
                var definition = reader.IsDBNull(8) ? null : reader.GetString(8);
                current = new StoredProcedureMetadata { Schema = schema, Name = proc, Definition = definition };
                currentSchema = schema;
                currentProc = proc;
            }
            if (!reader.IsDBNull(2))
            {
                current!.Parameters.Add(new ParameterMetadata { Name = reader.GetString(2), DataType = reader.GetString(3), Direction = reader.GetString(4), MaxLength = reader.IsDBNull(5) ? null : reader.GetInt16(5), Precision = reader.IsDBNull(6) ? null : reader.GetByte(6), Scale = reader.IsDBNull(7) ? null : reader.GetByte(7) });
            }
        }
        if (current is not null) procedures.Add(current);
        foreach (var proc in procedures)
        {
            try { proc.OutputColumns.AddRange(await GetProcedureOutputColumnsAsync(connection, proc.FullName)); } catch { }
        }
        return procedures;
    }

    async Task<List<ColumnMetadata>> GetProcedureOutputColumnsAsync(SqlConnection connection, string procedureName)
    {
        var columns = new List<ColumnMetadata>();
        try
        {
            await using var command = new SqlCommand("sp_describe_first_result_set", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 5 };
            command.Parameters.AddWithValue("@tsql", $"EXEC {procedureName}");
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader.IsDBNull(reader.GetOrdinal("name")) ? $"Column{reader.GetInt32(reader.GetOrdinal("column_ordinal"))}" : reader.GetString(reader.GetOrdinal("name"));
                var systemTypeName = reader.GetString(reader.GetOrdinal("system_type_name"));
                var isNullable = reader.GetBoolean(reader.GetOrdinal("is_nullable"));
                columns.Add(new ColumnMetadata { Name = columnName, DataType = systemTypeName, IsNullable = isNullable, MaxLength = reader.IsDBNull(reader.GetOrdinal("max_length")) ? null : reader.GetInt16(reader.GetOrdinal("max_length")), Precision = reader.IsDBNull(reader.GetOrdinal("precision")) ? null : reader.GetByte(reader.GetOrdinal("precision")), Scale = reader.IsDBNull(reader.GetOrdinal("scale")) ? null : reader.GetByte(reader.GetOrdinal("scale")) });
            }
        }
        catch { }
        return columns;
    }
}
