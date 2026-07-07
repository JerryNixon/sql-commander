using Microsoft.Extensions.Logging;
using SqlCmdr.Abstractions;
using SqlCmdr.Infrastructure;
using SqlCmdr.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SqlCmdr.Services;

public class MetadataService : IMetadataService
{
    readonly ISqlConnectionFactory _connectionFactory;
    readonly ILogger<MetadataService> _logger;

    public MetadataService(ILogger<MetadataService> logger, ISqlConnectionFactory connectionFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(settings).ConfigureAwait(false);
            var serverVersion = connection.ServerVersion;
            var database = connection.Database;
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT SYSTEM_USER";
            var userName = (await command.ExecuteScalarAsync().ConfigureAwait(false))?.ToString() ?? "Unknown";
            return new ConnectionTestResult { Success = true, ServerVersion = serverVersion, DatabaseName = database, UserName = userName };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");
            return new ConnectionTestResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        var settings = AppSettings.FromConnectionString(connectionString);
        return await TestConnectionAsync(settings);
    }

    public async Task<DatabaseMetadata> GetMetadataAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var metadata = new DatabaseMetadata();
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(settings).ConfigureAwait(false);

        metadata.Connection = await GetConnectionInfoAsync(connection).ConfigureAwait(false);
        metadata.TablesInternal.AddRange(await GetTablesAsync(connection).ConfigureAwait(false));
        metadata.ViewsInternal.AddRange(await GetViewsAsync(connection).ConfigureAwait(false));
        metadata.StoredProceduresInternal.AddRange(await GetStoredProceduresAsync(connection).ConfigureAwait(false));
        metadata.ForeignKeysInternal.AddRange(await GetForeignKeysAsync(connection).ConfigureAwait(false));

