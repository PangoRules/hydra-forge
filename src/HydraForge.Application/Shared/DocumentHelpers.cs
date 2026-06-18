namespace HydraForge.Application.Shared;

public static class DocumentMarkdownLimits
{
    public const int MaxMarkdownPayloadBytes = 1_000_000;
}

public static class VersionAppender
{
    public static TSpecVersion CreateVersion<TSpecVersion>(
        Guid specId,
        int version,
        string content,
        Guid actorId,
        Func<Guid, int, string, Guid, DateTime, TSpecVersion> factory)
        where TSpecVersion : class
    {
        return factory(specId, version, content, actorId, DateTime.UtcNow);
    }
}
