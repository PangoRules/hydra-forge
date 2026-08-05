using System.Text.RegularExpressions;

namespace HydraForge.Tui.Services;

/// <summary>
/// Local filesystem/git-config inspection backing the "export doc to repo" flow —
/// confirms the directory the TUI was launched from is actually a checkout of the
/// project it's exporting for (by comparing git remotes) before writing files into it.
/// </summary>
public static class GitRepoDetector
{
    // Walks up from startDir looking for a .git directory, same as git itself does
    // to find the repo root from any subdirectory.
    public static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    public static string? GetOriginRemoteUrl(string repoRoot)
    {
        var configPath = Path.Combine(repoRoot, ".git", "config");
        if (!File.Exists(configPath))
            return null;

        string? currentSection = null;
        foreach (var rawLine in File.ReadAllLines(configPath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = line[1..^1].Trim();
                continue;
            }

            if (
                currentSection == "remote \"origin\""
                && line.StartsWith("url", StringComparison.OrdinalIgnoreCase)
            )
            {
                var eq = line.IndexOf('=');
                if (eq >= 0)
                    return line[(eq + 1)..].Trim();
            }
        }

        return null;
    }

    // Normalizes SSH (git@host:owner/repo.git) and HTTPS (https://host/owner/repo.git)
    // forms to a common "host/owner/repo" shape — same repo, different clone protocol,
    // should still compare equal.
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        var trimmed = url.Trim();
        string hostAndPath;

        if (trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            hostAndPath = trimmed["git@".Length..].Replace(':', '/');
        }
        else
        {
            var withoutScheme = Regex.Replace(trimmed, "^[a-zA-Z]+://", "");
            var at = withoutScheme.IndexOf('@');
            hostAndPath = at >= 0 ? withoutScheme[(at + 1)..] : withoutScheme;
        }

        hostAndPath = hostAndPath.TrimEnd('/');
        if (hostAndPath.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            hostAndPath = hostAndPath[..^4];

        return hostAndPath.ToLowerInvariant();
    }

    public static bool Matches(string? localUrl, string? configuredUrl)
    {
        var a = Normalize(localUrl);
        var b = Normalize(configuredUrl);
        return a != null && b != null && a == b;
    }
}
