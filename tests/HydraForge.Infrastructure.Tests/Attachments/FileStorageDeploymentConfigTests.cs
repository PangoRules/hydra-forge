namespace HydraForge.Infrastructure.Tests.Attachments;

public class FileStorageDeploymentConfigTests
{
    [Fact]
    public void EnvironmentExample_UsesProjectLocalIgnoredAttachmentPathForHostRun()
    {
        var root = FindRepoRoot();
        var envExample = File.ReadAllText(Path.Combine(root, ".env.example"));
        var gitignore = File.ReadAllText(Path.Combine(root, ".gitignore"));

        Assert.Contains("FileStorage__LocalPath=.hydraforge/attachments", envExample);
        Assert.Contains(".hydraforge/", gitignore);
    }

    [Fact]
    public void DockerCompose_MountsLocalAttachmentStorageAsNamedVolume()
    {
        var root = FindRepoRoot();
        var compose = File.ReadAllText(Path.Combine(root, "docker-compose.yml"));

        Assert.Contains("FileStorage__LocalPath: /data/hydraforge/attachments", compose);
        Assert.Contains("- hydraforge-attachments:/data/hydraforge/attachments", compose);
        Assert.Contains("hydraforge-attachments:", compose);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "HydraForge.slnx")))
            directory = directory.Parent;

        if (directory == null)
            throw new InvalidOperationException("Could not find repository root.");

        return directory.FullName;
    }
}
