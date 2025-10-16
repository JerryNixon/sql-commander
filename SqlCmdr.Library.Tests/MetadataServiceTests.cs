using SqlCmdr.Library.Services;
using SqlCmdr.Library.Abstractions;
using SqlCmdr.Library.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FluentAssertions;
using AutoFixture;
using AutoFixture.Xunit2;

namespace SqlCmdr.Library.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "MetadataService")]
public class MetadataServiceTests
{
    private readonly IFixture _fixture;
    private readonly Mock<ILogger<MetadataService>> _mockLogger;
    private readonly MetadataService _sut;

    public MetadataServiceTests()
    {
        _fixture = new Fixture();
        _mockLogger = new Mock<ILogger<MetadataService>>();
        _sut = new MetadataService(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new MetadataService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var service = new MetadataService(_mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IMetadataService>();
    }

    #endregion

    #region TestConnectionAsync Tests

    [Fact]
    public async Task TestConnectionAsync_WithNullConnectionString_ThrowsArgumentException()
    {
        // Act
        Func<Task> act = async () => await _sut.TestConnectionAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TestConnectionAsync_WithEmptyConnectionString_ThrowsArgumentException(string connectionString)
    {
        // Act
        Func<Task> act = async () => await _sut.TestConnectionAsync(connectionString);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidConnectionString_ReturnsFailureResult()
    {
        // Arrange
        var invalidConnectionString = "Server=InvalidServer12345;Database=NonExistent;Connection Timeout=1;TrustServerCertificate=True";

        // Act
        var result = await _sut.TestConnectionAsync(invalidConnectionString);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.ServerVersion.Should().BeNull();
        result.DatabaseName.Should().BeNull();
        result.UserName.Should().BeNull();
    }

    [Fact]
    public async Task TestConnectionAsync_WithNonExistentDatabase_ReturnsFailureResult()
    {
        // Arrange
        var connectionString = "Server=.;Database=NonExistentDatabase_12345;Integrated Security=True;TrustServerCertificate=True;Connection Timeout=5";

        // Act
        var result = await _sut.TestConnectionAsync(connectionString);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidCredentials_ReturnsFailureResult()
    {
        // Arrange
        var connectionString = "Server=.;Database=master;User Id=InvalidUser;Password=InvalidPassword;Connection Timeout=5;TrustServerCertificate=True";

        // Act
        var result = await _sut.TestConnectionAsync(connectionString);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TestConnectionAsync_ResultProperties_AreCorrectlyPopulatedOnFailure()
    {
        // Arrange
        var invalidConnectionString = "Server=InvalidServer;Database=Test;Connection Timeout=1";

        // Act
        var result = await _sut.TestConnectionAsync(invalidConnectionString);

        // Assert
        result.Should().BeOfType<ConnectionTestResult>();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region GetMetadataAsync Tests

    [Fact]
    public async Task GetMetadataAsync_WithNullConnectionString_ThrowsArgumentException()
    {
        // Act
        Func<Task> act = async () => await _sut.GetMetadataAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetMetadataAsync_WithEmptyConnectionString_ThrowsArgumentException(string connectionString)
    {
        // Act
        Func<Task> act = async () => await _sut.GetMetadataAsync(connectionString);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("connectionString");
    }

    [Fact]
    public async Task GetMetadataAsync_WithInvalidConnectionString_ThrowsException()
    {
        // Arrange
        var invalidConnectionString = "Server=InvalidServer12345;Database=NonExistent;Connection Timeout=1";

        // Act
        Func<Task> act = async () => await _sut.GetMetadataAsync(invalidConnectionString);

        // Assert
        await act.Should().ThrowAsync<Exception>();
    }

    #endregion
}
