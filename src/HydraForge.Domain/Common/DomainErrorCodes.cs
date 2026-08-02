namespace HydraForge.Domain.Common;

public static class DomainErrorCodes
{
    public static class Auth
    {
        public const string InvalidCredentials = "AUTH_INVALID_CREDENTIALS";
        public const string UserDisabled = "AUTH_USER_DISABLED";
        public const string AdminSeedNotConfigured = "AUTH_ADMIN_SEED_NOT_CONFIGURED";
        public const string AccountLocked = "AUTH_ACCOUNT_LOCKED";
    }

    public static class Infrastructure
    {
        public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
        public const string AuditWriteFailed = "AUDIT_WRITE_FAILED";
        public const string LlmProviderUnavailable = "LLM_PROVIDER_UNAVAILABLE";
    }

    public static class Llm
    {
        public const string TokenBudgetExceeded = "TOKEN_BUDGET_EXCEEDED";
        public const string ImageBudgetExceeded = "IMAGE_BUDGET_EXCEEDED";
        public const string NoModelForFeature = "LLM_NO_MODEL_FOR_FEATURE";
        public const string ContextWindowExceeded = "LLM_CONTEXT_WINDOW_EXCEEDED";
        public const string EncryptionKeyInvalid = "LLM_ENCRYPTION_KEY_INVALID";
        public const string ProviderUnavailable = "LLM_PROVIDER_UNAVAILABLE";
        public const string ProviderNotFound = "PROVIDER_NOT_FOUND";
        public const string ModelNotFound = "MODEL_NOT_FOUND";
        public const string ModelAlreadyExists = "MODEL_ALREADY_EXISTS";
        public const string RoutingNotFound = "ROUTING_NOT_FOUND";
        public const string InvalidFeature = "INVALID_FEATURE";
    }

    public static class Validation
    {
        public const string Required = "VALIDATION_REQUIRED";
        public const string InvalidValue = "VALIDATION_INVALID_VALUE";
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
        public const string InvalidParent = "CARD_INVALID_PARENT";
        public const string ParentCycle = "CARD_PARENT_CYCLE";
        public const string BlockedMoveWarning = "CARD_BLOCKED_MOVE_WARNING";
        public const string ConcurrencyMismatch = "CARD_CONCURRENCY_MISMATCH";
        public const string ConcurrencyConflict = "CARD_CONCURRENCY_CONFLICT";
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

    public static class Attachments
    {
        public const string NotFound = "ATTACHMENT_NOT_FOUND";
        public const string UnsupportedContentType = "ATTACHMENT_UNSUPPORTED_CONTENT_TYPE";
        public const string FileTooLarge = "ATTACHMENT_FILE_TOO_LARGE";
        public const string FileStoreUnavailable = "ATTACHMENT_FILE_STORE_UNAVAILABLE";
    }

    public static class Mentions
    {
        public const string UserNotFound = "MENTION_USER_NOT_FOUND";
    }

    public static class Specs
    {
        public const string NotFound = "SPEC_NOT_FOUND";
        public const string DocumentVersionNotFound = "DOCUMENT_VERSION_NOT_FOUND";
        public const string MarkdownPayloadTooLarge = "MARKDOWN_PAYLOAD_TOO_LARGE";
        public const string CardDocumentProjectMismatch = "CARD_DOCUMENT_PROJECT_MISMATCH";
        public const string InvalidCardType = "SPEC_INVALID_CARD_TYPE";
        public const string DocTypeMismatch = "SPEC_DOC_TYPE_MISMATCH";
        public const string AlreadyExists = "SPEC_ALREADY_EXISTS";
    }

    public static class Plans
    {
        public const string NotFound = "PLAN_NOT_FOUND";
        public const string DocumentVersionNotFound = "DOCUMENT_VERSION_NOT_FOUND";
        public const string MarkdownPayloadTooLarge = "MARKDOWN_PAYLOAD_TOO_LARGE";
        public const string CardDocumentProjectMismatch = "CARD_DOCUMENT_PROJECT_MISMATCH";
        public const string EditForbiddenWhenDone = "PLAN_EDIT_FORBIDDEN_WHEN_DONE";
        public const string InvalidCardType = "PLAN_INVALID_CARD_TYPE";
        public const string SpecLinkNotAllowed = "PLAN_SPEC_LINK_NOT_ALLOWED";
        public const string SpecCardMismatch = "PLAN_SPEC_CARD_MISMATCH";
    }

    public static class Relationships
    {
        public const string NotFound = "RELATIONSHIP_NOT_FOUND";
        public const string Duplicate = "RELATIONSHIP_DUPLICATE";
        public const string CrossProjectDenied = "RELATIONSHIP_CROSS_PROJECT_DENIED";
        public const string Cycle = "RELATIONSHIP_CYCLE";
        public const string SelfDenied = "RELATIONSHIP_SELF_DENIED";
        public const string ArchiveImpactConfirmRequired = "CARD_ARCHIVE_IMPACT_CONFIRM_REQUIRED";
    }

    public static class Chat
    {
        public const string SessionNotFound = "CHAT_SESSION_NOT_FOUND";
        public const string MessageNotFound = "CHAT_MESSAGE_NOT_FOUND";
        public const string SessionClosed = "CHAT_SESSION_CLOSED";
        public const string SessionArchived = "CHAT_SESSION_ARCHIVED";
        public const string SessionNotOwner = "CHAT_SESSION_NOT_OWNER";
        public const string FolderMaxDepth = "CHAT_FOLDER_MAX_DEPTH";
        public const string DocumentNotOwned = "CHAT_DOCUMENT_NOT_OWNED";
        public const string StreamInProgress = "CHAT_STREAM_IN_PROGRESS";
        public const string ModelNoVision = "CHAT_MODEL_NO_VISION";
        public const string SummaryFailed = "CHAT_SUMMARY_FAILED";
        public const string PresetGroupNotFound = "CHAT_PRESET_GROUP_NOT_FOUND";
        public const string PersonalityNotFound = "CHAT_PERSONALITY_NOT_FOUND";
        public const string CardNotInProject = "CHAT_CARD_NOT_IN_PROJECT";
        public const string DocumentAlreadyAttached = "CHAT_DOCUMENT_ALREADY_ATTACHED";
        public const string EmbeddingFailed = "CHAT_EMBEDDING_FAILED";
    }
}
