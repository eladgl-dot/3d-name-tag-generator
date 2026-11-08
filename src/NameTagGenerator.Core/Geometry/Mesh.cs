using Clipper2Lib;
using LibTessDotNet;

namespace NameTagGenerator.Geometry;

/// <summary>
/// Triangle mesh on an integer grid. Identical coordinates always become the same vertex,
/// which is what keeps neighbouring faces joined with no gaps.
/// </summary>
public sealed class Mesh
{
    readonly Dictionary<(long, long, long), int> _lookup = new();

    public List<(long X, long Y, long Z)> Vertices { get; } = new();
    public List<int> Indices { get; } = new();
    public int TriangleCount => Indices.Count / 3;
    public bool IsEmpty => Indices.Count == 0;

    public int Vertex(long x, long y, long z)
    {
        if (_lookup.TryGetValue((x, y, z), out var index)) return index;
        index = Vertices.Count;
        Vertices.Add((x, y, z));
        _lookup[(x, y, z)] = index;
        return index;
    }

    public void Triangle(int a, int b, int c)
    {
        if (a == b || b == c || a == c) return;
        Indices.Add(a);
        Indices.Add(b);
        Indices.Add(c);
    }

    /// <summary>Solid prism of an outline between two heights.</summary>
    public void Extrude(Paths64 area, long z0, long z1)
    {
        if (area.Count == 0 || z1 <= z0) return;
        Walls(area, z0, z1);
        Cap(area, z1, facingUp: true);
        Cap(area, z0, facingUp: false);
    }

    /// <summary>Upright walls along an outline, facing out from its solid side. Reverse the outline for the inside of a pocket.</summary>
    public void Walls(Paths64 area, long z0, long z1)
    {
        foreach (var path in area)
            for (var i = 0; i < path.Count; i++)
            {
                var p = path[i];
                var q = path[(i + 1) % path.Count];
                int p0 = Vertex(p.X, p.Y, z0), q0 = Vertex(q.X, q.Y, z0), q1 = Vertex(q.X, q.Y, z1), p1 = Vertex(p.X, p.Y, z1);
                Triangle(p0, q0, q1);
                Triangle(p0, q1, p1);
            }
    }

    /// <summary>A flat face filling an outline. Outlines nested inside each other become holes.</summary>
    public void Cap(Paths64 area, long z, bool facingUp)
    {
        foreach (var (a, b, c) in Triangulator.Triangulate(area))
        {
            if (facingUp) Triangle(Vertex(a.X, a.Y, z), Vertex(b.X, b.Y, z), Vertex(c.X, c.Y, z));
            else Triangle(Vertex(c.X, c.Y, z), Vertex(b.X, b.Y, z), Vertex(a.X, a.Y, z));
        }
    }

    /// <summary>Adds a copy of another mesh at an offset. The two stay separate bodies.</summary>
    public void AddMoved(Mesh other, long dx, long dy)
    {
        var start = Vertices.Count;
        foreach (var v in other.Vertices) Vertices.Add((v.X + dx, v.Y + dy, v.Z));
        foreach (var i in other.Indices) Indices.Add(start + i);
    }

    /// <summary>A copy with every corner moved. Faces stay joined exactly as they are here, so a closed body stays closed.</summary>
    public Mesh Mapped(Func<(long X, long Y, long Z), (long X, long Y, long Z)> move)
    {
        var copy = new Mesh();
        foreach (var v in Vertices) copy.Vertices.Add(move(v));
        copy.Indices.AddRange(Indices);
        return copy;
    }

    public double VolumeMm3
    {
        get
        {
            var sum = 0.0;
            for (var i = 0; i < Indices.Count; i += 3)
            {
                var (ax, ay, az) = Mm(Indices[i]);
                var (bx, by, bz) = Mm(Indices[i + 1]);
                var (cx, cy, cz) = Mm(Indices[i + 2]);
                sum += ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
            }
            return sum / 6;
        }
    }

    (double, double, double) Mm(int i)
    {
        var v = Vertices[i];
        return (Poly.Mm(v.X), Poly.Mm(v.Y), Poly.Mm(v.Z));
    }

    /// <summary>True when every edge is shared by exactly two faces running in opposite directions.</summary>
    public bool IsManifold() => CountBadEdges() == 0;

    public string DescribeOpenEdges()
    {
        var bad = FindBadEdges();
        var first = string.Join("; ", bad.OrderBy(e => e.From.Y).ThenBy(e => e.From.X).Take(14).Select(e => $"{e.From}>{e.To} x{e.Uses}"));
        return $"{bad.Count} bad edges of {Indices.Count} edge uses: {first}";
    }

    int CountBadEdges() => FindBadEdges().Count;

    /// <summary>Edges that do not join exactly two faces, as vertex pairs. Empty for a sound mesh.</summary>
    public List<((long X, long Y, long Z) From, (long X, long Y, long Z) To, int Uses)> FindBadEdges()
    {
        var edges = new Dictionary<(int, int), int>(Indices.Count);
        void Count(int a, int b) => edges[(a, b)] = edges.GetValueOrDefault((a, b)) + 1;
        for (var i = 0; i < Indices.Count; i += 3)
        {
            Count(Indices[i], Indices[i + 1]);
            Count(Indices[i + 1], Indices[i + 2]);
            Count(Indices[i + 2], Indices[i]);
        }
        return edges
            .Where(e => e.Value != 1 || edges.GetValueOrDefault((e.Key.Item2, e.Key.Item1)) != 1)
            .Select(e => (Vertices[e.Key.Item1], Vertices[e.Key.Item2], e.Value))
            .ToList();
    }
}

public static class Triangulator
{
    /// <summary>Fills an outline with counter-clockwise triangles, using only the outline's own points.</summary>
    public static List<(Point64 A, Point64 B, Point64 C)> Triangulate(Paths64 area)
    {
        var result = new List<(Point64, Point64, Point64)>();
        // Zero-area triangles must stay: dropping one leaves a hairline gap beside a point lying on a straight edge.
        var tess = new Tess { NoEmptyPolygons = false };
        foreach (var path in area)
        {
            if (path.Count < 3) continue;
            var contour = new ContourVertex[path.Count];
            for (var i = 0; i < path.Count; i++)
                contour[i].Position = new Vec3(path[i].X, path[i].Y, 0);
            tess.AddContour(contour, ContourOrientation.Original);
        }
        tess.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3);

        Point64 At(int index)
        {
            var p = tess.Vertices[index].Position;
            return new Point64((long)Math.Round((double)p.X), (long)Math.Round((double)p.Y));
        }

        for (var i = 0; i < tess.ElementCount; i++)
        {
            Point64 a = At(tess.Elements[i * 3]), b = At(tess.Elements[i * 3 + 1]), c = At(tess.Elements[i * 3 + 2]);
            var twiceArea = (double)(b.X - a.X) * (c.Y - a.Y) - (double)(b.Y - a.Y) * (c.X - a.X);
            result.Add(twiceArea >= 0 ? (a, b, c) : (a, c, b));
        }
        return result;
    }
}
