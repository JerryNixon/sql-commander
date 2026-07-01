using Microsoft.Data.SqlClient;
using SqlCmdr.Models;

namespace SqlCmdr.Helpers;

public sealed record QueryErrorDiagnostic(
    string ErrorType,
    string ErrorTitle,
    string ErrorMessage,
    string FixRecommendation,
    string? ErrorCode = null,
    string? ErrorDetail = null,
    IReadOnlyList<string>? TroubleshootingSteps = null)
{
    public IReadOnlyList<string> TroubleshootingSteps { get; init; } = TroubleshootingSteps ?? [];
}

/// <summary>
/// Classifies query failures (SQL Server errors, timeouts, cancellations, and generic
/// exceptions) into user-facing, actionable diagnostics.
/// </summary>
public static class QueryErrorDiagnostics
{
    private const string GenericMessage = "SQL Server returned an error while running the query.";

    private static readonly string[] DataValueSteps =
    [
        "Review the values in the statement.",
        "Compare the values with column definitions and constraints."
    ];

    private static readonly IReadOnlyList<DiagnosticRule> Rules =
    [
        new("server-unreachable",
            "SQL Server is unreachable.",
            "Check the server name, instance or port, container DNS, VPN/firewall access, and that SQL Server accepts remote connections.",
            [
                "Verify the Server Name in Settings.",
                "If this is SQL Server in Docker, use the host/container name that is reachable from SQL Commander.",
                "Confirm SQL Server is running and listening on the expected port, commonly 1433."
            ],
            Numbers: [53, 26, 11001, 10060, 10061],
            MessageMatch: m => m.Contains("server was not found")
                || m.Contains("network-related")
                || m.Contains("actively refused")
                || m.Contains("could not open a connection")),

        new("azure-sql-firewall",
            "Azure SQL firewall blocked this connection.",
            "Add a firewall rule for this client/container host or connect from an allowed network.",
            [
                "In Azure Portal, add the current client IP to the SQL server firewall rules.",
                "If SQL Commander runs in a container/cloud host, allow that host''s outbound IP."
            ],
            Numbers: [40615],
            MessageMatch: m => m.Contains("client with ip address") && m.Contains("is not allowed")),

        new("login-failed",
            "Login failed.",
            "Verify the authentication type, username/password or Azure identity, and confirm the login is enabled.",
            [
                "Review Authentication in Settings.",
                "For SQL authentication, confirm SQL logins are enabled on the server.",
                "For Azure identity, confirm the identity has Microsoft Entra and database permissions."
            ],
            Numbers: [18456, 18452, 18470, 18487, 18488]),

        new("database-unavailable",
            "Database is unavailable.",
            "Check the database name and confirm this login/user has access to it.",
            [
                "Review Database Name in Settings.",
                "Confirm the database exists on the selected server.",
                "Confirm the user is mapped to the database or has access through Microsoft Entra."
            ],
            Numbers: [4060, 916, 911]),

        new("permission-denied",
            "Permission denied.",
            "Grant the required SELECT, EXECUTE, or metadata permission, or use a different account.",
            [
                "Check whether the query needs SELECT, INSERT, UPDATE, DELETE, EXECUTE, or VIEW DEFINITION.",
                "Ask a database administrator to grant least-privilege access for the target object."
            ],
            Numbers: [229, 230, 297],
            MessageMatch: m => m.Contains("permission") && m.Contains("denied")),

        new("object-not-found",
            "Object or column was not found.",
            "Refresh metadata, check schema qualification, spelling, and current database.",
            [
                "Use two-part names like schema.object when possible.",
                "Refresh the schema tree if the object was recently created or renamed.",
                "Confirm the status bar shows the database you intended to query."
            ],
            Numbers: [208, 207, 2812]),

        new("syntax-error",
            "SQL syntax error.",
            "Review the token or line named in the error and verify T-SQL syntax.",
            [
                "Look for misspelled keywords, missing commas, unbalanced quotes, or clauses in the wrong order.",
                "If the query was generated, compare it with the object type selected in the tree."
            ],
            Numbers: [102, 156, 4145],
            MessageMatch: m => m.Contains("incorrect syntax near")),

        new("certificate-validation",
            "SQL encryption/certificate validation failed.",
            "For local/dev SQL Server, enable Trust Server Certificate. For production, use a trusted SQL Server certificate.",
            [
                "Open Settings and review Encrypt and Trust Server Certificate.",
                "For production, prefer a certificate trusted by the SQL Commander host."
            ],
            Numbers: [],
            MessageMatch: m => m.Contains("certificate")
                || m.Contains("ssl provider")
                || m.Contains("trust")
                || m.Contains("encryption")),

        new("duplicate-key",
            "Duplicate key value.",
            "Use a unique key value or update the existing row instead.",
            DataValueSteps,
            Numbers: [2627, 2601]),

        new("constraint-violation",
            "A constraint blocked this change.",
            "Check foreign key, check constraint, or delete/update order.",
            DataValueSteps,
            Numbers: [547]),

        new("required-value-missing",
            "A required column received NULL.",
            "Provide a value for required columns or change the column to allow nulls.",
            DataValueSteps,
            Numbers: [515]),

        new("data-value-error",
            "A value could not be converted or stored.",
            "Check data types, string lengths, numeric ranges, and divide-by-zero cases.",
            DataValueSteps,
            Numbers: [245, 8152, 2628, 8115, 8134]),

        new("locking-conflict",
            "The query was blocked or deadlocked.",
            "Retry, reduce transaction scope, or investigate blocking sessions.",
            [
                "Retry the query after a moment.",
                "If this repeats, inspect blocking sessions or long-running transactions on SQL Server."
            ],
            Numbers: [1205, 1222])
    ];

