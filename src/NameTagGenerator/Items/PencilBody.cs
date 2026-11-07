using Clipper2Lib;
using NameTagGenerator.Geometry;

namespace NameTagGenerator.Items;

/// <summary>
/// The pencil top's base: a flat outline of given height, plus a tube along X that joins the
/// letters, minus a round hole along X.
///
/// Rather than a general 3D cut, the shape is built as strips across Y. Inside one strip every
/// surface height changes linearly with Y, so each strip is just triangulated outlines at known
/// heights plus side walls. This keeps the mesh closed and needs no 3D cutting library.
/// </summary>
public static class PencilBody
{
    const int Samples = 48;

    public static Mesh Build(Paths64 area, double height, double holeDiameter, double tubeDiameter)
    {
        var mesh = new Mesh();
        if (area.Count == 0) return mesh;

        var holeR = holeDiameter / 2;
        var tubeR = Math.Max(tubeDiameter / 2, holeR + 0.4);
        var midZ = height / 2;
        var box = Poly.Bounds(area);
        // The tube runs slightly past the outline, so an outline edge can never sit exactly on its end.
        var tubeOverhang = Poly.U(0.05);
        long x0 = box.MinX - tubeOverhang, x1 = box.MaxX + tubeOverhang;

        // Strip lines stay clear of outline points. On a point, a real horizontal edge would look
        // like an internal strip edge and lose its wall; very near one, the sliver left between
        // the point and the line is too small for the clipper to keep, which opens a gap.
        const int clearance = 4;
        var outlineYs = area.SelectMany(p => p).Select(p => p.Y).Distinct().OrderBy(y => y).ToArray();
        bool TooClose(long y)
        {
            var i = Array.BinarySearch(outlineYs, y);
            if (i >= 0) return true;
            i = ~i;
            return (i < outlineYs.Length && outlineYs[i] - y < clearance) || (i > 0 && y - outlineYs[i - 1] < clearance);
        }

        var lines = new SortedSet<long>();
        long Line(double mm)
        {
            var y = Poly.U(mm);
            while (TooClose(y)) y++;
            // Lines nearly on top of each other would make the same kind of sliver.
            var near = lines.GetViewBetween(y - clearance, y + clearance);
            if (near.Count > 0) return near.Min;
            lines.Add(y);
            return y;
        }

        var tubeLow = Line(-tubeR);
        var tubeHigh = Line(tubeR);
        var holeLow = Line(-holeR);
        var holeHigh = Line(holeR);
        for (var i = 1; i < Samples; i++)
        {
            var c = Math.Cos(Math.PI * i / Samples);
            Line(tubeR * c);
            if (Math.Abs(holeR * c) < holeR) Line(holeR * c);
        }
        if (tubeR > midZ)
        {
            var crossing = Math.Sqrt(tubeR * tubeR - midZ * midZ);
            Line(crossing);
            Line(-crossing);
        }
        Line(Poly.Mm(Math.Min(box.MinY, tubeLow)) - 1);
        Line(Poly.Mm(Math.Max(box.MaxY, tubeHigh)) + 1);

        static double Half(double r, double yMm) => Math.Sqrt(Math.Max(0, r * r - yMm * yMm));
        double TubeHalf(long y) => y <= tubeLow || y >= tubeHigh ? 0 : Half(tubeR, Poly.Mm(y));
        double HoleHalf(long y) => y <= holeLow || y >= holeHigh ? 0 : Half(holeR, Poly.Mm(y));
        // On these lines a neighbouring wall has a point at mid height, so walls must share it.
        bool IsSplitLine(long y) => y == tubeLow || y == tubeHigh || y == holeLow || y == holeHigh;

        var ys = lines.ToArray();
        for (var s = 0; s + 1 < ys.Length; s++)
        {
            long ya = ys[s], yb = ys[s + 1];
            var inTube = ya >= tubeLow && yb <= tubeHigh;
            var inHole = ya >= holeLow && yb <= holeHigh;

            var strip = new Paths64 { new() { new Point64(x0 - 1, ya), new Point64(x1 + 1, ya), new Point64(x1 + 1, yb), new Point64(x0 - 1, yb) } };
            var flat = Poly.Intersect(area, strip);

            Column Heights(bool isFlat, long y)
            {
                var tube = TubeHalf(y);
                var hole = inHole ? HoleHalf(y) : 0;
                var low = isFlat ? Math.Min(0, midZ - tube) : midZ - tube;
                var high = isFlat ? Math.Max(height, midZ + tube) : midZ + tube;
                return new Column(Poly.U(low), Poly.U(midZ - hole), Poly.U(midZ + hole), Poly.U(high));
            }

            var strips = new StripContext(mesh, ya, yb, inHole, Heights(true, ya), Heights(true, yb), Heights(false, ya), Heights(false, yb));

            // Areas with the full base height.
            strips.Surfaces(flat, isFlat: true);
            foreach (var path in flat)
                for (var i = 0; i < path.Count; i++)
                {
                    Point64 p = path[i], q = path[(i + 1) % path.Count];
                    if (p.Y == q.Y && (p.Y == ya || p.Y == yb)) continue;
                    var besideTube = inTube && (p.X + q.X) / 2 > x0 && (p.X + q.X) / 2 < x1;
                    if (besideTube) strips.WallAboveAndBelowTube(p, q);
                    else strips.FullWall(p, q, Poly.U(midZ), IsSplitLine);
                }

            // The tube alone, between the letters.
            if (!inTube) continue;
            var tubeRect = new Paths64 { new() { new Point64(x0, ya), new Point64(x1, ya), new Point64(x1, yb), new Point64(x0, yb) } };
            var bridge = Poly.Difference(tubeRect, area);
            strips.Surfaces(bridge, isFlat: false);
            foreach (var path in bridge)
                for (var i = 0; i < path.Count; i++)
                {
                    Point64 p = path[i], q = path[(i + 1) % path.Count];
                    if (p.X == q.X && (p.X == x0 || p.X == x1)) strips.TubeEndWall(p, q, Poly.U(midZ), IsSplitLine);
                }
        }
        return mesh;
    }

