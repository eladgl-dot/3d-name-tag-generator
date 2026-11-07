using System.Globalization;

namespace NameTagGenerator.Items;

/// <summary>Columns follows the Columns number. The others work the count out from however many names there are.</summary>
public enum ArrangeShape { Columns, OneRow, OneColumn, Square }

public enum ArrangeOrder { AsListed, Alphabetical, BySize }

public sealed class ArrangeSettings
{
    public ArrangeShape Shape { get; set; }
    public ArrangeOrder Order { get; set; }
    /// <summary>Number of columns; 0 means as many as fit on the plate. Used only by the Columns shape.</summary>
    public int Columns { get; set; }
    public double GapX { get; set; } = 5;
    public double GapY { get; set; } = 5;
    public double PlateWidth { get; set; } = 256;
    public double PlateHeight { get; set; } = 256;
}

public sealed record ArrayResult(IReadOnlyList<(double X, double Y)> Centers, int Columns, int Rows, double TotalWidth, double TotalHeight, bool FitsPlate);

/// <summary>Arranges tags in rows and columns, starting top right to follow Hebrew reading order.</summary>
public static class ArrayLayout
{
    public static ArrayResult Place(IReadOnlyList<(double W, double H)> sizes, ArrangeSettings s)
    {
        if (sizes.Count == 0) return new ArrayResult(Array.Empty<(double, double)>(), 0, 0, 0, 0, true);

        var columns = s.Shape switch
        {
            ArrangeShape.OneRow => sizes.Count,
            ArrangeShape.OneColumn => 1,
            ArrangeShape.Square => (int)Math.Ceiling(Math.Sqrt(sizes.Count)),
            _ => s.Columns > 0 ? Math.Min(s.Columns, sizes.Count) : AutoColumns(sizes, s),
        };
        var rows = (int)Math.Ceiling(sizes.Count / (double)columns);

        var colWidths = new double[columns];
        var rowHeights = new double[rows];
        for (var i = 0; i < sizes.Count; i++)
        {
            colWidths[i % columns] = Math.Max(colWidths[i % columns], sizes[i].W);
            rowHeights[i / columns] = Math.Max(rowHeights[i / columns], sizes[i].H);
        }

        var totalWidth = colWidths.Sum() + s.GapX * (columns - 1);
        var totalHeight = rowHeights.Sum() + s.GapY * (rows - 1);

        var colX = new double[columns];
        var x = totalWidth / 2;
        for (var c = 0; c < columns; c++)
        {
            colX[c] = x - colWidths[c] / 2;
            x -= colWidths[c] + s.GapX;
        }

        var rowY = new double[rows];
        var y = totalHeight / 2;
        for (var r = 0; r < rows; r++)
        {
            rowY[r] = y - rowHeights[r] / 2;
            y -= rowHeights[r] + s.GapY;
        }

        var centers = new (double, double)[sizes.Count];
        for (var i = 0; i < sizes.Count; i++)
            centers[i] = (colX[i % columns], rowY[i / columns]);

        var fits = totalWidth <= s.PlateWidth && totalHeight <= s.PlateHeight;
        return new ArrayResult(centers, columns, rows, totalWidth, totalHeight, fits);
    }

    /// <summary>The positions of the names in the order they go on the plate. Equal names keep their list order.</summary>
    public static IReadOnlyList<int> Order(IReadOnlyList<(string Name, double Width)> tags, ArrangeOrder order)
    {
        var indexes = Enumerable.Range(0, tags.Count);
        return order switch
        {
            ArrangeOrder.Alphabetical => indexes.OrderBy(i => tags[i].Name, StringComparer.Create(Hebrew, ignoreCase: true)).ToList(),
            ArrangeOrder.BySize => indexes.OrderByDescending(i => tags[i].Width).ToList(),
            _ => indexes.ToList(),
        };
    }

    static readonly CultureInfo Hebrew = CultureInfo.GetCultureInfo("he-IL");

    static int AutoColumns(IReadOnlyList<(double W, double H)> sizes, ArrangeSettings s)
    {
        for (var columns = sizes.Count; columns > 1; columns--)
        {
            var widths = new double[columns];
            for (var i = 0; i < sizes.Count; i++) widths[i % columns] = Math.Max(widths[i % columns], sizes[i].W);
            if (widths.Sum() + s.GapX * (columns - 1) <= s.PlateWidth) return columns;
        }
        return 1;
    }
}
