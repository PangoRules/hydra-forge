namespace HydraForge.Application.Chat;

public interface IChatSearchService
{
    Task<IReadOnlyList<ChatSearchResultDto>> SearchAsync(
        Guid userId,
        string query,
        Guid? projectId = null,
        CancellationToken ct = default
    );
}
