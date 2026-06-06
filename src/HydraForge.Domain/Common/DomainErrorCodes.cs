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
}