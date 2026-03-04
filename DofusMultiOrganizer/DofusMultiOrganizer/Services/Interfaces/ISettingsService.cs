using DofusOrganizer.Models;

namespace DofusOrganizer.Services.Interfaces;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
