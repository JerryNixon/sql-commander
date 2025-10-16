using SqlCmdr.Models;
using FluentAssertions;
using AutoFixture;

namespace SqlCmdr.Tests;

[Trait("Category", "Integration")]
[Trait("Component", "Models")]
public class QueryModelsTests
{
    private readonly IFixture _fixture;

    public QueryModelsTests()
    {
        _fixture = new Fixture();
    }

    #region QueryRequest Tests

    [Fact]
    public void QueryRequest_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var request = new QueryRequest();

        // Assert
        request.Sql.Should().BeEmpty();
        request.ResultLimit.Should().BeNull();
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void QueryRequest_WithResultLimit_StoresCorrectly(int limit)
    {
        // Arrange & Act
        var request = new QueryRequest { ResultLimit = limit };

        // Assert
        request.ResultLimit.Should().Be(limit);
    }

    [Fact]
    public void QueryRequest_WithSql_StoresCorrectly()
    {
        // Arrange
        var sql = "SELECT * FROM Users WHERE Id = 1";

        // Act
        var request = new QueryRequest { Sql = sql };

        // Assert
        request.Sql.Should().Be(sql);
    }

    [Fact]
    public void QueryRequest_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var request1 = new QueryRequest { Sql = "SELECT 1", ResultLimit = 100 };
        var request2 = new QueryRequest { Sql = "SELECT 1", ResultLimit = 100 };

