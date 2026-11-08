using Clipper2Lib;

namespace NameTagGenerator.Geometry;

public enum PipResult { Inside, Outside }

public readonly record struct Box(long MinX, long MinY, long MaxX, long MaxY)
{
    public long Width => MaxX - MinX;
    public long Height => MaxY - MinY;
}

/// <summary>2D outline operations. Everything works in integer units: 1 mm = <see cref="Scale"/> units.</summary>
public static class Poly
{
    public const double Scale = 2000;
    const double ArcTolerance = 0.02 * Scale;
    const int CircleSegments = 60;
    const double CleanToleranceUnits = 2;

    public static long U(double mm) => (long)Math.Round(mm * Scale);
    public static double Mm(long units) => units / Scale;

    public static Path64 Rect(double x0, double y0, double x1, double y1) => new()
    {
        new Point64(U(x0), U(y0)), new Point64(U(x1), U(y0)), new Point64(U(x1), U(y1)), new Point64(U(x0), U(y1)),
    };

    public static Path64 Circle(double cx, double cy, double diameter)
    {
        var path = new Path64(CircleSegments);
        for (var i = 0; i < CircleSegments; i++)
        {
            var a = 2 * Math.PI * i / CircleSegments;
            path.Add(new Point64(U(cx + diameter / 2 * Math.Cos(a)), U(cy + diameter / 2 * Math.Sin(a))));
        }
        return path;
    }

    public static Paths64 Union(Paths64 a) => Clipper.Union(a, FillRule.NonZero);
    public static Paths64 Union(Paths64 a, Paths64 b) => Clipper.Union(a, b, FillRule.NonZero);
    public static Paths64 Difference(Paths64 a, Paths64 b) => Clipper.Difference(a, b, FillRule.NonZero);
    public static Paths64 Intersect(Paths64 a, Paths64 b) => Clipper.Intersect(a, b, FillRule.NonZero);

    /// <summary>Grows (or, when negative, shrinks) an outline with round corners.</summary>
    public static Paths64 Inflate(Paths64 area, double mm)
    {
        if (area.Count == 0) return new Paths64();
        if (mm == 0) return Union(area);
        return Clipper.InflatePaths(area, mm * Scale, JoinType.Round, EndType.Polygon, 2.0, ArcTolerance);
    }

    /// <summary>Fills every gap and notch narrower than the given width, leaving the rest of the shape where it was.</summary>
    public static Paths64 Close(Paths64 area, double widthMm) =>
        widthMm <= 0 ? area : Clean(Inflate(Inflate(area, widthMm / 2), -widthMm / 2));

    /// <summary>
    /// Removes features of about a micron. Growing and shrinking can leave an edge doubling back on itself
    /// that thinly, far too small to print, and such slivers stop the mesh from closing.
    /// </summary>
    public static Paths64 Clean(Paths64 area) => Union(Clipper.SimplifyPaths(area, CleanToleranceUnits));

    /// <summary>Removes all holes, keeping only outer contours.</summary>
    public static Paths64 FillHoles(Paths64 area) => new(area.Where(Clipper.IsPositive));

    public static Paths64 Translate(Paths64 area, long dx, long dy) => Clipper.TranslatePaths(area, dx, dy);

    /// <summary>Flips left and right. Reversing each path keeps outer contours and holes as they were.</summary>
    public static Paths64 Mirror(Paths64 area) => Reversed(new Paths64(area.Select(path => new Path64(path.Select(p => new Point64(-p.X, p.Y))))));

    /// <summary>The same outlines running the other way, which turns a solid area into a pocket.</summary>
    public static Paths64 Reversed(Paths64 area) => new(area.Select(path => new Path64(Enumerable.Reverse(path))));

    public static Box Bounds(Paths64 area)
    {
        long minX = long.MaxValue, minY = long.MaxValue, maxX = long.MinValue, maxY = long.MinValue;
        foreach (var path in area)
            foreach (var p in path)
            {
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
        return area.Count == 0 ? default : new Box(minX, minY, maxX, maxY);
    }

    /// <summary>Counts separate solid islands: each outer contour is one.</summary>
    public static int CountIslands(Paths64 area) => area.Count(Clipper.IsPositive);

    public static PipResult PointInArea(Point64 point, Paths64 area)
    {
        var inside = false;
        foreach (var path in area)
            if (Clipper.PointInPolygon(point, path) == PointInPolygonResult.IsInside)
                inside = !inside;
        return inside ? PipResult.Inside : PipResult.Outside;
    }

    /// <summary>Convex hull of a set of outlines, counter-clockwise.</summary>
    public static Path64 ConvexHull(params Path64[] paths)
    {
        var pts = paths.SelectMany(p => p).Distinct().OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        if (pts.Count < 3) return new Path64(pts);

        static double Cross(Point64 o, Point64 a, Point64 b) =>
            (double)(a.X - o.X) * (b.Y - o.Y) - (double)(a.Y - o.Y) * (b.X - o.X);

        var hull = new List<Point64>(pts.Count + 1);
        foreach (var p in pts)
        {
            while (hull.Count >= 2 && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        var lowerCount = hull.Count + 1;
        for (var i = pts.Count - 2; i >= 0; i--)
        {
            var p = pts[i];
            while (hull.Count >= lowerCount && Cross(hull[^2], hull[^1], p) <= 0) hull.RemoveAt(hull.Count - 1);
            hull.Add(p);
        }
        hull.RemoveAt(hull.Count - 1);
        return new Path64(hull);
    }
}