    readonly record struct Column(long Low, long HoleLow, long HoleHigh, long High);

    sealed class StripContext
    {
        readonly Mesh _mesh;
        readonly long _ya, _yb;
        readonly bool _inHole;
        readonly Column _flatA, _flatB, _tubeA, _tubeB;

        public StripContext(Mesh mesh, long ya, long yb, bool inHole, Column flatA, Column flatB, Column tubeA, Column tubeB)
        {
            (_mesh, _ya, _yb, _inHole, _flatA, _flatB, _tubeA, _tubeB) = (mesh, ya, yb, inHole, flatA, flatB, tubeA, tubeB);
        }

        long Lerp(long a, long b, long y) => a == b ? a : a + (long)Math.Round((double)(b - a) * (y - _ya) / (_yb - _ya));

        Column At(bool isFlat, long y)
        {
            var (a, b) = isFlat ? (_flatA, _flatB) : (_tubeA, _tubeB);
            return new Column(Lerp(a.Low, b.Low, y), Lerp(a.HoleLow, b.HoleLow, y), Lerp(a.HoleHigh, b.HoleHigh, y), Lerp(a.High, b.High, y));
        }

        int V(Point64 p, long z) => _mesh.Vertex(p.X, p.Y, z);

        public void Surfaces(Paths64 cell, bool isFlat)
        {
            if (cell.Count == 0) return;
            foreach (var (a, b, c) in Triangulator.Triangulate(cell))
            {
                Column ca = At(isFlat, a.Y), cb = At(isFlat, b.Y), cc = At(isFlat, c.Y);
                _mesh.Triangle(V(c, cc.Low), V(b, cb.Low), V(a, ca.Low));
                _mesh.Triangle(V(a, ca.High), V(b, cb.High), V(c, cc.High));
                if (!_inHole) continue;
                _mesh.Triangle(V(a, ca.HoleLow), V(b, cb.HoleLow), V(c, cc.HoleLow));
                _mesh.Triangle(V(c, cc.HoleHigh), V(b, cb.HoleHigh), V(a, ca.HoleHigh));
            }
        }

        void Quad(Point64 p, Point64 q, long pLow, long qLow, long qHigh, long pHigh)
        {
            _mesh.Triangle(V(p, pLow), V(q, qLow), V(q, qHigh));
            _mesh.Triangle(V(p, pLow), V(q, qHigh), V(p, pHigh));
        }

        /// <summary>Side of the full-height area where the tube continues beyond it.</summary>
        public void WallAboveAndBelowTube(Point64 p, Point64 q)
        {
            Column fp = At(true, p.Y), fq = At(true, q.Y), tp = At(false, p.Y), tq = At(false, q.Y);
            Quad(p, q, fp.Low, fq.Low, tq.Low, tp.Low);
            Quad(p, q, tp.High, tq.High, fq.High, fp.High);
        }

        /// <summary>Side of the full-height area with nothing beside it.</summary>
        public void FullWall(Point64 p, Point64 q, long midZ, Func<long, bool> splitAt)
        {
            Column fp = At(true, p.Y), fq = At(true, q.Y);
            if (_inHole)
            {
                Quad(p, q, fp.Low, fq.Low, fq.HoleLow, fp.HoleLow);
                Quad(p, q, fp.HoleHigh, fq.HoleHigh, fq.High, fp.High);
                return;
            }

            SplitWall(p, q, fp, fq, midZ, splitAt);
        }

        /// <summary>Open end of the tube at the far left or right.</summary>
        public void TubeEndWall(Point64 p, Point64 q, long midZ, Func<long, bool> splitAt)
        {
            Column tp = At(false, p.Y), tq = At(false, q.Y);
            if (_inHole)
            {
                Quad(p, q, tp.Low, tq.Low, tq.HoleLow, tp.HoleLow);
                Quad(p, q, tp.HoleHigh, tq.HoleHigh, tq.High, tp.High);
                return;
            }

            SplitWall(p, q, tp, tq, midZ, splitAt);
        }

        /// <summary>A wall from low to high, with an extra mid-height point on any end that needs one.</summary>
        void SplitWall(Point64 p, Point64 q, Column cp, Column cq, long midZ, Func<long, bool> splitAt)
        {
            int p0 = V(p, cp.Low), q0 = V(q, cq.Low), q1 = V(q, cq.High), p1 = V(p, cp.High);
            bool splitP = splitAt(p.Y), splitQ = splitAt(q.Y);
            if (splitP && splitQ)
            {
                int pm = V(p, midZ), qm = V(q, midZ);
                _mesh.Triangle(p0, q0, qm);
                _mesh.Triangle(p0, qm, pm);
                _mesh.Triangle(pm, qm, q1);
                _mesh.Triangle(pm, q1, p1);
            }
            else if (splitP)
            {
                var pm = V(p, midZ);
                _mesh.Triangle(p0, q0, pm);
                _mesh.Triangle(pm, q0, q1);
                _mesh.Triangle(pm, q1, p1);
            }
            else if (splitQ)
            {
                var qm = V(q, midZ);
                _mesh.Triangle(p0, q0, qm);
                _mesh.Triangle(p0, qm, p1);
                _mesh.Triangle(p1, qm, q1);
            }
            else
            {
                _mesh.Triangle(p0, q0, q1);
                _mesh.Triangle(p0, q1, p1);
            }
        }
    }
}
