namespace HydraForge.Server.Tests.Realtime;

using HydraForge.Server.Hubs;
using Moq;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using HydraForge.Application.Projects;

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
