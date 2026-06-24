namespace HydraForge.Server.Tests.Realtime;

using HydraForge.Application.Realtime;
using HydraForge.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using Moq;

public class SignalRProjectBoardEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsToProjectGroup()
    {
        var hubContext = new Mock<IHubContext<BoardHub, HydraForge.Infrastructure.Realtime.IBoardHub>>();
        var clients = new Mock<IHubClients<HydraForge.Infrastructure.Realtime.IBoardHub>>();
        var group = new Mock<HydraForge.Infrastructure.Realtime.IBoardHub>();
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(group.Object);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);

        var publisher = new SignalRProjectBoardEventPublisher(hubContext.Object);

        var envelope = new ProjectBoardEventEnvelope(
            EventId: Guid.NewGuid(),
            ProjectId: Guid.NewGuid(),
            EntityType: BoardEntityType.Card,
            EntityId: Guid.NewGuid(),
            Action: BoardAction.Created,
            Version: 1,
            OccurredAt: DateTime.UtcNow,
            Payload: new { name = "Test Card" }
        );

        await publisher.PublishAsync(envelope);

        group.Verify(g => g.OnBoardEvent(envelope), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_UsesCorrectProjectGroup()
    {
        var projectId = Guid.NewGuid();
        var hubContext = new Mock<IHubContext<BoardHub, HydraForge.Infrastructure.Realtime.IBoardHub>>();
        var clients = new Mock<IHubClients<HydraForge.Infrastructure.Realtime.IBoardHub>>();
        var group = new Mock<HydraForge.Infrastructure.Realtime.IBoardHub>();
        clients.Setup(c => c.Group($"project-{projectId}")).Returns(group.Object);
        hubContext.Setup(h => h.Clients).Returns(clients.Object);

        var publisher = new SignalRProjectBoardEventPublisher(hubContext.Object);

        var envelope = new ProjectBoardEventEnvelope(
            EventId: Guid.NewGuid(),
            ProjectId: projectId,
            EntityType: BoardEntityType.Column,
            EntityId: Guid.NewGuid(),
            Action: BoardAction.Updated,
            Version: 2,
            OccurredAt: DateTime.UtcNow,
            Payload: new { name = "Updated Column" }
        );

        await publisher.PublishAsync(envelope);

        group.Verify(g => g.OnBoardEvent(It.IsAny<ProjectBoardEventEnvelope>()), Times.Once);
    }
}