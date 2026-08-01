namespace HydraForge.Server.Tests.Realtime;

using HydraForge.Application.Realtime;
using HydraForge.Infrastructure.Realtime;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

public class SignalRProjectBoardEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsToProjectGroup()
    {
        var hubContext = Substitute.For<IHubContext<BoardHub, Infrastructure.Realtime.IBoardHub>>();
        var clients = Substitute.For<IHubClients<Infrastructure.Realtime.IBoardHub>>();
        var group = Substitute.For<Infrastructure.Realtime.IBoardHub>();
        clients.Group(Arg.Any<string>()).Returns(group);
        hubContext.Clients.Returns(clients);

        var publisher = new SignalRProjectBoardEventPublisher(hubContext);

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

        await group.Received(1).OnBoardEvent(envelope);
    }

    [Fact]
    public async Task PublishAsync_UsesCorrectProjectGroup()
    {
        var projectId = Guid.NewGuid();
        var hubContext = Substitute.For<IHubContext<BoardHub, Infrastructure.Realtime.IBoardHub>>();
        var clients = Substitute.For<IHubClients<Infrastructure.Realtime.IBoardHub>>();
        var group = Substitute.For<Infrastructure.Realtime.IBoardHub>();
        clients.Group($"project-{projectId}").Returns(group);
        hubContext.Clients.Returns(clients);

        var publisher = new SignalRProjectBoardEventPublisher(hubContext);

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

        await group.Received(1).OnBoardEvent(Arg.Any<ProjectBoardEventEnvelope>());
    }
}
