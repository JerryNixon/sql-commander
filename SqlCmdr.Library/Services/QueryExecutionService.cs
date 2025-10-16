using Microsoft.Data.SqlClient;
using SqlCmdr.Library.Models;
using System.Data;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SqlCmdr.Library.Abstractions;

namespace SqlCmdr.Library.Services;

public class QueryExecutionService : IQueryExecutionService
{
    readonly ILogger<QueryExecutionService> _logger;
    SqlCommand? _currentCommand;

    public QueryExecutionService(ILogger<QueryExecutionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void CancelCurrentQuery()
    {
        try { _currentCommand?.Cancel(); } catch { }
    }

    public async Task<QueryResponse> ExecuteQueryAsync(string connectionString, QueryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));
        
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.Sql))
            throw new ArgumentException("SQL query cannot be null or empty.", nameof(request));

        var stopwatch = Stopwatch.StartNew();
        var resultSets = new List<ResultSet>();
        var messages = new List<string>();
        var totalRows = 0;
        var wasTruncated = false;
        var resultLimit = request.ResultLimit ?? 100;
        try
        {
            await using var connection = new SqlConnection(connectionString);
            connection.InfoMessage += (sender, e) => { foreach (SqlError error in e.Errors) messages.Add(error.Message); };
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(request.Sql, connection) { CommandTimeout = 300 };
            _currentCommand = command;
            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                do
                {
                    var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
                    var rows = new List<Dictionary<string, object?>>();
                    var rowCount = 0;
                    while (await reader.ReadAsync(cancellationToken) && rowCount < resultLimit)
                    {
                        var row = new Dictionary<string, object?>();
                        for (var i = 0; i < reader.FieldCount; i++) row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        rows.Add(row);
                        rowCount++;
                        totalRows++;
                    }
                    if (await reader.ReadAsync(cancellationToken)) wasTruncated = true;
                    resultSets.Add(new ResultSet { Columns = columns, Rows = rows, RowCount = rowCount });
                } while (await reader.NextResultAsync(cancellationToken));
            }
            finally { _currentCommand = null; }
            stopwatch.Stop();
            return new QueryResponse { Success = true, Messages = messages, ResultSets = resultSets, ElapsedMilliseconds = stopwatch.ElapsedMilliseconds, TotalRowsReturned = totalRows, WasTruncated = wasTruncated };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new QueryResponse { Success = false, ErrorMessage = "Query was cancelled", Messages = messages, ElapsedMilliseconds = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new QueryResponse { Success = false, ErrorMessage = ex.Message, ElapsedMilliseconds = stopwatch.ElapsedMilliseconds };
        }
        finally { _currentCommand = null; }
    }
}
