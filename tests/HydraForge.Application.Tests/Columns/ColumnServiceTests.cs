using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Columns;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Tests.Columns;

public class ColumnServiceTests
{
    private static Guid NewId() => Guid.NewGuid();

    [Fact]
    public async Task CreateAsync_AppendsAtMaxPosition()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        repo.Columns.Add(new Column { Id = NewId(), ProjectId = projectId, Name = "Backlog", Position = 0 });
        repo.Columns.Add(new Column { Id = NewId(), ProjectId = projectId, Name = "Done", Position = 1 });
        memberRepo.Members.Add(new ProjectMember { Id = NewId(), ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.CreateAsync(new CreateColumnCommand(projectId, "In Progress", null, null, actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Position);
    }

    [Fact]
    public async Task CreateAsync_NonMember_ReturnsMembershipDenied()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var nonMemberId = NewId();

        var result = await service.CreateAsync(new CreateColumnCommand(projectId, "New", null, null, nonMemberId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingColumn_ReturnsColumn()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        repo.Columns.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.GetByIdAsync(projectId, columnId, actorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Backlog", result.Value.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistent_ReturnsNotFound()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.GetByIdAsync(projectId, NewId(), actorId);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Columns.NotFound, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_ChangesNameColorWip()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        repo.Columns.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UpdateAsync(new UpdateColumnCommand(projectId, columnId, "In Progress", "#FF0000", 5, actorId));

        Assert.True(result.IsSuccess);
        Assert.Equal("In Progress", result.Value.Name);
        Assert.Equal("#FF0000", result.Value.Color);
        Assert.Equal(5, result.Value.WipLimit);
    }

    [Fact]
    public async Task DeleteAsync_EmptyColumn_CompactsPositions()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId1 = NewId();
        var columnId2 = NewId();
        var columnId3 = NewId();

        repo.Columns.Add(new Column { Id = columnId1, ProjectId = projectId, Name = "Backlog", Position = 0 });
        repo.Columns.Add(new Column { Id = columnId2, ProjectId = projectId, Name = "In Dev", Position = 1 });
        repo.Columns.Add(new Column { Id = columnId3, ProjectId = projectId, Name = "Done", Position = 2 });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.DeleteAsync(new DeleteColumnCommand(projectId, columnId2, actorId));

        Assert.True(result.IsSuccess);
        var remaining = repo.Columns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToList();
        Assert.Equal(2, remaining.Count);
        Assert.Equal(0, remaining[0].Position);
        Assert.Equal(1, remaining[1].Position);
    }

    [Fact]
    public async Task DeleteAsync_NonEmptyColumn_ReturnsDeleteNonEmpty()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        repo.Columns.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        cardRepo.Cards.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, Title = "Task 1" });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.DeleteAsync(new DeleteColumnCommand(projectId, columnId, actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Columns.DeleteNonEmpty, result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_ArchivedCard_DoesNotBlockDelete()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        repo.Columns.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        cardRepo.Cards.Add(new Card { Id = NewId(), ProjectId = projectId, ColumnId = columnId, Title = "Archived Task", ArchivedAt = DateTime.UtcNow });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.DeleteAsync(new DeleteColumnCommand(projectId, columnId, actorId));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ReorderAsync_ValidColumnIds_RewritesDensePositions()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var col1 = new Column { Id = NewId(), ProjectId = projectId, Name = "Backlog", Position = 0 };
        var col2 = new Column { Id = NewId(), ProjectId = projectId, Name = "In Dev", Position = 1 };
        var col3 = new Column { Id = NewId(), ProjectId = projectId, Name = "Done", Position = 2 };
        repo.Columns.Add(col1);
        repo.Columns.Add(col2);
        repo.Columns.Add(col3);
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.ReorderAsync(new ReorderColumnsCommand(projectId, [col3.Id, col1.Id, col2.Id], actorId));

        Assert.True(result.IsSuccess);
        var reordered = repo.Columns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToList();
        Assert.Equal(col3.Id, reordered[0].Id);
        Assert.Equal(0, reordered[0].Position);
        Assert.Equal(col1.Id, reordered[1].Id);
        Assert.Equal(1, reordered[1].Position);
        Assert.Equal(col2.Id, reordered[2].Id);
        Assert.Equal(2, reordered[2].Position);
    }

    [Fact]
    public async Task ReorderAsync_InvalidColumnId_ReturnsInvalidPosition()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var col1 = new Column { Id = NewId(), ProjectId = projectId, Name = "Backlog", Position = 0 };
        repo.Columns.Add(col1);
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.ReorderAsync(new ReorderColumnsCommand(projectId, [col1.Id, NewId()], actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Columns.InvalidPosition, result.Error.Code);
    }

    [Fact]
    public async Task ReorderAsync_WrongProjectColumnId_ReturnsInvalidPosition()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var otherProjectId = NewId();
        var col1 = new Column { Id = NewId(), ProjectId = projectId, Name = "Backlog", Position = 0 };
        var colOther = new Column { Id = NewId(), ProjectId = otherProjectId, Name = "Other", Position = 0 };
        repo.Columns.Add(col1);
        repo.Columns.Add(colOther);
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });
        memberRepo.Members.Add(new ProjectMember { ProjectId = otherProjectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.ReorderAsync(new ReorderColumnsCommand(projectId, [col1.Id, colOther.Id], actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Columns.InvalidPosition, result.Error.Code);
    }

    [Fact]
    public async Task ReorderAsync_MissingColumn_ReturnsInvalidPosition()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var col1 = new Column { Id = NewId(), ProjectId = projectId, Name = "Backlog", Position = 0 };
        repo.Columns.Add(col1);
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.ReorderAsync(new ReorderColumnsCommand(projectId, [col1.Id, NewId()], actorId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Columns.InvalidPosition, result.Error.Code);
    }

    [Fact]
    public async Task ReorderAsync_NonMember_ReturnsMembershipDenied()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var nonMemberId = NewId();

        var result = await service.ReorderAsync(new ReorderColumnsCommand(projectId, [], nonMemberId));

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Projects.MembershipDenied, result.Error.Code);
    }

