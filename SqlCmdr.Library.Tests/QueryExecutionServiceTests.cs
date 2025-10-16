using SqlCmdr.Models;
using SqlCmdr.Services;
using SqlCmdr.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FluentAssertions;
using AutoFixture;

namespace SqlCmdr.Tests;

[Trait("Category", "Integration")]
[Trait("Service", "QueryExecutionService")]
public class QueryExecutionServiceTests
{
    private readonly IFixture _fixture;
    private readonly Mock<ILogger<QueryExecutionService>> _mockLogger;
    private readonly QueryExecutionService _sut;

    public QueryExecutionServiceTests()
    {
        _fixture = new Fixture();
        _mockLogger = new Mock<ILogger<QueryExecutionService>>();
        _sut = new QueryExecutionService(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new QueryExecutionService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var service = new QueryExecutionService(_mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IQueryExecutionService>();
    }

    #endregion

    #region ExecuteQueryAsync Tests

    [Fact]
    public async Task ExecuteQueryAsync_WithNullConnectionString_ThrowsArgumentException()
    {
        // Arrange
        var request = new QueryRequest { Sql = "SELECT 1" };

        // Act
        Func<Task> act = async () => await _sut.ExecuteQueryAsync(null!, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True";

        // Act
        Func<Task> act = async () => await _sut.ExecuteQueryAsync(connectionString, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteQueryAsync_WithEmptyOrWhitespaceSql_ThrowsArgumentException(string sql)
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True";
        var request = new QueryRequest { Sql = sql };

        // Act
        Func<Task> act = async () => await _sut.ExecuteQueryAsync(connectionString, request);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithInvalidSql_ReturnsFailureResponse()
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=5";
        var request = new QueryRequest { Sql = "SELECT * FROM NonExistentTable_12345" };

        // Act
        var response = await _sut.ExecuteQueryAsync(connectionString, request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        response.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        response.ResultSets.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithSyntaxError_ReturnsFailureWithErrorMessage()
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=5";
        var request = new QueryRequest { Sql = "SELECT * FORM sys.tables" }; // Intentional typo

        // Act
        var response = await _sut.ExecuteQueryAsync(connectionString, request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Contain("syntax").And.NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithInvalidConnectionString_ReturnsFailureResponse()
    {
        // Arrange
        var invalidConnectionString = "Server=InvalidServer12345;Database=Test;Connection Timeout=1";
        var request = new QueryRequest { Sql = "SELECT 1" };

        // Act
        var response = await _sut.ExecuteQueryAsync(invalidConnectionString, request);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteQueryAsync_WithCancellationRequested_ReturnsCancelledResponse()
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=5";
        var request = new QueryRequest { Sql = "WAITFOR DELAY '00:00:10'" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var response = await _sut.ExecuteQueryAsync(connectionString, request, cts.Token);

        // Assert
        response.Should().NotBeNull();
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().Contain("cancel").And.NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteQueryAsync_ResponseHasCorrectStructure()
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=5";
        var request = new QueryRequest { Sql = "INVALID SQL" };

        // Act
        var response = await _sut.ExecuteQueryAsync(connectionString, request);

        // Assert
        response.Should().BeOfType<QueryResponse>();
        response.Messages.Should().NotBeNull();
        response.ResultSets.Should().NotBeNull();
        response.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Theory]
    [InlineData("SELECT 1; SELECT 2")]
    [InlineData("PRINT 'Test'")]
    public async Task ExecuteQueryAsync_WithValidSql_HandlesCorrectly(string sql)
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=5";
        var request = new QueryRequest { Sql = sql };

        // Act
        var response = await _sut.ExecuteQueryAsync(connectionString, request);

        // Assert
        response.Should().NotBeNull();
        response.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region CancelCurrentQuery Tests

    [Fact]
    public void CancelCurrentQuery_WhenNoQueryRunning_DoesNotThrow()
    {
        // Act
        Action act = () => _sut.CancelCurrentQuery();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void CancelCurrentQuery_CanBeCalledMultipleTimes()
    {
        // Act & Assert
        _sut.CancelCurrentQuery();
        _sut.CancelCurrentQuery();
        _sut.CancelCurrentQuery();
    }

    #endregion

    #region Integration-like Tests

    [Fact]
    public async Task ExecuteQueryAsync_WithMultipleCalls_MaintainsIndependence()
    {
        // Arrange
        var connectionString = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=5";
        var request1 = new QueryRequest { Sql = "SELECT 1" };
        var request2 = new QueryRequest { Sql = "INVALID SQL" };

        // Act
        var response1 = await _sut.ExecuteQueryAsync(connectionString, request1);
        var response2 = await _sut.ExecuteQueryAsync(connectionString, request2);

        // Assert
        response1.Should().NotBeNull();
        response2.Should().NotBeNull();
        response1.Should().NotBeSameAs(response2);
    }

    #endregion
}
