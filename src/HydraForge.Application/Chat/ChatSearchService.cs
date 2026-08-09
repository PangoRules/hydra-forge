namespace HydraForge.Application.Chat;

public class ChatSearchService : IChatSearchService
{
    private readonly IChatSessionRepository _sessionRepo;
    private readonly IChatMessageRepository _messageRepo;

    private const int SnippetLength = 200;
    private const int MaxResults = 20;

    public ChatSearchService(IChatSessionRepository sessionRepo, IChatMessageRepository messageRepo)
    {
        _sessionRepo = sessionRepo;
        _messageRepo = messageRepo;
    }

    public async Task<IReadOnlyList<ChatSearchResultDto>> SearchAsync(
        Guid userId,
        string query,
        Guid? projectId = null,
        CancellationToken ct = default
    )
    {
        var titleResults = await _sessionRepo.SearchByTitleAsync(
            userId,
            query,
            projectId,
            MaxResults,
            isAdmin: false,
            scope: ChatSessionScope.Mine,
            ct
        );
        var contentResults = await _messageRepo.SearchByContentAsync(
            userId,
            query,
            projectId,
            MaxResults,
            ct
        );

        var seen = new HashSet<Guid>();
        var results = new List<ChatSearchResultDto>();

        foreach (var session in titleResults)
        {
            if (seen.Add(session.Id))
            {
                results.Add(
                    new ChatSearchResultDto(
                        SessionId: session.Id,
                        SessionTitle: session.Title,
                        MatchedOn: "Title",
                        Snippet: null
                    )
                );
            }
        }

        var sessionTitles = titleResults.ToDictionary(s => s.Id, s => s.Title);

        var contentSessionIds = contentResults.Select(m => m.SessionId).Distinct().ToList();
        foreach (var id in contentSessionIds)
        {
            if (!sessionTitles.ContainsKey(id))
            {
                var session = await _sessionRepo.GetByIdAsync(id, ct);
                if (session != null)
                    sessionTitles[id] = session.Title;
            }
        }

        foreach (var message in contentResults)
        {
            if (seen.Add(message.SessionId))
            {
                var snippet = BuildSnippet(message.Content, query);
                results.Add(
                    new ChatSearchResultDto(
                        SessionId: message.SessionId,
                        SessionTitle: sessionTitles.GetValueOrDefault(
                            message.SessionId,
                            string.Empty
                        ),
                        MatchedOn: "Content",
                        Snippet: snippet
                    )
                );
            }
        }

        return results.Take(MaxResults).ToList();
    }

    private static string BuildSnippet(string content, string query)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var index = content.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return content.Length <= SnippetLength ? content : content[..SnippetLength] + "…";

        var start = Math.Max(0, index - SnippetLength / 2);
        var end = Math.Min(content.Length, start + SnippetLength);
        if (end == content.Length && start > 0)
            start = Math.Max(0, end - SnippetLength);

        var snippet = content[start..end];
        if (start > 0)
            snippet = "…" + snippet;
        if (end < content.Length)
            snippet += "…";

        return snippet;
    }
}
