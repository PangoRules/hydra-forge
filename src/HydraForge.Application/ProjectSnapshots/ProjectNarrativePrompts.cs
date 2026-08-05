namespace HydraForge.Application.ProjectSnapshots;

public static class ProjectNarrativePrompts
{
    public const string SystemPrompt =
        "Generate a concise project narrative (3-5 sentences) based on the following project snapshot. "
        + "The snapshot includes the project's own name and description, plus its cards (with their own "
        + "descriptions where set) grouped by column. "
        + "Infer what the project is actually about from that name/description/card content first, and let "
        + "the tone match — a reading list, a hobby tracker, or a personal errand board should not read like "
        + "a software sprint update. Only use words like \"workflow\", \"development\", or \"deployment\" if the "
        + "project's own content is actually about building/shipping something. "
        + "Describe the current state, key themes, and notable work items. Be descriptive but succinct.";
}
