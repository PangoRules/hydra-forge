namespace HydraForge.Domain.Enums;

public enum CardType
{
    Task = 1,
    Issue = 2,   // was Bug
    // 3 intentionally skipped — was Spec; rows migrated to Goal in MigrateSpecCardsToGoal
    Idea = 4,
    Goal = 5     // was Epic
}
