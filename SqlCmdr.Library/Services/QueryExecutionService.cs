using Microsoft.Data.SqlClient;
using SqlCmdr.Models;
using System.Data;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SqlCmdr.Abstractions;

namespace SqlCmdr.Services;

public class QueryExecutionService : IQueryExecutionService
{
    readonly ILogger<QueryExecutionService> _logger;
    SqlCommand? _currentCommand;
    readonly object _commandLock = new();

    public QueryExecutionService(ILogger<QueryExecutionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            throw new ArgumentException("SQL query cannot be null or empty.", nameof(request));
        }

        var stopwatch = Stopwatch.StartNew();
        var response = new QueryResponse();
        var totalRows = 0;
        var wasTruncated = false;
        var resultLimit = request.ResultLimit ?? 100;
        
        try
        {
            await using var connection = new SqlConnection(connectionString);
            connection.InfoMessage += (sender, e) =>
            {
                foreach (SqlError error in e.Errors)
                {
                    response.MessagesInternal.Add(error.Message);
                }
            };
            await connection.OpenAsync(cancellationToken);
            
            SqlCommand command;
            lock (_commandLock)
            {
                command = new SqlCommand(request.Sql, connection) { CommandTimeout = 300 };
                _currentCommand = command;
            }
            
            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                do
                {
                    var resultSet = new ResultSet();
                    resultSet.ColumnsInternal.AddRange(Enumerable.Range(0, reader.FieldCount).Select(reader.GetName));
                    
                    var rowCount = 0;
                    while (rowCount < resultLimit && await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object?>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row[resultSet.Columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        resultSet.RowsInternal.Add(row);
                        rowCount++;
                        totalRows++;
                    }
                    
                    // Check for truncation: if we hit the limit and there's more data
                    if (rowCount == resultLimit && !reader.IsClosed && await reader.ReadAsync(cancellationToken))
                    {
                        wasTruncated = true;
                    }
                    
                    response.ResultSetsInternal.Add(resultSet with { RowCount = rowCount });
                } while (await reader.NextResultAsync(cancellationToken));
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
            return response with 
            { 
                Success = false, 
                ErrorMessage = "Query was cancelled", 
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds 
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogDebug(ex, "Query execution failed");
            return response with 
            { 
                Success = false, 
                ErrorMessage = ex.Message, 
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds 
            };
        }
    }
}
