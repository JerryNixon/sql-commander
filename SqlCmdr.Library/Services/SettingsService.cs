using System.Text.Json;
using SqlCmdr.Models;
using SqlCmdr.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SqlCmdr.Abstractions;

namespace SqlCmdr.Services;

public class SettingsService : ISettingsService
{
    private const string SettingsFileName = "sqlcmdr.settings.json";
    readonly IConfiguration _configuration;
    readonly ILogger<SettingsService> _logger;
    readonly string _settingsFilePath;

    public SettingsService(IConfiguration configuration, ILogger<SettingsService> logger, IHostEnvironment environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }
        
        _settingsFilePath = Path.Combine(environment.ContentRootPath, SettingsFileName);
    }

    public async Task<AppSettings> GetSettingsAsync()
    {
        if (SettingsFileExists())
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions.Default);
                if (settings is not null)
                {
                    return settings;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to load settings from file");
            }
        }
        var connectionString = _configuration.GetConnectionString("db");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return AppSettings.FromConnectionString(connectionString);
        }
        return new AppSettings();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var json = JsonSerializer.Serialize(settings, JsonOptions.Indented);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }

    public Task DeleteSettingsAsync()
    {
        if (File.Exists(_settingsFilePath))
        {
            File.Delete(_settingsFilePath);
        }
        return Task.CompletedTask;
    }

    public bool SettingsFileExists() => File.Exists(_settingsFilePath);
}
