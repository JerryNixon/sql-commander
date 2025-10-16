using SqlCmdr.Library.Models;
using SqlCmdr.Library.Services;
using SqlCmdr.Library.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using FluentAssertions;
using AutoFixture;

namespace SqlCmdr.Library.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "SettingsService")]
public class SettingsServiceTests : IDisposable
{
    private readonly IFixture _fixture;
    private readonly Mock<ILogger<SettingsService>> _mockLogger;
    private readonly Mock<IWebHostEnvironment> _mockEnvironment;
    private readonly string _tempPath;
    private readonly IConfiguration _configuration;

    public SettingsServiceTests()
    {
        _fixture = new Fixture();
        _mockLogger = new Mock<ILogger<SettingsService>>();
        _mockEnvironment = new Mock<IWebHostEnvironment>();
        _tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempPath);
        _mockEnvironment.Setup(e => e.ContentRootPath).Returns(_tempPath);
        _configuration = new ConfigurationBuilder().Build();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            try
            {
                Directory.Delete(_tempPath, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new SettingsService(null!, _mockLogger.Object, _mockEnvironment.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new SettingsService(_configuration, null!, _mockEnvironment.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullEnvironment_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new SettingsService(_configuration, _mockLogger.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("environment");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<ISettingsService>();
    }

    #endregion

    #region GetSettingsAsync Tests

    [Fact]
    public async Task GetSettingsAsync_WhenNoSettingsExist_ReturnsDefaultSettings()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);

        // Act
        var settings = await service.GetSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.DefaultResultLimit.Should().Be(100);
        settings.Server.Should().BeEmpty();
        settings.Database.Should().BeEmpty();
        settings.UserId.Should().BeEmpty();
        settings.Password.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsAppSettingsType()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);

        // Act
        var settings = await service.GetSettingsAsync();

        // Assert
        settings.Should().BeOfType<AppSettings>();
    }

    [Fact]
    public async Task GetSettingsAsync_DefaultSettings_HaveExpectedValues()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);

        // Act
        var settings = await service.GetSettingsAsync();

        // Assert
        settings.DefaultResultLimit.Should().Be(100);
        settings.TrustServerCertificate.Should().BeTrue();
        settings.ConnectionTimeout.Should().Be(30);
        settings.ConfirmActions.Should().BeFalse();
        settings.Theme.Should().Be("dark");
    }

    #endregion

    #region SaveSettingsAsync Tests

    [Fact]
    public async Task SaveSettingsAsync_WithNullSettings_ThrowsArgumentNullException()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);

        // Act
        Func<Task> act = async () => await service.SaveSettingsAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("settings");
    }

    [Fact]
    public async Task SaveSettingsAsync_WithValidSettings_CreatesFile()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);
        var settings = _fixture.Build<AppSettings>()
            .With(s => s.Server, "TestServer")
            .With(s => s.Database, "TestDb")
            .Create();

        // Act
        await service.SaveSettingsAsync(settings);

        // Assert
        service.SettingsFileExists().Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_ThenGetSettings_RoundTripsCorrectly()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);
        var original = new AppSettings
        {
            Server = "TestServer",
            Database = "TestDatabase",
            UserId = "TestUser",
            Password = "TestPassword",
            DefaultResultLimit = 50,
            TrustServerCertificate = false,
            ConnectionTimeout = 60,
            ConfirmActions = true,
            Theme = "light"
        };

        // Act
        await service.SaveSettingsAsync(original);
        var loaded = await service.GetSettingsAsync();

        // Assert
        loaded.Should().BeEquivalentTo(original);
    }

    [Fact]
    public async Task SaveSettingsAsync_WithPartialSettings_SavesCorrectly()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);
        var settings = new AppSettings
        {
            Server = "PartialServer",
            Database = "PartialDb",
            DefaultResultLimit = 25
        };

        // Act
        await service.SaveSettingsAsync(settings);
        var loaded = await service.GetSettingsAsync();

        // Assert
        loaded.Server.Should().Be("PartialServer");
        loaded.Database.Should().Be("PartialDb");
        loaded.DefaultResultLimit.Should().Be(25);
    }

    [Fact]
    public async Task SaveSettingsAsync_MultipleWrites_OverwritesPrevious()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);
        var settings1 = new AppSettings { Server = "Server1", Database = "Db1" };
        var settings2 = new AppSettings { Server = "Server2", Database = "Db2" };

        // Act
        await service.SaveSettingsAsync(settings1);
        await service.SaveSettingsAsync(settings2);
        var loaded = await service.GetSettingsAsync();

        // Assert
        loaded.Server.Should().Be("Server2");
        loaded.Database.Should().Be("Db2");
    }

    [Fact]
    public async Task SaveSettingsAsync_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);
        var settings = new AppSettings
        {
            Server = "Server\\Instance",
            Database = "Test-DB_123",
            UserId = "domain\\user",
            Password = "P@$$w0rd!"
        };

        // Act
        await service.SaveSettingsAsync(settings);
        var loaded = await service.GetSettingsAsync();

        // Assert
        loaded.Server.Should().Be("Server\\Instance");
        loaded.Database.Should().Be("Test-DB_123");
        loaded.UserId.Should().Be("domain\\user");
        loaded.Password.Should().Be("P@$$w0rd!");
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public async Task SaveAndGetSettings_WithVariousResultLimits_RoundTripsCorrectly(int resultLimit)
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);
        var settings = new AppSettings { DefaultResultLimit = resultLimit };

        // Act
        await service.SaveSettingsAsync(settings);
        var loaded = await service.GetSettingsAsync();

        // Assert
        loaded.DefaultResultLimit.Should().Be(resultLimit);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAndGetSettings_WithEmptyStrings_HandlesCorrectly(string emptyValue)
    {
        // Arrange
        var service = new SettingsService(_configuration, _mockLogger.Object, _mockEnvironment.Object);
        var settings = new AppSettings
        {
            Server = emptyValue,
            Database = emptyValue
        };

        // Act
        await service.SaveSettingsAsync(settings);
        var loaded = await service.GetSettingsAsync();

        // Assert
        loaded.Server.Should().Be(emptyValue);
        loaded.Database.Should().Be(emptyValue);
    }

    #endregion
}
