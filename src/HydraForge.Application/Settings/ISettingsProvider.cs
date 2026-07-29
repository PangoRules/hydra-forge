using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Settings;

public interface ISettingsProvider
{
    Task<SystemSettings> GetAsync(CancellationToken ct = default);
}
