using System.Text.RegularExpressions;

namespace HydraForge.Application.Comments;

public static partial class MentionExtractor
{
    private static readonly Regex MentionRegex = GetMentionRegex();

    [GeneratedRegex(@"(?<!\w)@([A-Za-z0-9_.-]{1,64})")]
    private static partial Regex GetMentionRegex();

    public static IReadOnlyList<string> Extract(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var matches = MentionRegex.Matches(content);
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            usernames.Add(match.Groups[1].Value);
        }

        return [.. usernames];
    }
}