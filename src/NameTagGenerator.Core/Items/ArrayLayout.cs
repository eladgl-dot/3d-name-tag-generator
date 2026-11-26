using System.Globalization;

namespace NameTagGenerator.Items;

/// <summary>
/// Columns follows the Columns number. The others work the count out from however many names there are.
/// FillBed packs tightly, turns tags where that helps, and starts more beds when one is full. New shapes go at the end: settings save the number.
/// </summary>
public enum ArrangeShape { Columns, OneRow, OneColumn, Square, FillBed }

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

/// <summary>
/// Where each tag goes. Turned: the tag is given a quarter turn (anticlockwise) before it is placed. Beds: which bed each tag is on;
/// bed b is centred on ArrayLayout.BedCentre(b). Only Fill the bed turns tags or uses more than one bed.
/// </summary>
public sealed record ArrayResult(IReadOnlyList<(double X, double Y)> Centers, int Columns, int Rows, double TotalWidth, double TotalHeight, bool FitsPlate)
{
    public IReadOnlyList<bool> Turned { get; init; } = new bool[Centers.Count];
    public IReadOnlyList<int> Beds { get; init; } = new int[Centers.Count];
    public int BedCount { get; init; } = 1;
}

/// <summary>Arranges tags in rows and columns, starting top right to follow Hebrew reading order.</summary>
public static class ArrayLayout
{
    public static ArrayResult Place(IReadOnlyList<(double W, double H)> sizes, ArrangeSettings s)
    {
        if (sizes.Count == 0) return new ArrayResult(Array.Empty<(double, double)>(), 0, 0, 0, 0, true);
        if (s.Shape == ArrangeShape.FillBed) return BedPacker.Pack(sizes, s);

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

    /// <summary>Space between beds side by side, in mm.</summary>
    public static double BedSpacing(ArrangeSettings s) => Math.Max(20, 0.2 * s.PlateWidth);

    /// <summary>The middle of bed b. Bed 0 is centred on the origin; the next ones follow to the right.</summary>
    public static (double X, double Y) BedCentre(int bed, ArrangeSettings s) => (bed * (s.PlateWidth + BedSpacing(s)), 0);

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

/// <summary>
/// "Fill the bed": packs tags as tightly as it can on as few beds as it can, turning a tag a quarter turn
/// when that fits better. Tags go on the first bed with room, so list order fills bed 1 first.
/// Uses the "maximal rectangles" method: it keeps every empty rectangle on a bed and picks the one
/// that leaves the least wasted strip beside the tag.
/// </summary>
static class BedPacker
{
    readonly record struct Rect(double X, double Y, double W, double H)
    {
        public double Right => X + W;
        public double Bottom => Y + H;
        public bool Contains(Rect o) => o.X >= X && o.Y >= Y && o.Right <= Right && o.Bottom <= Bottom;
        public bool Overlaps(Rect o) => o.X < Right && o.Right > X && o.Y < Bottom && o.Bottom > Y;
    }

    sealed class Bin
    {
        public List<Rect> Free { get; } = new();
        public List<(int Index, Rect Spot)> Placed { get; } = new();
    }

    const double Tolerance = 1e-9;

    public static ArrayResult Pack(IReadOnlyList<(double W, double H)> sizes, ArrangeSettings s)
    {
        // Each tag reserves a gap on its right and below; the bed grows by one gap to match, so tags can touch its edges.
        double binW = s.PlateWidth + s.GapX, binH = s.PlateHeight + s.GapY;
        var bins = new List<Bin>();
        var turned = new bool[sizes.Count];
        var fits = true;

        for (var i = 0; i < sizes.Count; i++)
        {
            double w = sizes[i].W + s.GapX, h = sizes[i].H + s.GapY;
            var placed = false;
            foreach (var bin in bins)
                if (TryPlace(bin, i, w, h, turned)) { placed = true; break; }
            if (placed) continue;

            var fresh = new Bin();
            fresh.Free.Add(new Rect(0, 0, binW, binH));
            bins.Add(fresh);
            if (TryPlace(fresh, i, w, h, turned)) continue;

            // Bigger than the bed either way: alone on its own bed, as it is.
            fits = false;
            fresh.Free.Clear();
            fresh.Placed.Add((i, new Rect(0, 0, w, h)));
        }

        // A tag alone on its bed has nothing to make room for: keep it the way it was drawn if it fits.
        foreach (var bin in bins)
        {
            if (bin.Placed.Count != 1) continue;
            var (index, _) = bin.Placed[0];
            double w = sizes[index].W + s.GapX, h = sizes[index].H + s.GapY;
            if (!turned[index] || w > binW + Tolerance || h > binH + Tolerance) continue;
            turned[index] = false;
            bin.Placed[0] = (index, new Rect(0, 0, w, h));
        }

        var centers = new (double X, double Y)[sizes.Count];
        var beds = new int[sizes.Count];
        for (var b = 0; b < bins.Count; b++)
        {
            var spots = bins[b].Placed;
            // Centre what is on the bed, and start top right, following Hebrew reading order.
            double right = spots.Max(p => p.Spot.Right) - s.GapX, bottom = spots.Max(p => p.Spot.Bottom) - s.GapY;
            var (bx, by) = ArrayLayout.BedCentre(b, s);
            foreach (var (index, spot) in spots)
            {
                double w = spot.W - s.GapX, h = spot.H - s.GapY;
                beds[index] = b;
                centers[index] = (bx + right / 2 - spot.X - w / 2, by + bottom / 2 - spot.Y - h / 2);
            }
        }

        var count = Math.Max(1, bins.Count);
        var totalWidth = count * s.PlateWidth + (count - 1) * ArrayLayout.BedSpacing(s);
        return new ArrayResult(centers, 0, 0, totalWidth, s.PlateHeight, fits) { Turned = turned, Beds = beds, BedCount = bins.Count };
    }

    static bool TryPlace(Bin bin, int index, double w, double h, bool[] turned)
    {
        Rect? best = null;
        var bestTurned = false;
        (double Short, double Long) bestScore = (double.MaxValue, double.MaxValue);
        foreach (var free in bin.Free)
            foreach (var turn in new[] { false, true })
            {
                var (tw, th) = turn ? (h, w) : (w, h);
                if (tw > free.W + Tolerance || th > free.H + Tolerance) continue;
                double dw = free.W - tw, dh = free.H - th;
                var score = (Math.Min(dw, dh), Math.Max(dw, dh));
                if (score.CompareTo(bestScore) >= 0) continue;
                (best, bestTurned, bestScore) = (new Rect(free.X, free.Y, tw, th), turn, score);
            }
        if (best is not { } spot) return false;

        turned[index] = bestTurned;
        bin.Placed.Add((index, spot));
        Split(bin.Free, spot);
        return true;
    }

    // Cuts the used spot out of every empty rectangle it touches, keeping the largest empty rectangles left over.
    static void Split(List<Rect> free, Rect used)
    {
        var next = new List<Rect>();
        foreach (var f in free)
        {
            if (!f.Overlaps(used)) { next.Add(f); continue; }
            if (used.X > f.X) next.Add(new Rect(f.X, f.Y, used.X - f.X, f.H));
            if (used.Right < f.Right) next.Add(new Rect(used.Right, f.Y, f.Right - used.Right, f.H));
            if (used.Y > f.Y) next.Add(new Rect(f.X, f.Y, f.W, used.Y - f.Y));
            if (used.Bottom < f.Bottom) next.Add(new Rect(f.X, used.Bottom, f.W, f.Bottom - used.Bottom));
        }
        // Drop rectangles lying inside another one.
        free.Clear();
        for (var i = 0; i < next.Count; i++)
        {
            var inside = false;
            for (var j = 0; j < next.Count && !inside; j++)
                inside = i != j && next[j].Contains(next[i]) && (next[i] != next[j] || j < i);
            if (!inside && next[i].W > Tolerance && next[i].H > Tolerance) free.Add(next[i]);
        }
    }
}
