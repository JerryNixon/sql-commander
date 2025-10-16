using SqlCmdr.Models;

namespace SqlCmdr.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task DeleteSettingsAsync();
    bool SettingsFileExists();
}
