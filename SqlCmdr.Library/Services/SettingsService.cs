using SqlCmdr.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqlCmdr.Abstractions;

namespace SqlCmdr.Services;

public class SettingsService : ISettingsService
{
    readonly IConfiguration _configuration;
    readonly ILogger<SettingsService> _logger;
    readonly object _settingsLock = new();
    AppSettings _currentSettings;

    public SettingsService(IConfiguration configuration, ILogger<SettingsService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentSettings = LoadInitialSettings();
    }

    public Task<AppSettings> GetSettingsAsync()
    {
        lock (_settingsLock)
        {
            return Task.FromResult(_currentSettings);
        }
    }

    public Task<AppSettings> SaveSettingsAsync(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize();
        lock (_settingsLock)
        {
            _currentSettings = normalized;
        }

        _logger.LogInformation("Connection settings updated in process memory for {Server}/{Database}", normalized.Server, normalized.Database);
        return Task.FromResult(normalized);
    }

    AppSettings LoadInitialSettings()
    {
        var connectionString = GetConfiguredConnectionString();
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            try
            {
                return AppSettings.FromConnectionString(connectionString).Normalize();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse configured connection string. SQL Commander will start with empty settings.");
            }
        }

        return new AppSettings().Normalize();
    }

    string? GetConfiguredConnectionString()
    {
        var candidates = new[]
        {
            _configuration.GetConnectionString("db"),
            _configuration["ConnectionStrings:db"],
            _configuration["SQLCMDR_CONNECTION_STRING"],
            _configuration["SQL_CONNECTION_STRING"],
            _configuration["MSSQL_CONNECTION_STRING"],
            _configuration["ConnectionString"]
        };

        return candidates.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }
}
