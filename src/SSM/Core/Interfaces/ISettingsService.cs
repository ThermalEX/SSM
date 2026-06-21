using SSM.Core.Models;

namespace SSM.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Load();
    void Save();
}
