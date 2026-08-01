namespace HydraForge.Server.Tests.Realtime;

using HydraForge.Application.Projects;
using HydraForge.Server.Hubs;
using NSubstitute;

public class PresenceHubTests
{
    [Fact]
    public void PresenceHub_CanBeConstructed()
    {
        var memberRepo = Substitute.For<IProjectMemberRepository>();
        var hub = new PresenceHub(memberRepo);
        Assert.NotNull(hub);
    }
}
