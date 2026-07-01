using Microsoft.Extensions.Logging;
using SqlCmdr.Abstractions;
using SqlCmdr.Helpers;
using SqlCmdr.Infrastructure;
using SqlCmdr.Models;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace SqlCmdr.Services;

public class QueryExecutionService : IQueryExecutionService
{
    readonly ISqlConnectionFactory _connectionFactory;
    readonly ILogger<QueryExecutionService> _logger;
    SqlCommand? _currentCommand;
    readonly object _commandLock = new();

    public QueryExecutionService(ILogger<QueryExecutionService> logger, ISqlConnectionFactory connectionFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public void CancelCurrentQuery()
    {
        lock (_commandLock)
        {
            try
            {
                _currentCommand?.Cancel();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error cancelling query");
            }
        }
    }

    public async Task<QueryResponse> ExecuteQueryAsync(string connectionString, QueryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        }

        var settings = AppSettings.FromConnectionString(connectionString);
        return await ExecuteQueryAsync(settings, request, cancellationToken);
    }

    public async Task<QueryResponse> ExecuteQueryAsync(AppSettings settings, QueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            throw new ArgumentException("SQL query cannot be null or empty.", nameof(request));
        }

        var stopwatch = Stopwatch.StartNew();
        var response = new QueryResponse();
        var totalRows = 0;
        var wasTruncated = false;
        var resultLimit = Math.Max(1, request.ResultLimit ?? 100);

        try
        {
            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(settings, cancellationToken).ConfigureAwait(false);
            connection.InfoMessage += (sender, e) =>
            {
                foreach (SqlError error in e.Errors)
                {
                    response.MessagesInternal.Add(error.Message);
                }
            };

            SqlCommand command;
            lock (_commandLock)
            {
                command = new SqlCommand(request.Sql, connection) { CommandTimeout = Math.Max(0, settings.CommandTimeout) };
                _currentCommand = command;
            }

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                do
                {
                    var resultSet = new ResultSet();
                    var columnNames = MakeColumnNamesUnique(
                        Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList());
                    resultSet.ColumnsInternal.AddRange(columnNames);

                    var rowCount = 0;
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (rowCount == resultLimit)
                        {
                            wasTruncated = true;
                            break;
                        }

                        var row = new Dictionary<string, object?>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        resultSet.RowsInternal.Add(row);
                        rowCount++;
                        totalRows++;
                    }

                    response.ResultSetsInternal.Add(resultSet with { RowCount = rowCount });
                } while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));
            }
            finally
            {
                lock (_commandLock)
                {
                    _currentCommand = null;
                }
            }

            stopwatch.Stop();
            return response with 
            { 
                Success = true, 
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds, 
                TotalRowsReturned = totalRows, 
                WasTruncated = wasTruncated 
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogDebug("Query was cancelled");
            var diagnostic = QueryErrorDiagnostics.FromException(new OperationCanceledException("Query was cancelled"));
            return diagnostic.ToFailedResponse(response, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Query execution failed");
            var diagnostic = QueryErrorDiagnostics.FromException(ex);
            return diagnostic.ToFailedResponse(response, stopwatch.ElapsedMilliseconds);
        }
    }

    // Result sets can contain duplicate or empty column names (for example an unaliased join
    // such as SELECT a.Id, b.Id, or computed columns like SELECT 1, 2). Because rows are stored
    // in a name-keyed dictionary, duplicate names would overwrite earlier columns and silently
    // corrupt the displayed data. This produces a positional, unique name for every column.
    internal static List<string> MakeColumnNamesUnique(IReadOnlyList<string> rawNames)
    {
        ArgumentNullException.ThrowIfNull(rawNames);

        var names = new List<string>(rawNames.Count);
        var used = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < rawNames.Count; i++)
        {
            var baseName = string.IsNullOrEmpty(rawNames[i]) ? $"Column{i + 1}" : rawNames[i];
            var name = baseName;
            var suffix = 2;
            while (!used.Add(name))
            {
                name = $"{baseName} ({suffix})";
                suffix++;
            }
            names.Add(name);
        }
        return names;
    }
}
