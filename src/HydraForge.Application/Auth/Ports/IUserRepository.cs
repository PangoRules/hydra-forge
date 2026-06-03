namespace HydraForge.Application.Auth.Ports;

public interface IUserRepository
{
    Task<HydraForge.Domain.Entities.Auth.User?> FindByUsernameAsync(string username);
    Task UpdateLastLoginAsync(Guid userId, DateTime loginAt);
    Task<bool> AnyAdminExistsAsync();
    Task CreateAsync(HydraForge.Domain.Entities.Auth.User user);
}