    // ─── Audit tests ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();

        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.CreateAsync(new CreateColumnCommand(projectId, "New Column", null, null, actorId));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Column", req.EntityType);
        Assert.Equal(result.Value.Id, req.EntityId);
        Assert.Equal("Created", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task UpdateAsync_WritesAuditLog()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        repo.Columns.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Member });

        var result = await service.UpdateAsync(new UpdateColumnCommand(projectId, columnId, "Updated", "#FF0000", 5, actorId));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Column", req.EntityType);
        Assert.Equal(columnId, req.EntityId);
        Assert.Equal("Updated", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task DeleteAsync_WritesAuditLog()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var columnId = NewId();

        repo.Columns.Add(new Column { Id = columnId, ProjectId = projectId, Name = "Backlog", Position = 0 });
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.DeleteAsync(new DeleteColumnCommand(projectId, columnId, actorId));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Column", req.EntityType);
        Assert.Equal(columnId, req.EntityId);
        Assert.Equal("Deleted", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    [Fact]
    public async Task ReorderAsync_WritesAuditLog()
    {
        var (repo, cardRepo, memberRepo, snapshotRefresher, publisher, auditWriter) = CreateMocks();
        var service = new ColumnService(repo, cardRepo, memberRepo, snapshotRefresher, new FakeProjectBoardEventPublisher(), auditWriter);
        var projectId = NewId();
        var actorId = NewId();
        var col1 = new Column { Id = NewId(), ProjectId = projectId, Name = "Backlog", Position = 0 };
        var col2 = new Column { Id = NewId(), ProjectId = projectId, Name = "Done", Position = 1 };
        repo.Columns.Add(col1);
        repo.Columns.Add(col2);
        memberRepo.Members.Add(new ProjectMember { ProjectId = projectId, UserId = actorId, Role = MemberRole.Owner });

        var result = await service.ReorderAsync(new ReorderColumnsCommand(projectId, [col2.Id, col1.Id], actorId));

        Assert.True(result.IsSuccess);
        var req = Assert.Single(auditWriter.Writes);
        Assert.Equal(actorId, req.ActorId);
        Assert.Equal(AuditLogScope.Project, req.Scope);
        Assert.Equal("Column", req.EntityType);
        Assert.Equal(Guid.Empty, req.EntityId);
        Assert.Equal("Reordered", req.Action);
        Assert.Equal(projectId, req.ProjectId);
    }

    private static (
        InMemoryColumnRepository repo,
        InMemoryCardRepository cardRepo,
        InMemoryProjectMemberRepository memberRepo,
        NullSnapshotRefresher snapshotRefresher,
        FakeProjectBoardEventPublisher publisher,
        InMemoryAuditLogWriter auditWriter
    ) CreateMocks()
    {
        return (
            new InMemoryColumnRepository(),
            new InMemoryCardRepository(),
            new InMemoryProjectMemberRepository(),
            new NullSnapshotRefresher(),
            new FakeProjectBoardEventPublisher(),
            new InMemoryAuditLogWriter()
        );
    }
}

internal class InMemoryColumnRepository : IColumnRepository
{
    public List<Column> Columns { get; } = [];

    public Task AddAsync(Column column, CancellationToken ct = default)
    {
        Columns.Add(column);
        return Task.CompletedTask;
    }

    public Task<Column?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Columns.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Column>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Column>>(Columns.Where(c => c.ProjectId == projectId).OrderBy(c => c.Position).ToList());

    public Task UpdateAsync(Column column, CancellationToken ct = default)
    {
        var idx = Columns.FindIndex(c => c.Id == column.Id);
        if (idx >= 0) Columns[idx] = column;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Columns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }

    public Task ReorderAsync(Guid projectId, IReadOnlyList<Guid> orderedColumnIds, CancellationToken ct = default)
    {
        for (var i = 0; i < orderedColumnIds.Count; i++)
        {
            var col = Columns.FirstOrDefault(c => c.Id == orderedColumnIds[i]);
            if (col != null) col.Position = i;
        }
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<Column> columns, CancellationToken ct = default)
    {
        Columns.AddRange(columns);
        return Task.CompletedTask;
    }
}

internal class InMemoryCardRepository : ICardRepository
{
    public List<Card> Cards { get; } = [];

    public Task<Card?> GetByIdAsync(Guid cardId, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.Id == cardId));

    public Task<IReadOnlyDictionary<Guid, Card>> GetByIdsAsync(IReadOnlyList<Guid> cardIds, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, Card>>(Cards.Where(c => cardIds.Contains(c.Id)).ToDictionary(c => c.Id));

    public Task<Card?> GetByProjectAndNumberAsync(Guid projectId, int cardNumber, CancellationToken ct = default)
        => Task.FromResult(Cards.FirstOrDefault(c => c.ProjectId == projectId && c.CardNumber == cardNumber));

    public Task<IReadOnlyList<Card>> ListByProjectAsync(Guid projectId, CardListFilter filter, CancellationToken ct = default)
    {
        var query = Cards.Where(c => c.ProjectId == projectId);
        if (filter.ColumnId.HasValue)
            query = query.Where(c => c.ColumnId == filter.ColumnId.Value);
        if (!filter.IncludeArchived)
            query = query.Where(c => c.ArchivedAt == null);
        if (filter.Type.HasValue)
            query = query.Where(c => c.Type == filter.Type.Value);
        return Task.FromResult<IReadOnlyList<Card>>(query.ToList());
    }

    public Task<int> GetMaxCardNumberAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(Cards.Where(c => c.ProjectId == projectId).Select(c => c.CardNumber).DefaultIfEmpty(0).Max());

    public Task AddAsync(Card card, CancellationToken ct = default) { Cards.Add(card); return Task.CompletedTask; }
    public Task UpdateAsync(Card card, CancellationToken ct = default) { var idx = Cards.FindIndex(c => c.Id == card.Id); if (idx >= 0) Cards[idx] = card; return Task.CompletedTask; }
    public Task UpdateRangeAsync(IReadOnlyList<Card> cards, CancellationToken ct = default) { foreach (var c in cards) { var idx = Cards.FindIndex(x => x.Id == c.Id); if (idx >= 0) Cards[idx] = c; } return Task.CompletedTask; }
    public Task DeleteAsync(Guid cardId, CancellationToken ct = default) { Cards.RemoveAll(c => c.Id == cardId); return Task.CompletedTask; }
    public Task CompactColumnPositionsAsync(Guid columnId, int exceptPosition, CancellationToken ct = default) => Task.CompletedTask;
    public Task<int> CountByColumnIdAsync(Guid columnId, CancellationToken ct = default)
        => Task.FromResult(Cards.Count(c => c.ColumnId == columnId && c.ArchivedAt == null));
}

internal class InMemoryProjectMemberRepository : IProjectMemberRepository
{
    public List<ProjectMember> Members { get; } = [];

    public Task<ProjectMember?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Members.FirstOrDefault(m => m.Id == id));

    public Task<ProjectMember?> GetByProjectAndUserAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Members.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId));

    public Task<IReadOnlyList<ProjectMember>> ListMembersAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ProjectMember>>(Members.Where(m => m.ProjectId == projectId).ToList());

    public Task<IReadOnlyDictionary<Guid, int>> GetMemberCountsAsync(IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var idList = projectIds.ToList();
        var counts = Members
            .Where(m => idList.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult<IReadOnlyDictionary<Guid, int>>(counts);
    }

    public Task AddMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        Members.Add(member);
        return Task.CompletedTask;
    }

    public Task UpdateMemberAsync(ProjectMember member, CancellationToken ct = default)
    {
        var idx = Members.FindIndex(m => m.Id == member.Id);
        if (idx >= 0) Members[idx] = member;
        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(Guid id, CancellationToken ct = default)
    {
        Members.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }
}

internal class InMemoryAuditLogWriter : IAuditLogWriter
{
    public List<AuditLogRequest> Writes { get; } = [];

    public Task<Result> WriteAsync(AuditLogRequest request, CancellationToken ct = default)
    {
        Writes.Add(request);
        return Task.FromResult(Result.Success());
    }

    public void Clear() => Writes.Clear();
}
