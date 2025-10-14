using SqlCommander.Library.Models;

namespace SqlCommander.Library.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task DeleteSettingsAsync();
    bool SettingsFileExists();
}
