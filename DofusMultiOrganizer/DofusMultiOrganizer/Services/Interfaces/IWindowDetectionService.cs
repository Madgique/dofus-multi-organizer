using DofusOrganizer.Models;

namespace DofusOrganizer.Services.Interfaces;

public interface IWindowDetectionService
{
    IReadOnlyList<DofusWindowInfo> DetectDofusWindows(DofusMode mode);
}