    public static QueryErrorDiagnostic FromException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            OperationCanceledException => new QueryErrorDiagnostic(
                "cancelled",
                "Query cancelled.",
                "The query was cancelled before it completed.",
                "Run the query again when you are ready.",
                TroubleshootingSteps: ["If cancellation was unexpected, check whether the browser tab, app, or SQL connection was interrupted."]),
            SqlException sqlException => FromSqlException(sqlException),
            TimeoutException => Timeout(exception.Message),
            _ when FindInnerException<SqlException>(exception) is { } inner => FromSqlException(inner),
            _ => FromMessage(null, exception.Message, exception.ToString())
        };
    }

    public static QueryErrorDiagnostic FromSqlException(SqlException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var primary = exception.Errors.Count > 0 ? exception.Errors[0] : null;
        var number = primary?.Number ?? exception.Number;
        var detailLines = exception.Errors.Cast<SqlError>()
            .Select(error => $"SQL {error.Number}, state {error.State}, class {error.Class}, line {error.LineNumber}: {error.Message}")
            .ToArray();
        var detail = detailLines.Length > 0 ? string.Join(Environment.NewLine, detailLines) : exception.ToString();

        return FromSqlErrorData(number, exception.Message, detail);
    }

    public static QueryErrorDiagnostic FromSqlErrorData(int? number, string? message, string? detail = null)
    {
        var code = number?.ToString();
        var safeMessage = string.IsNullOrWhiteSpace(message) ? GenericMessage : message!;
        var normalized = safeMessage.ToLowerInvariant();

        if (number is -2 || normalized.Contains("timeout expired"))
        {
            return Timeout(safeMessage, code, detail);
        }

        foreach (var rule in Rules)
        {
            if (rule.Matches(number, normalized))
            {
                return rule.Build(safeMessage, code, detail);
            }
        }

        return FromMessage(code, safeMessage, detail);
    }

    public static QueryResponse ToFailedResponse(this QueryErrorDiagnostic diagnostic, QueryResponse? baseResponse = null, long elapsedMilliseconds = 0)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var response = baseResponse ?? new QueryResponse();
        response.TroubleshootingStepsInternal.AddRange(diagnostic.TroubleshootingSteps);

        return response with
        {
            Success = false,
            ErrorMessage = diagnostic.ErrorMessage,
            ErrorCode = diagnostic.ErrorCode,
            ErrorType = diagnostic.ErrorType,
            ErrorTitle = diagnostic.ErrorTitle,
            ErrorDetail = diagnostic.ErrorDetail,
            FixRecommendation = diagnostic.FixRecommendation,
            ElapsedMilliseconds = elapsedMilliseconds
        };
    }

    private static QueryErrorDiagnostic Timeout(string message, string? code = null, string? detail = null) => new(
        "query-timeout",
        "Query timed out.",
        string.IsNullOrWhiteSpace(message) ? "The query did not finish before the timeout expired." : message,
        "Optimize the query, add filters/indexes, or increase Command Timeout in Settings.",
        code,
        detail,
        ["Add a WHERE clause or reduce the result set.", "Check indexing and execution plan for expensive scans.", "Increase Command Timeout in Settings if the query is expected to run longer."]);

    private static QueryErrorDiagnostic FromMessage(string? code, string message, string? detail) => new(
        "query-error",
        "Query failed.",
        string.IsNullOrWhiteSpace(message) ? GenericMessage : message,
        "Review the technical details, verify the SQL text, and check the current connection settings.",
        code,
        detail,
        ["Check the selected server and database in the status bar.", "Refresh metadata if objects have recently changed.", "Use Test Connection if the error appears connection-related."]);

    private static TException? FindInnerException<TException>(Exception exception)
        where TException : Exception
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is TException typed)
            {
                return typed;
            }
        }

        return null;
    }

    private sealed record DiagnosticRule(
        string ErrorType,
        string ErrorTitle,
        string FixRecommendation,
        IReadOnlyList<string> TroubleshootingSteps,
        int[] Numbers,
        Func<string, bool>? MessageMatch = null)
    {
        public bool Matches(int? number, string normalizedMessage)
            => (number is int value && Array.IndexOf(Numbers, value) >= 0)
                || (MessageMatch?.Invoke(normalizedMessage) ?? false);

        public QueryErrorDiagnostic Build(string message, string? code, string? detail)
            => new(ErrorType, ErrorTitle, message, FixRecommendation, code, detail, TroubleshootingSteps);
    }
}
