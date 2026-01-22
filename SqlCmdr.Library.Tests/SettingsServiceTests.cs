using SqlCmdr.Models;
using SqlCmdr.Services;
using SqlCmdr.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;

namespace SqlCmdr.Tests;

[Trait("Category", "Unit")]
[Trait("Service", "SettingsService")]
public class SettingsServiceTests
{
    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        Action act = () => new SettingsService(null!, NullLogger<SettingsService>.Instance);
        act.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var config = new ConfigurationBuilder().Build();
        Action act = () => new SettingsService(config, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new SettingsService(config, NullLogger<SettingsService>.Instance);
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<ISettingsService>();
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNoConnectionString_ReturnsDefaultSettings()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var settings = await service.GetSettingsAsync();
        settings.Should().NotBeNull();
        settings.DefaultResultLimit.Should().Be(100);
        settings.Server.Should().BeEmpty();
        settings.Database.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSettingsAsync_WithConnectionString_ParsesSettings()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:db"] = "Server=TestServer;Database=TestDb;User Id=TestUser;Password=TestPass;TrustServerCertificate=true"
            })
            .Build();
        var service = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var settings = await service.GetSettingsAsync();
        settings.Server.Should().Be("TestServer");
        settings.Database.Should().Be("TestDb");
        settings.UserId.Should().Be("TestUser");
    }

    [Fact]
    public async Task GetSettingsAsync_DefaultSettings_HaveExpectedValues()
    {
        var config = new ConfigurationBuilder().Build();
        var service = new SettingsService(config, NullLogger<SettingsService>.Instance);
        var settings = await service.GetSettingsAsync();
        settings.DefaultResultLimit.Should().Be(100);
        settings.TrustServerCertificate.Should().BeTrue();
        settings.ConnectionTimeout.Should().Be(30);
        settings.ConfirmActions.Should().BeFalse();
        settings.Theme.Should().Be("dark");
    }
}