        // Assert
        request1.Should().Be(request2);
    }

    #endregion

    #region QueryResponse Tests

    [Fact]
    public void QueryResponse_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var response = new QueryResponse();

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().BeNull();
        response.Messages.Should().NotBeNull().And.BeEmpty();
        response.ResultSets.Should().NotBeNull().And.BeEmpty();
        response.ElapsedMilliseconds.Should().Be(0);
        response.TotalRowsReturned.Should().Be(0);
        response.WasTruncated.Should().BeFalse();
    }

    [Fact]
    public void QueryResponse_WithSuccess_HasCorrectProperties()
    {
        // Arrange & Act
        var response = new QueryResponse
        {
            Success = true,
            ElapsedMilliseconds = 150,
            TotalRowsReturned = 42
        };
        response.ResultSetsInternal.Add(new ResultSet { RowCount = 42 });

        // Assert
        response.Success.Should().BeTrue();
        response.ErrorMessage.Should().BeNull();
        response.ElapsedMilliseconds.Should().Be(150);
        response.TotalRowsReturned.Should().Be(42);
        response.ResultSets.Should().HaveCount(1);
    }

    [Fact]
    public void QueryResponse_WithError_HasErrorMessage()
    {
        // Arrange & Act
        var response = new QueryResponse
        {
            Success = false,
            ErrorMessage = "Invalid object name 'Users'."
        };

        // Assert
        response.Success.Should().BeFalse();
        response.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void QueryResponse_WithMultipleResultSets_StoresCorrectly()
    {
        // Arrange
        var response = new QueryResponse
        {
            Success = true,
            TotalRowsReturned = 60
        };
        response.ResultSetsInternal.Add(new ResultSet { RowCount = 10 });
        response.ResultSetsInternal.Add(new ResultSet { RowCount = 20 });
        response.ResultSetsInternal.Add(new ResultSet { RowCount = 30 });

        // Assert
        response.ResultSets.Should().HaveCount(3);
        response.TotalRowsReturned.Should().Be(60);
    }

    [Fact]
    public void QueryResponse_WithTruncation_FlagsCorrectly()
    {
        // Arrange & Act
        var response = new QueryResponse
        {
            Success = true,
            WasTruncated = true,
            TotalRowsReturned = 100
        };

        // Assert
        response.WasTruncated.Should().BeTrue();
    }

    #endregion

    #region ResultSet Tests

    [Fact]
    public void ResultSet_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var resultSet = new ResultSet();

        // Assert
        resultSet.Columns.Should().NotBeNull().And.BeEmpty();
        resultSet.Rows.Should().NotBeNull().And.BeEmpty();
        resultSet.RowCount.Should().Be(0);
    }

    [Fact]
    public void ResultSet_WithData_StoresCorrectly()
    {
        // Arrange
        var columns = new List<string> { "Id", "Name", "Email" };
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["Id"] = 1, ["Name"] = "John", ["Email"] = "john@example.com" },
            new() { ["Id"] = 2, ["Name"] = "Jane", ["Email"] = "jane@example.com" }
        };

        // Act
        var resultSet = new ResultSet { RowCount = rows.Count };
        resultSet.ColumnsInternal.AddRange(columns);
        resultSet.RowsInternal.AddRange(rows);

        // Assert
        resultSet.Columns.Should().HaveCount(3);
        resultSet.Rows.Should().HaveCount(2);
        resultSet.RowCount.Should().Be(2);
    }

    [Fact]
    public void ResultSet_WithNullValues_HandlesCorrectly()
    {
        // Arrange
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["Id"] = 1, ["Name"] = null, ["Email"] = null }
        };

        // Act
        var resultSet = new ResultSet { RowCount = 1 };
        resultSet.RowsInternal.AddRange(rows);

        // Assert
        resultSet.Rows[0]["Name"].Should().BeNull();
        resultSet.Rows[0]["Email"].Should().BeNull();
    }

    #endregion

    #region ConnectionTestResult Tests

    [Fact]
    public void ConnectionTestResult_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var result = new ConnectionTestResult();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.ServerVersion.Should().BeNull();
        result.DatabaseName.Should().BeNull();
        result.UserName.Should().BeNull();
    }

    [Fact]
    public void ConnectionTestResult_WithSuccess_HasCorrectProperties()
    {
        // Arrange & Act
        var result = new ConnectionTestResult
        {
            Success = true,
            ServerVersion = "16.0.1000.6",
            DatabaseName = "master",
            UserName = "sa"
        };

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ServerVersion.Should().NotBeNullOrEmpty();
        result.DatabaseName.Should().Be("master");
        result.UserName.Should().Be("sa");
    }

    [Fact]
    public void ConnectionTestResult_WithFailure_HasErrorMessage()
    {
        // Arrange & Act
        var result = new ConnectionTestResult
        {
            Success = false,
            ErrorMessage = "Login failed for user 'test'."
        };

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.ServerVersion.Should().BeNull();
    }

    #endregion

    #region StatusUpdate Tests

    [Theory]
    [InlineData(QueryState.Idle)]
    [InlineData(QueryState.Running)]
    [InlineData(QueryState.Cancelling)]
    [InlineData(QueryState.Cancelled)]
    [InlineData(QueryState.Completed)]
    [InlineData(QueryState.Failed)]
    public void StatusUpdate_WithDifferentStates_StoresCorrectly(QueryState state)
    {
        // Arrange & Act
        var status = new StatusUpdate { State = state };

        // Assert
        status.State.Should().Be(state);
    }

    [Fact]
    public void StatusUpdate_WithCompleteData_StoresCorrectly()
    {
        // Arrange & Act
        var status = new StatusUpdate
        {
            State = QueryState.Running,
            ElapsedMilliseconds = 5000,
            RowCount = 150,
            WasTruncated = false
        };

        // Assert
        status.State.Should().Be(QueryState.Running);
        status.ElapsedMilliseconds.Should().Be(5000);
        status.RowCount.Should().Be(150);
        status.WasTruncated.Should().BeFalse();
    }

    [Fact]
    public void StatusUpdate_WithError_HasErrorMessage()
    {
        // Arrange & Act
        var status = new StatusUpdate
        {
            State = QueryState.Failed,
            ErrorMessage = "Connection timeout expired"
        };

        // Assert
        status.State.Should().Be(QueryState.Failed);
        status.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region QueryState Enum Tests

    [Fact]
    public void QueryState_AllValues_AreDefined()
    {
        // Assert
        Enum.IsDefined(typeof(QueryState), QueryState.Idle).Should().BeTrue();
        Enum.IsDefined(typeof(QueryState), QueryState.Running).Should().BeTrue();
        Enum.IsDefined(typeof(QueryState), QueryState.Cancelling).Should().BeTrue();
        Enum.IsDefined(typeof(QueryState), QueryState.Cancelled).Should().BeTrue();
        Enum.IsDefined(typeof(QueryState), QueryState.Completed).Should().BeTrue();
        Enum.IsDefined(typeof(QueryState), QueryState.Failed).Should().BeTrue();
    }

    [Fact]
    public void QueryState_EnumValues_HaveExpectedCount()
    {
        // Act
        var values = Enum.GetValues(typeof(QueryState));

        // Assert
        values.Length.Should().Be(6);
    }

    #endregion
}
