using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Settings;

public interface ISettingsRepository
{
    Task<SystemSettings> GetSingletonAsync(CancellationToken ct = default);
    Task UpdateAsync(SystemSettings settings, CancellationToken ct = default);
}
