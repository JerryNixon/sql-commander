using SqlCmdr.Library.Models;

namespace SqlCmdr.Library.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task DeleteSettingsAsync();
    bool SettingsFileExists();
}
