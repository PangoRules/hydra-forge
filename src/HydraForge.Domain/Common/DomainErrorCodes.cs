namespace HydraForge.Domain.Common;

public static class DomainErrorCodes
{
    public static class Auth
    {
        public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
        public const string UserDisabled = "AUTH_USER_DISABLED";
        public const string AdminSeedNotConfigured = "AUTH_ADMIN_SEED_NOT_CONFIGURED";
    }

    public static class Infrastructure
    {
        public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
        public const string AuditWriteFailed = "AUDIT_WRITE_FAILED";
        public const string LlmProviderUnavailable = "LLM_PROVIDER_UNAVAILABLE";
    }

    public static class Projects
    {
        public const string NotFound = "PROJECT_NOT_FOUND";
        public const string Archived = "PROJECT_ARCHIVED";
        public const string OwnerRequired = "PROJECT_OWNER_REQUIRED";
        public const string LastOwnerRemovalDenied = "PROJECT_LAST_OWNER_REMOVAL_DENIED";
        public const string MembershipDenied = "PROJECT_MEMBERSHIP_DENIED";
        public const string MemberDuplicate = "PROJECT_MEMBER_DUPLICATE";
    }

    public static class Membership
    {
        public const string NotFound = "MEMBERSHIP_NOT_FOUND";
        public const string RoleDenied = "MEMBERSHIP_ROLE_DENIED";
    }

    public static class Columns
    {
        public const string NotFound = "COLUMN_NOT_FOUND";
        public const string InvalidPosition = "COLUMN_INVALID_POSITION";
        public const string DeleteNonEmpty = "COLUMN_DELETE_NON_EMPTY";
        public const string ArchivedProjectDenied = "COLUMN_ARCHIVED_PROJECT_DENIED";
    }

    public static class Cards
    {
        public const string NotFound = "CARD_NOT_FOUND";
        public const string Archived = "CARD_ARCHIVED";
        public const string InvalidType = "CARD_INVALID_TYPE";
        public const string InvalidAssignee = "CARD_INVALID_ASSIGNEE";
        public const string DuplicateAssignee = "CARD_DUPLICATE_ASSIGNEE";
        public const string InvalidParentEpic = "CARD_INVALID_PARENT_EPIC";
        public const string ParentCycle = "CARD_PARENT_CYCLE";
        public const string BlockedMoveWarning = "CARD_BLOCKED_MOVE_WARNING";
        public const string ConcurrencyMismatch = "CARD_CONCURRENCY_MISMATCH";
    }

    public static class Checklist
    {
        public const string ItemNotFound = "CHECKLIST_ITEM_NOT_FOUND";
        public const string InvalidPosition = "CHECKLIST_INVALID_POSITION";
        public const string InvalidAssignee = "CHECKLIST_INVALID_ASSIGNEE";
    }

    public static class Comments
    {
        public const string NotFound = "COMMENT_NOT_FOUND";
        public const string Archived = "COMMENT_ARCHIVED";
    }

    public static class Mentions
    {
        public const string UserNotFound = "MENTION_USER_NOT_FOUND";
    }
}
