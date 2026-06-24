using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HydraForge.Server.Tests.Errors;

public class ProblemDetailsMapperTests
{
    [Fact]
    public void FromError_AuthInvalidCredentials_MapsTo401()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials,
            "Invalid username or password.");

        var details = ProblemDetailsMapper.FromError(error, "corr-1");

        Assert.Equal(401, details.Status);
        Assert.Equal("Invalid credentials", details.Title);
        Assert.Equal("corr-1", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_AuthUserDisabled_MapsTo403()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.UserDisabled,
            "User account is disabled.");

        var details = ProblemDetailsMapper.FromError(error, "corr-2");

        Assert.Equal(403, details.Status);
        Assert.Equal("Access denied", details.Title);
        Assert.Equal("corr-2", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Auth.UserDisabled, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_AuthAdminSeedNotConfigured_MapsTo500()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.AdminSeedNotConfigured,
            "Admin seed is not configured.");

        var details = ProblemDetailsMapper.FromError(error, "corr-3");

        Assert.Equal(500, details.Status);
        Assert.Equal("Internal server error", details.Title);
        Assert.Equal("corr-3", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Auth.AdminSeedNotConfigured, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_DatabaseUnavailable_MapsTo503()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.DatabaseUnavailable,
            "Database is unavailable.");

        var details = ProblemDetailsMapper.FromError(error, "corr-4");

        Assert.Equal(503, details.Status);
        Assert.Equal("Service unavailable", details.Title);
        Assert.Equal("corr-4", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.DatabaseUnavailable, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_AuditWriteFailed_MapsTo500()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.AuditWriteFailed,
            "Failed to write audit entry.");

        var details = ProblemDetailsMapper.FromError(error, "corr-5");

        Assert.Equal(500, details.Status);
        Assert.Equal("Internal server error", details.Title);
        Assert.Equal("corr-5", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Infrastructure.AuditWriteFailed, details.Extensions["code"]);
    }

    [Fact]
    public void FromError_UnknownCode_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error("UNKNOWN_CODE", "Some unknown error.");

        var details = ProblemDetailsMapper.FromError(error, "corr-6");

        Assert.Equal(400, details.Status);
        Assert.Equal("Bad request", details.Title);
        Assert.Equal("corr-6", details.Extensions["correlationId"]);
        Assert.Equal("UNKNOWN_CODE", details.Extensions["code"]);
    }

    [Fact]
    public void FromError_SetsTypeToHydraForgeErrorsUri()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials,
            "Invalid username or password.");

        var details = ProblemDetailsMapper.FromError(error, "corr-7");

        Assert.StartsWith("https://hydraforge.local/errors/", details.Type);
    }

    [Fact]
    public void FromError_ConvertsErrorCodeToReadableSlug()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Auth.InvalidCredentials,
            "Invalid username or password.");

        var details = ProblemDetailsMapper.FromError(error, "corr-8");

        Assert.Equal("https://hydraforge.local/errors/auth-invalid-credentials", details.Type);
    }

    // Phase 2: Projects
    [Fact]
    public void FromError_ProjectNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Projects.NotFound, "Project not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-p1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Project not found", details.Title);
        Assert.Equal("corr-p1", details.Extensions["correlationId"]);
        Assert.Equal(HydraForge.Domain.Common.DomainErrorCodes.Projects.NotFound, details.Extensions["code"]);
        Assert.Equal("https://hydraforge.local/errors/project-not-found", details.Type);
    }

    [Fact]
    public void FromError_ProjectArchived_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Projects.Archived, "Project is archived.");

        var details = ProblemDetailsMapper.FromError(error, "corr-p2");

        Assert.Equal(400, details.Status);
        Assert.Equal("Project is archived", details.Title);
        Assert.Equal("https://hydraforge.local/errors/project-archived", details.Type);
    }

    [Fact]
    public void FromError_ProjectOwnerRequired_MapsTo403()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Projects.OwnerRequired, "Owner role required.");

        var details = ProblemDetailsMapper.FromError(error, "corr-p3");

        Assert.Equal(403, details.Status);
        Assert.Equal("Owner role required", details.Title);
        Assert.Equal("https://hydraforge.local/errors/project-owner-required", details.Type);
    }

    [Fact]
    public void FromError_ProjectLastOwnerRemovalDenied_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Projects.LastOwnerRemovalDenied, "Cannot remove last owner.");

        var details = ProblemDetailsMapper.FromError(error, "corr-p4");

        Assert.Equal(400, details.Status);
        Assert.Equal("Cannot remove the last owner", details.Title);
        Assert.Equal("https://hydraforge.local/errors/project-last-owner-removal-denied", details.Type);
    }

    [Fact]
    public void FromError_ProjectMembershipDenied_MapsTo403()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Projects.MembershipDenied, "Membership denied.");

        var details = ProblemDetailsMapper.FromError(error, "corr-p5");

        Assert.Equal(403, details.Status);
        Assert.Equal("Access denied", details.Title);
        Assert.Equal("https://hydraforge.local/errors/project-membership-denied", details.Type);
    }

    [Fact]
    public void FromError_ProjectMemberDuplicate_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Projects.MemberDuplicate, "Already a member.");

        var details = ProblemDetailsMapper.FromError(error, "corr-p6");

        Assert.Equal(409, details.Status);
        Assert.Equal("User is already a member", details.Title);
        Assert.Equal("https://hydraforge.local/errors/project-member-duplicate", details.Type);
    }

    // Phase 2: Membership
    [Fact]
    public void FromError_MembershipNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Membership.NotFound, "Member not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-m1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Member not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/membership-not-found", details.Type);
    }

    [Fact]
    public void FromError_MembershipRoleDenied_MapsTo403()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Membership.RoleDenied, "Role denied.");

        var details = ProblemDetailsMapper.FromError(error, "corr-m2");

        Assert.Equal(403, details.Status);
        Assert.Equal("Role denied", details.Title);
        Assert.Equal("https://hydraforge.local/errors/membership-role-denied", details.Type);
    }

    // Phase 2: Columns
    [Fact]
    public void FromError_ColumnNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Columns.NotFound, "Column not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-c1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Column not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/column-not-found", details.Type);
    }

    [Fact]
    public void FromError_ColumnInvalidPosition_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Columns.InvalidPosition, "Invalid positions.");

        var details = ProblemDetailsMapper.FromError(error, "corr-c2");

        Assert.Equal(400, details.Status);
        Assert.Equal("Invalid column positions", details.Title);
        Assert.Equal("https://hydraforge.local/errors/column-invalid-position", details.Type);
    }

    [Fact]
    public void FromError_ColumnDeleteNonEmpty_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Columns.DeleteNonEmpty, "Column has cards.");

        var details = ProblemDetailsMapper.FromError(error, "corr-c3");

        Assert.Equal(409, details.Status);
        Assert.Equal("Cannot delete column with cards", details.Title);
        Assert.Equal("https://hydraforge.local/errors/column-delete-non-empty", details.Type);
    }

    [Fact]
    public void FromError_ColumnArchivedProjectDenied_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Columns.ArchivedProjectDenied, "Project archived.");

        var details = ProblemDetailsMapper.FromError(error, "corr-c4");

        Assert.Equal(400, details.Status);
        Assert.Equal("Project is archived", details.Title);
        Assert.Equal("https://hydraforge.local/errors/column-archived-project-denied", details.Type);
    }

    // Phase 2: Cards
    [Fact]
    public void FromError_CardNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.NotFound, "Card not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Card not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-not-found", details.Type);
    }

    [Fact]
    public void FromError_CardArchived_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.Archived, "Card is archived.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca2");

        Assert.Equal(400, details.Status);
        Assert.Equal("Card is archived", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-archived", details.Type);
    }

    [Fact]
    public void FromError_CardInvalidType_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.InvalidType, "Invalid card type.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca3");

        Assert.Equal(400, details.Status);
        Assert.Equal("Invalid card type", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-invalid-type", details.Type);
    }

    [Fact]
    public void FromError_CardInvalidAssignee_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.InvalidAssignee, "Invalid assignee.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca4");

        Assert.Equal(400, details.Status);
        Assert.Equal("Invalid assignee", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-invalid-assignee", details.Type);
    }

    [Fact]
    public void FromError_CardDuplicateAssignee_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.DuplicateAssignee, "Already assigned.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca5");

        Assert.Equal(409, details.Status);
        Assert.Equal("User is already assigned", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-duplicate-assignee", details.Type);
    }

    [Fact]
    public void FromError_CardInvalidParentEpic_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.InvalidParentEpic, "Invalid parent epic.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca6");

        Assert.Equal(400, details.Status);
        Assert.Equal("Invalid parent epic", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-invalid-parent-epic", details.Type);
    }

    [Fact]
    public void FromError_CardParentCycle_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.ParentCycle, "Cycle detected.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca7");

        Assert.Equal(400, details.Status);
        Assert.Equal("Parent cycle detected", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-parent-cycle", details.Type);
    }

    [Fact]
    public void FromError_CardBlockedMoveWarning_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.BlockedMoveWarning, "Card has blockers.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca8");

        Assert.Equal(409, details.Status);
        Assert.Equal("Card has blockers", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-blocked-move-warning", details.Type);
    }

    [Fact]
    public void FromError_CardConcurrencyMismatch_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Cards.ConcurrencyMismatch, "Card modified.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ca9");

        Assert.Equal(409, details.Status);
        Assert.Equal("Card has been modified", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-concurrency-mismatch", details.Type);
    }

    // Phase 2: Checklist
    [Fact]
    public void FromError_ChecklistItemNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Checklist.ItemNotFound, "Item not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ch1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Checklist item not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/checklist-item-not-found", details.Type);
    }

    [Fact]
    public void FromError_ChecklistInvalidPosition_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Checklist.InvalidPosition, "Invalid position.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ch2");

        Assert.Equal(400, details.Status);
        Assert.Equal("Invalid position", details.Title);
        Assert.Equal("https://hydraforge.local/errors/checklist-invalid-position", details.Type);
    }

    [Fact]
    public void FromError_ChecklistInvalidAssignee_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Checklist.InvalidAssignee, "Invalid assignee.");

        var details = ProblemDetailsMapper.FromError(error, "corr-ch3");

        Assert.Equal(400, details.Status);
        Assert.Equal("Invalid assignee", details.Title);
        Assert.Equal("https://hydraforge.local/errors/checklist-invalid-assignee", details.Type);
    }

    // Phase 2: Comments
    [Fact]
    public void FromError_CommentNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Comments.NotFound, "Comment not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-cm1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Comment not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/comment-not-found", details.Type);
    }

    [Fact]
    public void FromError_CommentArchived_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Comments.Archived, "Comment is archived.");

        var details = ProblemDetailsMapper.FromError(error, "corr-cm2");

        Assert.Equal(400, details.Status);
        Assert.Equal("Comment is archived", details.Title);
        Assert.Equal("https://hydraforge.local/errors/comment-archived", details.Type);
    }

    // Phase 2: Attachments
    [Fact]
    public void FromError_AttachmentNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Attachments.NotFound, "Attachment not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-at1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/attachment-not-found", details.Type);
    }

    [Fact]
    public void FromError_AttachmentUnsupportedContentType_MapsTo415()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Attachments.UnsupportedContentType, "Unsupported type.");

        var details = ProblemDetailsMapper.FromError(error, "corr-at2");

        Assert.Equal(415, details.Status);
        Assert.Equal("Unsupported media type", details.Title);
        Assert.Equal("https://hydraforge.local/errors/attachment-unsupported-content-type", details.Type);
    }

    [Fact]
    public void FromError_AttachmentFileTooLarge_MapsTo413()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Attachments.FileTooLarge, "File too large.");

        var details = ProblemDetailsMapper.FromError(error, "corr-at3");

        Assert.Equal(413, details.Status);
        Assert.Equal("Payload too large", details.Title);
        Assert.Equal("https://hydraforge.local/errors/attachment-file-too-large", details.Type);
    }

    [Fact]
    public void FromError_AttachmentFileStoreUnavailable_MapsTo503()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Attachments.FileStoreUnavailable, "File store down.");

        var details = ProblemDetailsMapper.FromError(error, "corr-at4");

        Assert.Equal(503, details.Status);
        Assert.Equal("Service unavailable", details.Title);
        Assert.Equal("https://hydraforge.local/errors/attachment-file-store-unavailable", details.Type);
    }

    // Phase 2: Mentions
    [Fact]
    public void FromError_MentionUserNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Mentions.UserNotFound, "User not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-mn1");

        Assert.Equal(404, details.Status);
        Assert.Equal("User not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/mention-user-not-found", details.Type);
    }

    // Phase 2: Specs and Plans share patterns tested via or-pattern above

    [Fact]
    public void FromError_SpecNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Specs.NotFound, "Spec not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-sp1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Spec not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/spec-not-found", details.Type);
    }

    [Fact]
    public void FromError_SpecDocumentVersionNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Specs.DocumentVersionNotFound, "Version not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-sp2");

        Assert.Equal(404, details.Status);
        Assert.Equal("Spec/Plan version not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/document-version-not-found", details.Type);
    }

    [Fact]
    public void FromError_SpecMarkdownPayloadTooLarge_MapsTo413()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Specs.MarkdownPayloadTooLarge, "Payload too large.");

        var details = ProblemDetailsMapper.FromError(error, "corr-sp3");

        Assert.Equal(413, details.Status);
        Assert.Equal("Payload too large", details.Title);
        Assert.Equal("https://hydraforge.local/errors/markdown-payload-too-large", details.Type);
    }

    [Fact]
    public void FromError_SpecCardDocumentProjectMismatch_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Specs.CardDocumentProjectMismatch, "Project mismatch.");

        var details = ProblemDetailsMapper.FromError(error, "corr-sp4");

        Assert.Equal(409, details.Status);
        Assert.Equal("Card is in a different project", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-document-project-mismatch", details.Type);
    }

    [Fact]
    public void FromError_PlanNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Plans.NotFound, "Plan not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-pl1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Plan not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/plan-not-found", details.Type);
    }

    [Fact]
    public void FromError_PlanDocumentVersionNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Plans.DocumentVersionNotFound, "Version not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-pl2");

        Assert.Equal(404, details.Status);
        Assert.Equal("Spec/Plan version not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/document-version-not-found", details.Type);
    }

    [Fact]
    public void FromError_PlanMarkdownPayloadTooLarge_MapsTo413()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Plans.MarkdownPayloadTooLarge, "Payload too large.");

        var details = ProblemDetailsMapper.FromError(error, "corr-pl3");

        Assert.Equal(413, details.Status);
        Assert.Equal("Payload too large", details.Title);
        Assert.Equal("https://hydraforge.local/errors/markdown-payload-too-large", details.Type);
    }

    [Fact]
    public void FromError_PlanCardDocumentProjectMismatch_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Plans.CardDocumentProjectMismatch, "Project mismatch.");

        var details = ProblemDetailsMapper.FromError(error, "corr-pl4");

        Assert.Equal(409, details.Status);
        Assert.Equal("Card is in a different project", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-document-project-mismatch", details.Type);
    }

    // Phase 2: Relationships
    [Fact]
    public void FromError_RelationshipNotFound_MapsTo404()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Relationships.NotFound, "Relationship not found.");

        var details = ProblemDetailsMapper.FromError(error, "corr-r1");

        Assert.Equal(404, details.Status);
        Assert.Equal("Relationship not found", details.Title);
        Assert.Equal("https://hydraforge.local/errors/relationship-not-found", details.Type);
    }

    [Fact]
    public void FromError_RelationshipDuplicate_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Relationships.Duplicate, "Already exists.");

        var details = ProblemDetailsMapper.FromError(error, "corr-r2");

        Assert.Equal(409, details.Status);
        Assert.Equal("Relationship already exists", details.Title);
        Assert.Equal("https://hydraforge.local/errors/relationship-duplicate", details.Type);
    }

    [Fact]
    public void FromError_RelationshipCrossProjectDenied_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Relationships.CrossProjectDenied, "Cross project.");

        var details = ProblemDetailsMapper.FromError(error, "corr-r3");

        Assert.Equal(400, details.Status);
        Assert.Equal("Relationships must be in the same project", details.Title);
        Assert.Equal("https://hydraforge.local/errors/relationship-cross-project-denied", details.Type);
    }

    [Fact]
    public void FromError_RelationshipCycle_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Relationships.Cycle, "Cycle detected.");

        var details = ProblemDetailsMapper.FromError(error, "corr-r4");

        Assert.Equal(400, details.Status);
        Assert.Equal("Relationship would create a cycle", details.Title);
        Assert.Equal("https://hydraforge.local/errors/relationship-cycle", details.Type);
    }

    [Fact]
    public void FromError_RelationshipSelfDenied_MapsTo400()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Relationships.SelfDenied, "Self relationship.");

        var details = ProblemDetailsMapper.FromError(error, "corr-r5");

        Assert.Equal(400, details.Status);
        Assert.Equal("Card cannot relate to itself", details.Title);
        Assert.Equal("https://hydraforge.local/errors/relationship-self-denied", details.Type);
    }

    [Fact]
    public void FromError_RelationshipArchiveImpactConfirmRequired_MapsTo409()
    {
        var error = new HydraForge.Domain.Common.Error(
            HydraForge.Domain.Common.DomainErrorCodes.Relationships.ArchiveImpactConfirmRequired, "Confirm.");

        var details = ProblemDetailsMapper.FromError(error, "corr-r6");

        Assert.Equal(409, details.Status);
        Assert.Equal("Confirmation required", details.Title);
        Assert.Equal("https://hydraforge.local/errors/card-archive-impact-confirm-required", details.Type);
    }
}
