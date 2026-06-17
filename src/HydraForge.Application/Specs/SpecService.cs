using HydraForge.Application.Audit;
using HydraForge.Application.Cards;
using HydraForge.Application.Projects;
using HydraForge.Application.Shared;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.ProjectSpace;
using HydraForge.Domain.Enums;

namespace HydraForge.Application.Specs;

public class SpecService(
    ISpecRepository specRepo,
    ICardRepository cardRepo,
    IProjectMemberRepository memberRepo,
    IAuditLogWriter auditLogWriter
)
{
    private readonly ISpecRepository _specRepo = specRepo;
    private readonly ICardRepository _cardRepo = cardRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly IAuditLogWriter _auditLogWriter = auditLogWriter;

    public async Task<Result<SpecDto>> CreateAsync(
        CreateSpecCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<SpecDto>.Failure(
                new Error(
                    DomainErrorCodes.Specs.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."
                )
            );

        var spec = new Spec
        {
            Id = Guid.NewGuid(),
            ProjectId = cmd.ProjectId,
            Title = cmd.Title,
            Description = cmd.Description,
            Content = cmd.Content,
            Version = 1,
            CreatedByUserId = cmd.ActorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var version = new SpecVersion
        {
            Id = Guid.NewGuid(),
            SpecId = spec.Id,
            Version = 1,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _specRepo.AddAsync(spec, ct);
        await _specRepo.AddVersionAsync(version, ct);
        await _specRepo.SaveChangesAsync(ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Spec",
                spec.Id,
                "Created",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result<SpecDto>.Success(MapToDto(spec, null));
    }

    public async Task<Result<SpecDto>> GetByIdAsync(
        Guid projectId,
        Guid specId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var spec = await _specRepo.GetByIdAsync(specId, ct);
        if (spec == null || spec.ProjectId != projectId)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Specs.NotFound, "Spec not found.")
            );

        var linkedCardId = await _specRepo.GetLinkedCardIdAsync(specId, ct);
        return Result<SpecDto>.Success(MapToDto(spec, linkedCardId));
    }

    public async Task<Result<IReadOnlyList<SpecDto>>> ListAsync(
        Guid projectId,
        SpecListFilter filter,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<SpecDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var specs = await _specRepo.ListByProjectAsync(projectId, filter, ct);
        var linkedCardIds = await _specRepo.GetLinkedCardIdsAsync(projectId, ct);
        var dtos = specs.Select(s => MapToDto(s, linkedCardIds.TryGetValue(s.Id, out var id) ? id : null)).ToList();
        return Result<IReadOnlyList<SpecDto>>.Success(dtos);
    }

    public async Task<Result<SpecDto>> UpdateAsync(
        UpdateSpecCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var spec = await _specRepo.GetByIdAsync(cmd.SpecId, ct);
        if (spec == null || spec.ProjectId != cmd.ProjectId)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Specs.NotFound, "Spec not found.")
            );

        if (cmd.Content.Length > DocumentMarkdownLimits.MaxMarkdownPayloadBytes)
            return Result<SpecDto>.Failure(
                new Error(
                    DomainErrorCodes.Specs.MarkdownPayloadTooLarge,
                    "Markdown payload exceeds limit."
                )
            );

        spec.Title = cmd.Title;
        spec.Description = cmd.Description;
        spec.Content = cmd.Content;
        spec.Version += 1;
        spec.UpdatedAt = DateTime.UtcNow;

        var version = new SpecVersion
        {
            Id = Guid.NewGuid(),
            SpecId = spec.Id,
            Version = spec.Version,
            Content = cmd.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _specRepo.UpdateAsync(spec, ct);
        await _specRepo.AddVersionAsync(version, ct);
        await _specRepo.SaveChangesAsync(ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Spec",
                spec.Id,
                "Updated",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        var linkedCardId = await _specRepo.GetLinkedCardIdAsync(spec.Id, ct);
        return Result<SpecDto>.Success(MapToDto(spec, linkedCardId));
    }

    public async Task<Result<IReadOnlyList<SpecVersionDto>>> ListVersionsAsync(
        Guid projectId,
        Guid specId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(projectId, actorId, ct);
        if (membership == null)
            return Result<IReadOnlyList<SpecVersionDto>>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var spec = await _specRepo.GetByIdAsync(specId, ct);
        if (spec == null || spec.ProjectId != projectId)
            return Result<IReadOnlyList<SpecVersionDto>>.Failure(
                new Error(DomainErrorCodes.Specs.NotFound, "Spec not found.")
            );

        var versions = await _specRepo.ListVersionsAsync(specId, ct);
        return Result<IReadOnlyList<SpecVersionDto>>.Success(
            versions
                .Select(v => new SpecVersionDto(
                    v.Id,
                    v.SpecId,
                    v.Version,
                    v.Content,
                    v.CreatedAt,
                    v.CreatedByUserId
                ))
                .ToList()
        );
    }

    public async Task<Result<SpecDto>> RestoreVersionAsync(
        RestoreSpecVersionCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var spec = await _specRepo.GetByIdAsync(cmd.SpecId, ct);
        if (spec == null || spec.ProjectId != cmd.ProjectId)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Specs.NotFound, "Spec not found.")
            );

        var oldVersion = await _specRepo.GetVersionAsync(cmd.SpecId, cmd.Version, ct);
        if (oldVersion == null)
            return Result<SpecDto>.Failure(
                new Error(DomainErrorCodes.Specs.DocumentVersionNotFound, "Spec version not found.")
            );

        spec.Content = oldVersion.Content;
        spec.Version += 1;
        spec.UpdatedAt = DateTime.UtcNow;

        var newVersion = new SpecVersion
        {
            Id = Guid.NewGuid(),
            SpecId = spec.Id,
            Version = spec.Version,
            Content = oldVersion.Content,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = cmd.ActorId,
        };

        await _specRepo.UpdateAsync(spec, ct);
        await _specRepo.AddVersionAsync(newVersion, ct);
        await _specRepo.SaveChangesAsync(ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Spec",
                spec.Id,
                "Restored",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        var linkedCardId = await _specRepo.GetLinkedCardIdAsync(spec.Id, ct);
        return Result<SpecDto>.Success(MapToDto(spec, linkedCardId));
    }

    public async Task<Result> LinkToCardAsync(
        LinkSpecToCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var spec = await _specRepo.GetByIdAsync(cmd.SpecId, ct);
        if (spec == null || spec.ProjectId != cmd.ProjectId)
            return Result.Failure(new Error(DomainErrorCodes.Specs.NotFound, "Spec not found."));

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null)
            return Result.Failure(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));

        if (card.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Specs.CardDocumentProjectMismatch,
                    "Card is in a different project."
                )
            );

        card.SpecId = cmd.SpecId;
        await _cardRepo.UpdateAsync(card, ct);
        await _specRepo.SaveChangesAsync(ct);

        await _auditLogWriter.WriteAsync(
            new AuditLogRequest(
                cmd.ActorId,
                AuditLogScope.Project,
                "Spec",
                spec.Id,
                "Linked",
                cmd.ProjectId,
                null,
                null
            ),
            ct
        );

        return Result.Success();
    }

    public async Task<Result> UnlinkFromCardAsync(
        UnlinkSpecFromCardCommand cmd,
        CancellationToken ct = default
    )
    {
        var membership = await _memberRepo.GetByProjectAndUserAsync(cmd.ProjectId, cmd.ActorId, ct);
        if (membership == null)
            return Result.Failure(
                new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
            );

        var card = await _cardRepo.GetByIdAsync(cmd.CardId, ct);
        if (card == null)
            return Result.Failure(new Error(DomainErrorCodes.Cards.NotFound, "Card not found."));

        if (card.ProjectId != cmd.ProjectId)
            return Result.Failure(
                new Error(
                    DomainErrorCodes.Specs.CardDocumentProjectMismatch,
                    "Card is in a different project."
                )
            );

        if (card.SpecId != cmd.SpecId)
            return Result.Failure(
                new Error(DomainErrorCodes.Specs.NotFound, "Card is not linked to this spec.")
            );

        if (card.SpecId == null)
            return Result.Success();

        card.SpecId = null;
        await _cardRepo.UpdateAsync(card, ct);
        await _specRepo.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static SpecDto MapToDto(Spec spec, Guid? linkedCardId) =>
        new(
            spec.Id,
            spec.ProjectId,
            spec.Title,
            spec.Description,
            spec.Content,
            spec.Version,
            spec.CreatedByUserId,
            spec.CreatedAt,
            spec.UpdatedAt,
            linkedCardId
        );
}
