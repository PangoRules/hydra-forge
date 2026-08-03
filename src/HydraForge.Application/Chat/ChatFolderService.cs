using HydraForge.Application.Auth;
using HydraForge.Application.Projects;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Chat;
using HydraForge.Domain.Entities.PersonalSpace;

namespace HydraForge.Application.Chat;

public class ChatFolderService(
    IChatFolderRepository folderRepo,
    IChatSessionRepository sessionRepo,
    IUserRepository userRepo,
    IProjectMemberRepository memberRepo,
    ChatArchiveService archiveService
) : IChatFolderService
{
    private readonly IChatFolderRepository _folderRepo = folderRepo;
    private readonly IChatSessionRepository _sessionRepo = sessionRepo;
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IProjectMemberRepository _memberRepo = memberRepo;
    private readonly ChatArchiveService _archiveService = archiveService;

    public async Task<Result<ChatFolderDto>> CreateAsync(
        CreateChatFolderRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ChatFolderDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Folder name is required.")
            );

        if (request.ProjectId.HasValue)
        {
            if (
                !await MembershipGuard.HasAccessAsync(
                    _userRepo,
                    _memberRepo,
                    request.ProjectId.Value,
                    actorId,
                    ct
                )
            )
                return Result<ChatFolderDto>.Failure(
                    new Error(DomainErrorCodes.Projects.MembershipDenied, "Access denied.")
                );
        }

        if (request.ParentFolderId.HasValue)
        {
            var parent = await _folderRepo.GetByIdAsync(request.ParentFolderId.Value, ct);
            if (parent == null)
                return Result<ChatFolderDto>.Failure(
                    new Error(DomainErrorCodes.Chat.FolderNotFound, "Parent folder not found.")
                );

            var depth = await ComputeDepthAsync(request.ParentFolderId.Value, ct);
            if (depth >= 2)
                return Result<ChatFolderDto>.Failure(
                    new Error(DomainErrorCodes.Chat.FolderMaxDepth, "Max folder depth is 2.")
                );
        }

        var folder = new ChatFolder
        {
            Id = Guid.NewGuid(),
            OwnerId = actorId,
            Name = request.Name,
            ParentFolderId = request.ParentFolderId,
            ProjectId = request.ProjectId,
            CreatedAt = DateTime.UtcNow,
        };

        await _folderRepo.AddAsync(folder, ct);

        return Result<ChatFolderDto>.Success(MapToDto(folder));
    }

    public async Task<Result<IReadOnlyList<ChatFolderDto>>> ListAsync(
        Guid actorId,
        Guid? projectId,
        CancellationToken ct = default
    )
    {
        var allFolders = await _folderRepo.ListByOwnerAsync(actorId, projectId, ct);
        var folders = allFolders.Where(f => !f.ArchivedAt.HasValue);
        var dtos = folders.Select(MapToDto).ToList();
        return Result<IReadOnlyList<ChatFolderDto>>.Success(dtos);
    }

    public async Task<Result<ChatFolderDto>> UpdateAsync(
        Guid folderId,
        UpdateChatFolderRequest request,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ChatFolderDto>.Failure(
                new Error(DomainErrorCodes.Validation.Required, "Folder name is required.")
            );

        var folder = await _folderRepo.GetByIdAsync(folderId, ct);
        if (folder == null)
            return Result<ChatFolderDto>.Failure(
                new Error(DomainErrorCodes.Chat.FolderNotFound, "Folder not found.")
            );

        if (folder.OwnerId != actorId)
            return Result<ChatFolderDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotOwner, "Only the owner can update the folder.")
            );

        if (request.ParentFolderId.HasValue)
        {
            if (request.ParentFolderId.Value == folderId)
                return Result<ChatFolderDto>.Failure(
                    new Error(DomainErrorCodes.Chat.FolderSelfParent, "A folder cannot be its own parent.")
                );

            var parent = await _folderRepo.GetByIdAsync(request.ParentFolderId.Value, ct);
            if (parent == null)
                return Result<ChatFolderDto>.Failure(
                    new Error(DomainErrorCodes.Chat.FolderNotFound, "Parent folder not found.")
                );

            var depth = await ComputeDepthAsync(request.ParentFolderId.Value, ct);
            if (depth >= 2)
                return Result<ChatFolderDto>.Failure(
                    new Error(DomainErrorCodes.Chat.FolderMaxDepth, "Max folder depth is 2.")
                );
        }

        folder.Name = request.Name;
        folder.ParentFolderId = request.ParentFolderId;
        await _folderRepo.UpdateAsync(folder, ct);

        return Result<ChatFolderDto>.Success(MapToDto(folder));
    }

    public async Task<Result<ChatFolderDto>> ArchiveAsync(
        Guid folderId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var folder = await _folderRepo.GetByIdAsync(folderId, ct);
        if (folder == null)
            return Result<ChatFolderDto>.Failure(
                new Error(DomainErrorCodes.Chat.FolderNotFound, "Folder not found.")
            );

        if (folder.OwnerId != actorId)
            return Result<ChatFolderDto>.Failure(
                new Error(DomainErrorCodes.Chat.SessionNotOwner, "Only the owner can archive the folder.")
            );

        await _archiveService.ArchiveFolderAsync(folder.Id, ct);
        folder = await _folderRepo.GetByIdAsync(folderId, ct);
        if (folder == null)
            return Result<ChatFolderDto>.Failure(
                new Error(DomainErrorCodes.Chat.FolderNotFound, "Folder not found.")
            );

        return Result<ChatFolderDto>.Success(MapToDto(folder));
    }

    private async Task<int> ComputeDepthAsync(Guid folderId, CancellationToken ct)
    {
        var depth = 0;
        var currentId = (Guid?)folderId;
        while (currentId.HasValue)
        {
            var folder = await _folderRepo.GetByIdAsync(currentId.Value, ct);
            if (folder == null)
                break;
            depth++;
            currentId = folder.ParentFolderId;
        }
        return depth - 1;
    }

    private static ChatFolderDto MapToDto(ChatFolder folder) =>
        new(
            folder.Id,
            folder.Name,
            folder.ParentFolderId,
            folder.ProjectId,
            folder.CreatedAt,
            folder.ArchivedAt
        );
}
