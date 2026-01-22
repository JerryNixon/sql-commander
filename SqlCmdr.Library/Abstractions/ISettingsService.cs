using SqlCmdr.Models;

namespace SqlCmdr.Abstractions;

public interface ISettingsService
{
    Task<AppSettings> GetSettingsAsync();
}
