namespace HydraForge.Application.Projects;

public enum ColumnTemplate
{
    Software = 1,
    General = 2,
    Blank = 3,
}

public static class ColumnTemplates
{
    private static readonly Dictionary<ColumnTemplate, string[]> Templates = new()
    {
        [ColumnTemplate.Software] =
        [
            "Backlog",
            "Spec-ing",
            "Planned",
            "In Dev",
            "In Review",
            "Done",
        ],
        [ColumnTemplate.General] = ["Backlog", "In Progress", "Review", "Done"],
        [ColumnTemplate.Blank] = ["To Do", "Done"],
    };

    public static string[] Get(ColumnTemplate template)
    {
        return Templates[template];
    }
}
