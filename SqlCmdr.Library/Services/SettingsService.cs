using SqlCmdr.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqlCmdr.Abstractions;

namespace SqlCmdr.Services;

public class SettingsService : ISettingsService
{
    readonly IConfiguration _configuration;
    readonly ILogger<SettingsService> _logger;

    public SettingsService(IConfiguration configuration, ILogger<SettingsService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<AppSettings> GetSettingsAsync()
    {
        var connectionString = _configuration.GetConnectionString("db");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return Task.FromResult(AppSettings.FromConnectionString(connectionString));
        }
        return Task.FromResult(new AppSettings());
    }
}
