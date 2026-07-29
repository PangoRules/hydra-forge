using HydraForge.Domain.Entities.PersonalSpace;
using Xunit;

namespace HydraForge.Domain.Tests.Entities;

public class SystemSettingsTests
{
    [Fact]
    public void UpdateSettings_OnlyUpdatesNonNullArgs()
    {
        var settings = new SystemSettings();
        var originalRetention = settings.ArchivedItemRetentionDays;

        settings.UpdateSettings(ntfyServerUrl: "http://ntfy:80");

        Assert.Equal(originalRetention, settings.ArchivedItemRetentionDays);
        Assert.Equal("http://ntfy:80", settings.NtfyServerUrl);
    }

    [Fact]
    public void UpdateSettings_UpdatesTimestamp()
    {
        var settings = new SystemSettings();
        var before = settings.UpdatedAt;

        System.Threading.Thread.Sleep(10);
        settings.UpdateSettings(brandName: "Test");

        Assert.True(settings.UpdatedAt > before);
    }

    [Fact]
    public void UpdateSettings_NullStringArg_DoesNotChange()
    {
        var settings = new SystemSettings();
        settings.UpdateSettings(ntfyServerUrl: "http://ntfy:80");
        settings.UpdateSettings(ntfyServerUrl: null);

        Assert.Equal("http://ntfy:80", settings.NtfyServerUrl);
    }

    [Fact]
    public void UpdateSettings_BlankStringArg_ClearsToNull()
    {
        var settings = new SystemSettings();
        settings.UpdateSettings(ntfyServerUrl: "http://ntfy:80");
        settings.UpdateSettings(ntfyServerUrl: "");

        Assert.Null(settings.NtfyServerUrl);
    }
}