        return metadata;
    }

    public async Task<DatabaseMetadata> GetMetadataAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        var settings = AppSettings.FromConnectionString(connectionString);
        return await GetMetadataAsync(settings);
    }

    public async Task<DatabaseListResult> ListDatabasesAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();
        var candidates = new List<AppSettings> { normalized with { Database = "master" } };
        if (!string.Equals(normalized.Database, "master", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(normalized);
        }

        Exception? lastException = null;
        foreach (var candidate in candidates)
        {
            try
            {
                await using var connection = await _connectionFactory.CreateOpenConnectionAsync(candidate, cancellationToken).ConfigureAwait(false);
                var databases = await ReadAccessibleDatabasesAsync(connection, cancellationToken).ConfigureAwait(false);
                return new DatabaseListResult
                {
                    Success = true,
                    Databases = databases,
                    CurrentDatabase = connection.Database
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogDebug(ex, "Failed to list databases using initial catalog {Database}", candidate.Database);
            }
        }

        _logger.LogWarning(lastException, "Failed to list databases for {Server}", normalized.Server);
        return new DatabaseListResult
        {
            Success = false,
            ErrorMessage = lastException?.Message ?? "Could not load databases."
        };
    }

    static async Task<IReadOnlyList<string>> ReadAccessibleDatabasesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT [name]
            FROM sys.databases
            WHERE [state] = 0
              AND HAS_DBACCESS([name]) = 1
            ORDER BY
                CASE WHEN [name] = DB_NAME() THEN 0 ELSE 1 END,
                [name];";

        var databases = new List<string>();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 10 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            databases.Add(reader.GetString(0));
        }

        return databases;
    }

    async Task<ConnectionMetadata> GetConnectionInfoAsync(SqlConnection connection)
    {
        const string sql = @"
            SELECT
                CONVERT(nvarchar(256), SERVERPROPERTY('ServerName')) AS ServerName,
                DB_NAME() AS DatabaseName,
                SYSTEM_USER AS UserName,
                CONVERT(nvarchar(128), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
                CONVERT(nvarchar(128), SERVERPROPERTY('ProductLevel')) AS ProductLevel,
                CONVERT(nvarchar(256), SERVERPROPERTY('Edition')) AS Edition,
                CONVERT(nvarchar(max), @@VERSION) AS Version";

        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 5 };
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
        {
            return new ConnectionMetadata
            {
                ServerName = connection.DataSource,
                DatabaseName = connection.Database,
                VersionShort = connection.ServerVersion
            };
        }

        var serverName = reader["ServerName"]?.ToString() ?? connection.DataSource;
        var databaseName = reader["DatabaseName"]?.ToString() ?? connection.Database;
        var userName = reader["UserName"]?.ToString() ?? string.Empty;
        var productVersion = reader["ProductVersion"]?.ToString() ?? connection.ServerVersion;
        var productLevel = reader["ProductLevel"]?.ToString() ?? string.Empty;
        var edition = reader["Edition"]?.ToString() ?? string.Empty;
        var version = reader["Version"]?.ToString() ?? string.Empty;

        return new ConnectionMetadata
        {
            ServerName = serverName,
            DatabaseName = databaseName,
            UserName = userName,
            ProductVersion = productVersion,
            ProductLevel = productLevel,
            Edition = edition,
            Version = version,
            VersionShort = BuildVersionShort(productVersion, productLevel)
        };
    }

    static string BuildVersionShort(string productVersion, string productLevel)
    {
        if (string.IsNullOrWhiteSpace(productVersion))
        {
            return string.Empty;
        }

        var parts = productVersion.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var majorMinor = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : productVersion;
        return string.IsNullOrWhiteSpace(productLevel) ? majorMinor : $"{majorMinor} {productLevel}";
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
                rc.name AS ReferencedColumn,
                fkc.constraint_column_id AS ConstraintColumnId
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id
            INNER JOIN sys.schemas ps ON pt.schema_id = ps.schema_id
            INNER JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
            INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
            INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
            INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
            WHERE pt.is_ms_shipped = 0 AND rt.is_ms_shipped = 0
            ORDER BY ps.name, pt.name, fk.name, fkc.constraint_column_id";
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            fks.Add(new ForeignKeyMetadata { Name = reader["FkName"].ToString()!, ParentSchema = reader["ParentSchema"].ToString()!, ParentTable = reader["ParentTable"].ToString()!, ParentColumn = reader["ParentColumn"].ToString()!, ReferencedSchema = reader["ReferencedSchema"].ToString()!, ReferencedTable = reader["ReferencedTable"].ToString()!, ReferencedColumn = reader["ReferencedColumn"].ToString()!, ConstraintColumnId = Convert.ToInt32(reader["ConstraintColumnId"]) });
        }
        return fks;
    }

    async Task<List<TableMetadata>> GetTablesAsync(SqlConnection connection)
    {
        const string sql = @"
            SELECT 
                s.name AS SchemaName,
                t.name AS TableName,
                c.name AS ColumnName,
                ty.name AS DataType,
                c.is_nullable AS IsNullable,
                c.max_length AS MaxLength,
                c.precision AS Precision,
                c.scale AS Scale,
                CAST(CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS bit) AS IsPrimaryKey
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            INNER JOIN sys.columns c ON t.object_id = c.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                WHERE i.is_primary_key = 1 AND ic.is_included_column = 0
            ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
            WHERE t.is_ms_shipped = 0
            ORDER BY s.name, t.name, c.column_id";
        return await ReadColumnarObjectsAsync(
            connection,
            sql,
            (schema, name) => new TableMetadata { Schema = schema, Name = name },
            (table, column) => table.ColumnsInternal.Add(column)).ConfigureAwait(false);
    }

    async Task<List<ViewMetadata>> GetViewsAsync(SqlConnection connection)
    {
        const string sql = @"
            SELECT 
                s.name AS SchemaName,
                v.name AS ViewName,
                c.name AS ColumnName,
                ty.name AS DataType,
                c.is_nullable AS IsNullable,
                c.max_length AS MaxLength,
                c.precision AS Precision,
                c.scale AS Scale,
                CAST(CASE WHEN viewKey.column_id IS NULL THEN 0 ELSE 1 END AS bit) AS IsPrimaryKey
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            INNER JOIN sys.columns c ON v.object_id = c.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.indexes i
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                WHERE i.is_unique = 1 AND ic.is_included_column = 0
            ) viewKey ON viewKey.object_id = c.object_id AND viewKey.column_id = c.column_id
            WHERE v.is_ms_shipped = 0
            ORDER BY s.name, v.name, c.column_id";
        return await ReadColumnarObjectsAsync(
            connection,
            sql,
            (schema, name) => new ViewMetadata { Schema = schema, Name = name },
            (view, column) => view.ColumnsInternal.Add(column)).ConfigureAwait(false);
    }

    // Reads schema-qualified objects whose result set is grouped by (schema, name) with one
    // row per column. Shared by tables and views, which have an identical column projection.
    static async Task<List<T>> ReadColumnarObjectsAsync<T>(
        SqlConnection connection,
        string sql,
        Func<string, string, T> createObject,
        Action<T, ColumnMetadata> addColumn)
        where T : class
    {
        var results = new List<T>();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

        string? currentSchema = null;
        string? currentName = null;
        T? current = null;
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var schema = reader.GetString(0);
            var name = reader.GetString(1);
            if (currentSchema != schema || currentName != name)
            {
                if (current is not null)
                {
                    results.Add(current);
                }
                current = createObject(schema, name);
                currentSchema = schema;
                currentName = name;
            }
            addColumn(current!, ReadColumn(reader));
        }
        if (current is not null)
        {
            results.Add(current);
        }
        return results;
    }

    static ColumnMetadata ReadColumn(SqlDataReader reader) => new()
    {
        Name = reader.GetString(2),
        DataType = reader.GetString(3),
        IsNullable = reader.GetBoolean(4),
        MaxLength = reader.IsDBNull(5) ? null : reader.GetInt16(5),
        Precision = reader.IsDBNull(6) ? null : reader.GetByte(6),
        Scale = reader.IsDBNull(7) ? null : reader.GetByte(7),
        IsPrimaryKey = reader.GetBoolean(8)
    };

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
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        string? currentSchema = null;
        string? currentProc = null;
        StoredProcedureMetadata? current = null;
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var schema = reader.GetString(0);
            var proc = reader.GetString(1);
            if (currentSchema != schema || currentProc != proc)
            {
                if (current is not null)
                {
                    procedures.Add(current);
                }
                var definition = reader.IsDBNull(8) ? null : reader.GetString(8);
                current = new StoredProcedureMetadata { Schema = schema, Name = proc, Definition = definition };
                currentSchema = schema;
                currentProc = proc;
            }
            if (!reader.IsDBNull(2))
            {
                current!.ParametersInternal.Add(new ParameterMetadata { Name = reader.GetString(2), DataType = reader.GetString(3), Direction = reader.GetString(4), MaxLength = reader.IsDBNull(5) ? null : reader.GetInt16(5), Precision = reader.IsDBNull(6) ? null : reader.GetByte(6), Scale = reader.IsDBNull(7) ? null : reader.GetByte(7) });
            }
        }
        if (current is not null)
        {
            procedures.Add(current);
        }
        foreach (var proc in procedures)
        {
            try
            {
                proc.OutputColumnsInternal.AddRange(await GetProcedureOutputColumnsAsync(connection, proc.FullName).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to get output columns for procedure {ProcedureName}", proc.FullName);
            }
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
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var columnName = reader.IsDBNull(reader.GetOrdinal("name")) ? $"Column{reader.GetInt32(reader.GetOrdinal("column_ordinal"))}" : reader.GetString(reader.GetOrdinal("name"));
                var systemTypeName = reader.GetString(reader.GetOrdinal("system_type_name"));
                var isNullable = reader.GetBoolean(reader.GetOrdinal("is_nullable"));
                columns.Add(new ColumnMetadata { Name = columnName, DataType = systemTypeName, IsNullable = isNullable, MaxLength = reader.IsDBNull(reader.GetOrdinal("max_length")) ? null : reader.GetInt16(reader.GetOrdinal("max_length")), Precision = reader.IsDBNull(reader.GetOrdinal("precision")) ? null : reader.GetByte(reader.GetOrdinal("precision")), Scale = reader.IsDBNull(reader.GetOrdinal("scale")) ? null : reader.GetByte(reader.GetOrdinal("scale")) });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to describe result set for procedure {ProcedureName}", procedureName);
        }
        return columns;
    }
}
