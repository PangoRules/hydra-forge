namespace HydraForge.Tui.Rendering;

// Pure geometry for windowing a fixed-height-item list (e.g. 2 lines per row)
// so the current selection stays visible without the panel growing past its
// height cap. Kept Spectre-free so it's cheap to unit test.
public static class ListScrollWindow
{
    public record Window(int Start, int End, bool HasMoreAbove, bool HasMoreBelow);

    public static Window Compute(int count, int selectedIndex, int maxVisible)
    {
        if (count == 0 || maxVisible <= 0)
            return new Window(0, 0, false, false);

        if (count <= maxVisible)
            return new Window(0, count, false, false);

        var anchor = Math.Clamp(selectedIndex, 0, count - 1);
        var start = Math.Clamp(anchor - maxVisible / 2, 0, count - maxVisible);
        var end = start + maxVisible;
        return new Window(start, end, start > 0, end < count);
    }
}
