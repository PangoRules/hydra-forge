namespace HydraForge.Infrastructure.Chat;

using HydraForge.Application.Chat;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Infrastructure.Persistence;

// Shared "which sessions can actorId see under this scope" predicate — used by both
// EfChatSessionRepository (session list/search-by-title) and EfChatMessageRepository
// (search-by-content). A single copy so a future change to the participated-in rule
// (e.g. a new membership condition) can't apply to one query and drift from the other.
internal static class ChatScopeQueries
{
    // scope == Mine: strictly the caller's own sessions, regardless of admin status —
    // a personal history view is personal even for an admin. scope == Participated
    // grants project members visibility, with an admin bypass (admins see every
    // session regardless of membership).
    public static IQueryable<ChatSession> ApplyScope(
        HydraForgeDbContext context,
        IQueryable<ChatSession> q,
        Guid actorId,
        bool isAdmin,
        ChatSessionScope scope
    ) =>
        scope switch
        {
            ChatSessionScope.Participated => isAdmin ? q : WhereParticipatedIn(context, q, actorId),
            _ => q.Where(s => s.OwnerId == actorId),
        };

    // Participated-in = owner OR project member (for project-scoped sessions)
    public static IQueryable<ChatSession> WhereParticipatedIn(
        HydraForgeDbContext context,
        IQueryable<ChatSession> q,
        Guid actorId
    ) =>
        q.Where(s =>
            s.OwnerId == actorId
            || (
                s.ProjectId != null
                && context.ProjectMembers.Any(m =>
                    m.ProjectId == s.ProjectId && m.UserId == actorId
                )
            )
        );
}
