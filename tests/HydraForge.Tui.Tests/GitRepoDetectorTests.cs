using HydraForge.Tui.Services;

namespace HydraForge.Tui.Tests;

public class GitRepoDetectorTests
{
    [Fact]
    public void Normalize_HttpsUrl_StripsSchemeAndGitSuffix()
    {
        Assert.Equal(
            "github.com/acme/widgets",
            GitRepoDetector.Normalize("https://github.com/acme/widgets.git")
        );
    }

    [Fact]
    public void Normalize_SshUrl_ConvertsToSameShapeAsHttps()
    {
        Assert.Equal(
            "github.com/acme/widgets",
            GitRepoDetector.Normalize("git@github.com:acme/widgets.git")
        );
    }

    [Fact]
    public void Normalize_HttpsWithoutGitSuffix_MatchesWithSuffix()
    {
        Assert.Equal(
            GitRepoDetector.Normalize("https://github.com/acme/widgets.git"),
            GitRepoDetector.Normalize("https://github.com/acme/widgets")
        );
    }

    [Fact]
    public void Normalize_UrlWithEmbeddedCredentials_IgnoresUserInfo()
    {
        Assert.Equal(
            "github.com/acme/widgets",
            GitRepoDetector.Normalize("https://user:token@github.com/acme/widgets.git")
        );
    }

    [Fact]
    public void Normalize_CaseInsensitive()
    {
        Assert.Equal(
            GitRepoDetector.Normalize("https://GitHub.com/Acme/Widgets.git"),
            GitRepoDetector.Normalize("https://github.com/acme/widgets.git")
        );
    }

    [Fact]
    public void Normalize_Null_ReturnsNull()
    {
        Assert.Null(GitRepoDetector.Normalize(null));
        Assert.Null(GitRepoDetector.Normalize("  "));
    }

    [Fact]
    public void Matches_SshAndHttpsForSameRepo_ReturnsTrue()
    {
        Assert.True(
            GitRepoDetector.Matches(
                "git@github.com:acme/widgets.git",
                "https://github.com/acme/widgets"
            )
        );
    }

    [Fact]
    public void Matches_DifferentRepos_ReturnsFalse()
    {
        Assert.False(
            GitRepoDetector.Matches(
                "https://github.com/acme/widgets.git",
                "https://github.com/acme/gadgets.git"
            )
        );
    }

    [Fact]
    public void Matches_EitherUrlNull_ReturnsFalse()
    {
        Assert.False(GitRepoDetector.Matches(null, "https://github.com/acme/widgets.git"));
        Assert.False(GitRepoDetector.Matches("https://github.com/acme/widgets.git", null));
    }

    [Fact]
    public void FindRepoRoot_WalksUpToGitDirectory()
    {
        var root = Directory.CreateTempSubdirectory("hydraforge-git-test-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "a", "b", "c"));

            var found = GitRepoDetector.FindRepoRoot(nested.FullName);

            Assert.Equal(root.FullName, found);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindRepoRoot_NoGitDirectoryAnywhereUp_ReturnsNull()
    {
        var root = Directory.CreateTempSubdirectory("hydraforge-git-test-nogit-");
        try
        {
            // A temp dir's ancestry (e.g. /tmp) is not expected to contain a .git
            // directory in any sane CI/dev environment.
            var found = GitRepoDetector.FindRepoRoot(root.FullName);
            Assert.Null(found);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetOriginRemoteUrl_ParsesUrlFromGitConfig()
    {
        var root = Directory.CreateTempSubdirectory("hydraforge-git-test-config-");
        try
        {
            var gitDir = Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            File.WriteAllText(
                Path.Combine(gitDir.FullName, "config"),
                """
                [core]
                	repositoryformatversion = 0
                [remote "origin"]
                	url = https://github.com/acme/widgets.git
                	fetch = +refs/heads/*:refs/remotes/origin/*
                [branch "main"]
                	remote = origin
                """
            );

            var url = GitRepoDetector.GetOriginRemoteUrl(root.FullName);

            Assert.Equal("https://github.com/acme/widgets.git", url);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void GetOriginRemoteUrl_NoConfigFile_ReturnsNull()
    {
        var root = Directory.CreateTempSubdirectory("hydraforge-git-test-noconfig-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            Assert.Null(GitRepoDetector.GetOriginRemoteUrl(root.FullName));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
