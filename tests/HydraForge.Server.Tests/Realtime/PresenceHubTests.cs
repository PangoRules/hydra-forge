namespace HydraForge.Server.Tests.Realtime;

using HydraForge.Application.Projects;
using HydraForge.Server.Hubs;
using Moq;

public class PresenceHubTests
{
    [Fact]
    public void PresenceHub_CanBeConstructed()
    {
        var memberRepo = new Mock<IProjectMemberRepository>();
        var hub = new PresenceHub(memberRepo.Object);
        Assert.NotNull(hub);
    }
}
