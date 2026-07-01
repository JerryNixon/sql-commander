using FluentAssertions;
using SqlCmdr.Helpers;

namespace SqlCmdr.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "QueryErrorDiagnostics")]
public class QueryErrorDiagnosticsTests
{
    [Theory]
    [InlineData(208, "Invalid object name 'dbo.DoesNotExist'.", "object-not-found", "Object or column was not found.")]
    [InlineData(102, "Incorrect syntax near 'FORM'.", "syntax-error", "SQL syntax error.")]
    [InlineData(18456, "Login failed for user 'sa'.", "login-failed", "Login failed.")]
    [InlineData(-2, "Execution Timeout Expired.", "query-timeout", "Query timed out.")]
    [InlineData(229, "The SELECT permission was denied on the object 'Todo'.", "permission-denied", "Permission denied.")]
    [InlineData(40615, "Client with IP address is not allowed to access the server.", "azure-sql-firewall", "Azure SQL firewall blocked this connection.")]
    public void FromSqlErrorData_WithKnownSqlError_ReturnsActionableDiagnostic(int number, string message, string expectedType, string expectedTitle)
    {
        var diagnostic = QueryErrorDiagnostics.FromSqlErrorData(number, message);

        diagnostic.ErrorType.Should().Be(expectedType);
        diagnostic.ErrorTitle.Should().Be(expectedTitle);
        diagnostic.ErrorMessage.Should().Be(message);
        diagnostic.FixRecommendation.Should().NotBeNullOrWhiteSpace();
        diagnostic.TroubleshootingSteps.Should().NotBeEmpty();
    }

    [Fact]
    public void FromSqlErrorData_WithCertificateMessage_ReturnsCertificateDiagnostic()
    {
        var diagnostic = QueryErrorDiagnostics.FromSqlErrorData(null, "SSL Provider: The certificate chain was issued by an authority that is not trusted.");

        diagnostic.ErrorType.Should().Be("certificate-validation");
        diagnostic.ErrorTitle.Should().Be("SQL encryption/certificate validation failed.");
        diagnostic.FixRecommendation.Should().Contain("Trust Server Certificate");
    }

    [Fact]
    public void FromException_WithOperationCanceledException_ReturnsCancelledDiagnostic()
    {
        var diagnostic = QueryErrorDiagnostics.FromException(new OperationCanceledException("Query was cancelled"));

        diagnostic.ErrorType.Should().Be("cancelled");
        diagnostic.ErrorTitle.Should().Be("Query cancelled.");
    }
}