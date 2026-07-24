using System.Text.Json;
using HydraForge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Errors;

public static class ProblemDetailsMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ProblemDetails FromError(Error error, string correlationId)
    {
        var (status, title) = error.Code switch
        {
            DomainErrorCodes.Auth.InvalidCredentials => (401, "Invalid credentials"),
            DomainErrorCodes.Auth.UserDisabled => (403, "Access denied"),
            DomainErrorCodes.Auth.AdminSeedNotConfigured => (500, "Internal server error"),
            DomainErrorCodes.Infrastructure.DatabaseUnavailable => (503, "Service unavailable"),
            DomainErrorCodes.Infrastructure.AuditWriteFailed => (500, "Internal server error"),
            DomainErrorCodes.Projects.NotFound => (404, "Project not found"),
            DomainErrorCodes.Projects.Archived => (400, "Project is archived"),
            DomainErrorCodes.Projects.OwnerRequired => (403, "Owner role required"),
            DomainErrorCodes.Projects.LastOwnerRemovalDenied => (400, "Cannot remove the last owner"),
            DomainErrorCodes.Projects.MembershipDenied => (403, "Access denied"),
            DomainErrorCodes.Projects.MemberDuplicate => (409, "User is already a member"),
            DomainErrorCodes.Membership.NotFound => (404, "Member not found"),
            DomainErrorCodes.Membership.RoleDenied => (403, "Role denied"),
            DomainErrorCodes.Mentions.UserNotFound => (404, "User not found"),
            DomainErrorCodes.Columns.NotFound => (404, "Column not found"),
            DomainErrorCodes.Columns.InvalidPosition => (400, "Invalid column positions"),
            DomainErrorCodes.Columns.DeleteNonEmpty => (409, "Cannot delete column with cards"),
            DomainErrorCodes.Columns.ArchivedProjectDenied => (400, "Project is archived"),
            DomainErrorCodes.Cards.NotFound => (404, "Card not found"),
            DomainErrorCodes.Cards.Archived => (400, "Card is archived"),
            DomainErrorCodes.Cards.InvalidType => (400, "Invalid card type"),
            DomainErrorCodes.Cards.InvalidAssignee => (400, "Invalid assignee"),
            DomainErrorCodes.Cards.DuplicateAssignee => (409, "User is already assigned"),
            DomainErrorCodes.Cards.InvalidParent => (400, "Invalid parent"),
            DomainErrorCodes.Cards.ParentCycle => (400, "Parent cycle detected"),
            DomainErrorCodes.Cards.BlockedMoveWarning => (409, "Card has blockers"),
            DomainErrorCodes.Cards.ConcurrencyMismatch => (409, "Card has been modified"),
            DomainErrorCodes.Checklist.ItemNotFound => (404, "Checklist item not found"),
            DomainErrorCodes.Checklist.InvalidPosition => (400, "Invalid position"),
            DomainErrorCodes.Checklist.InvalidAssignee => (400, "Invalid assignee"),
            DomainErrorCodes.Comments.NotFound => (404, "Comment not found"),
            DomainErrorCodes.Comments.Archived => (400, "Comment is archived"),
            DomainErrorCodes.Attachments.UnsupportedContentType => (415, "Unsupported media type"),
            DomainErrorCodes.Attachments.NotFound => (404, "Not found"),
            DomainErrorCodes.Attachments.FileTooLarge => (413, "Payload too large"),
            DomainErrorCodes.Attachments.FileStoreUnavailable => (503, "Service unavailable"),
            DomainErrorCodes.Specs.NotFound => (404, "Spec not found"),
            DomainErrorCodes.Plans.NotFound => (404, "Plan not found"),
            DomainErrorCodes.Specs.DocumentVersionNotFound
                or DomainErrorCodes.Plans.DocumentVersionNotFound => (404, "Spec/Plan version not found"),
            DomainErrorCodes.Specs.MarkdownPayloadTooLarge
                or DomainErrorCodes.Plans.MarkdownPayloadTooLarge => (413, "Payload too large"),
            DomainErrorCodes.Specs.CardDocumentProjectMismatch
                or DomainErrorCodes.Plans.CardDocumentProjectMismatch => (409, "Card is in a different project"),
            DomainErrorCodes.Relationships.NotFound => (404, "Relationship not found"),
            DomainErrorCodes.Relationships.Duplicate => (409, "Relationship already exists"),
            DomainErrorCodes.Relationships.CrossProjectDenied => (400, "Relationships must be in the same project"),
            DomainErrorCodes.Relationships.Cycle => (400, "Relationship would create a cycle"),
            DomainErrorCodes.Relationships.SelfDenied => (400, "Card cannot relate to itself"),
            DomainErrorCodes.Relationships.ArchiveImpactConfirmRequired => (409, "Confirmation required"),
            _ => (400, "Bad request"),
        };

        var type = $"https://hydraforge.local/errors/{ToKebabCase(error.Code)}";

        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
        };

        details.Extensions["correlationId"] = correlationId;
        details.Extensions["code"] = error.Code;

        return details;
    }

    private static string ToKebabCase(string code)
    {
        if (string.IsNullOrEmpty(code)) return code;
        return code.ToLowerInvariant().Replace('_', '-');
    }
}
