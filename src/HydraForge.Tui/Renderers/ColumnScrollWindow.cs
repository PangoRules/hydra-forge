namespace HydraForge.Tui.Renderers;

// Pure geometry for deciding which cards fit in a column's visible height and
// where to draw the scroll window — kept Spectre-free so it's cheap to unit test.
public static class ColumnScrollCalculator
{
    public const int MaxBadgesPerCard = 5;

    public static int CardBoxHeight(int badgeCount, int extraLines = 0)
    {
        var shown = Math.Min(badgeCount, MaxBadgesPerCard);
        var overflowLine = badgeCount > MaxBadgesPerCard ? 1 : 0;
        return 2 /* border */ + 1 /* title */ + shown + overflowLine + extraLines;
    }

    public record ScrollWindow(int Start, int End, bool HasMoreAbove, bool HasMoreBelow);

    // activeIndex: the currently selected card in a focused column (window scrolls
    // to keep it visible). Pass null for unfocused columns — they're always top-anchored.
    public static ScrollWindow ComputeVisibleRange(
        IReadOnlyList<int> cardHeights,
        int? activeIndex,
        int innerHeight
    )
    {
        var count = cardHeights.Count;
        if (count == 0)
            return new ScrollWindow(0, 0, false, false);

        if (innerHeight <= 0)
            return new ScrollWindow(0, 0, true, true);

        int Height(int start, int end)
        {
            var h = 0;
            for (var i = start; i < end; i++)
            {
                h += cardHeights[i];
                if (i > start)
                    h += 1; // gap between cards
            }
            return h;
        }

        int start;
        int end;

        if (activeIndex is null)
        {
            start = 0;
            end = 0;
            while (end < count && Height(start, end + 1) <= innerHeight)
                end++;
            end = Math.Max(end, Math.Min(1, count));
        }
        else
        {
            var anchor = Math.Clamp(activeIndex.Value, 0, count - 1);
            start = anchor;
            end = anchor + 1;

            // Grow the window outward — downward first, then upward — while it still fits.
            bool grew;
            do
            {
                grew = false;
                if (end < count && Height(start, end + 1) <= innerHeight)
                {
                    end++;
                    grew = true;
                }
                if (start > 0 && Height(start - 1, end) <= innerHeight)
                {
                    start--;
                    grew = true;
                }
            } while (grew);
        }

        var hasMoreAbove = start > 0;
        var hasMoreBelow = end < count;

        // Scroll indicators cost a row each — shrink the window to make room, trimming
        // the side farther from the active card (or the bottom, when top-anchored).
        var anchorIndex = activeIndex ?? start;
        while (end - start > 1 && Height(start, end) + (hasMoreAbove ? 1 : 0) + (hasMoreBelow ? 1 : 0) > innerHeight)
        {
            if (activeIndex is not null && end - 1 - anchorIndex > anchorIndex - start)
                end--;
            else if (start < anchorIndex)
                start++;
            else
                end--;

            hasMoreAbove = start > 0;
            hasMoreBelow = end < count;
        }

        return new ScrollWindow(start, end, hasMoreAbove, hasMoreBelow);
    }
}
