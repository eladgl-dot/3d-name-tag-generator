using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Text;

namespace NameTagGenerator.Items;

/// <summary>A wedge that stands on a desk with the name on its leaning face. Prints as it stands, with no supports.</summary>
public sealed class DeskPlateItem : IItemType
{
    public string Id => "deskplate";
    public string Name => "Desk name plate";
    public int Order => 4;
    public override string ToString() => Name;

    public IReadOnlyList<Field> Fields { get; } = new[]
    {
        new Field(Keys.Tilt, "Lean back (degrees)", Min: 5, Step: 1),
        new Field(Keys.StandDepth, "Stand depth (mm)", Min: 5, Step: 1),
    };

    public IReadOnlyDictionary<string, double> Defaults { get; } = new Dictionary<string, double>
    {
        [Keys.FontSize] = 20, [Keys.BaseHeight] = 4, [Keys.LetterHeight] = 1.2, [Keys.Outline] = 5, [Keys.Weight] = 0, [Keys.ConnectorOutline] = 0, [Keys.FillGaps] = 4,
        [Keys.Tilt] = 20, [Keys.StandDepth] = 25,
    };

    public ItemModel Build(TextShape text, ItemSettings s)
    {
        var placed = TagAreas.Position(text, TagAreas.Center);
        var (letters, outline) = TagAreas.Build(placed, s);
        var box = Poly.Bounds(outline);

        var tilt = Math.Clamp(s[Keys.Tilt], 5, 45) * Math.PI / 180;
        var (sin, cos) = Math.SinCos(tilt);
        var faceLength = Poly.Mm(box.Height);
        var width = Poly.Mm(box.Width);
        var topThickness = s[Keys.BaseHeight];

        // Side view, front edge at the origin: Y goes back, Z goes up.
        var faceTopY = faceLength * sin;
        var faceTopZ = faceLength * cos;
        var backTopY = faceTopY + topThickness * cos;
        var backTopZ = faceTopZ - topThickness * sin;
        var depth = Math.Max(s[Keys.StandDepth], backTopY + 1);

        var side = new Paths64
        {
            new Path64
            {
                new Point64(0, 0), new Point64(Poly.U(depth), 0),
                new Point64(Poly.U(backTopY), Poly.U(backTopZ)), new Point64(Poly.U(faceTopY), Poly.U(faceTopZ)),
            },
        };
        var sideways = new Mesh();
        sideways.Extrude(side, Poly.U(-width / 2), Poly.U(width / 2));
        // The side view becomes Y and Z, and its thickness becomes the width across X.
        var body = sideways.Mapped(v => (v.Z, v.X, v.Y));

        var flat = new Mesh();
        var centreX = (box.MinX + box.MaxX) / 2;
        flat.Extrude(Poly.Translate(letters, -centreX, -box.MinY), 0, Poly.U(s[Keys.LetterHeight]));
        // Stands the flat letters up onto the face. The letters keep their left and right, so Hebrew still reads correctly from the front.
        var onFace = flat.Mapped(v => (
            v.X,
            (long)Math.Round(v.Y * sin - v.Z * cos),
            (long)Math.Round(v.Y * cos + v.Z * sin)));

        // Array layout works from the area the plate covers on the print bed.
        var footprint = new Paths64 { Poly.Rect(-width / 2, 0, width / 2, depth) };
        return new ItemModel(body, onFace, footprint, letters, null, 1);
    }
}

/// <summary>
/// A saddle that sits on top of a cup's rim. Two curved walls follow the rim on either side,
/// so it grips a long stretch of rim and cannot rock. It prints name side down, with the letters
/// flush in the face and the walls growing straight up, so it needs no supports.
/// </summary>
public sealed class CupMarkerItem : IItemType
{
    // The walls would otherwise pinch the rim, because printed gaps come out slightly small.
    const double Clearance = 0.2;
    const double PlateBeyondWalls = 1, SkinAroundLetters = 0.4, ThinnestPlateAboveLetters = 0.4, ArcStep = 1.5;

    public string Id => "cupmarker";
    public string Name => "Cup marker";
    public int Order => 5;
    public override string ToString() => Name;

    public IReadOnlyList<Field> Fields { get; } = new[]
    {
        new Field(Keys.CupDiameter, "Cup diameter (mm)", Min: 30, Step: 1),
        new Field(Keys.RimThickness, "Rim thickness (mm)", Min: 0.5, Step: 0.1),
        new Field(Keys.GripLength, "Grip length (mm)", Min: 10, Step: 1),
        new Field(Keys.GripDepthInner, "Inner grip depth (mm)", Min: 2, Step: 1),
        new Field(Keys.GripDepthOuter, "Outer grip depth (mm)", Min: 2, Step: 1),
        new Field(Keys.SlotWall, "Grip wall (mm)", Min: 1, Step: 0.5),
    };

    public IReadOnlyDictionary<string, double> Defaults { get; } = new Dictionary<string, double>
    {
        [Keys.FontSize] = 12, [Keys.BaseHeight] = 3, [Keys.LetterHeight] = 0.8, [Keys.Outline] = 2, [Keys.Weight] = 0, [Keys.ConnectorOutline] = 0, [Keys.FillGaps] = 4,
        [Keys.CupDiameter] = 80, [Keys.RimThickness] = 3, [Keys.GripLength] = 30, [Keys.GripDepthInner] = 8, [Keys.GripDepthOuter] = 8, [Keys.SlotWall] = 2.5,
    };

    /// <summary>The two walls as seen from the name side, inner wall first, with the cup's centre straight below the name.</summary>
    public sealed record GripShape(Paths64 Walls, Path64 Footprint, double CentreY, double Radius, double HalfAngle);

    public static GripShape Grip(ItemSettings s)
    {
        var rim = s[Keys.RimThickness];
        var wall = s[Keys.SlotWall];
        var halfGap = (rim + Clearance) / 2;
        // Cup diameter is measured across the outside, and the groove follows the middle of the rim.
        var radius = Math.Max((s[Keys.CupDiameter] - rim) / 2, halfGap + wall + 2);
        var halfAngle = Math.Min(s[Keys.GripLength] / (2 * radius), Math.PI * 0.45);
        // The curve dips at both ends, so lifting it by half of that keeps it centred on the name.
        var centreY = radius * (1 - Math.Cos(halfAngle)) / 2 - radius;

        Path64 Sector(double from, double to) => ArcBand(centreY, from, to, halfAngle);
        return new GripShape(
            new Paths64 { Sector(radius - halfGap - wall, radius - halfGap), Sector(radius + halfGap, radius + halfGap + wall) },
            Sector(radius - halfGap - wall, radius + halfGap + wall),
            centreY, radius, halfAngle);
    }

    static Path64 ArcBand(double centreY, double innerRadius, double outerRadius, double halfAngle)
    {
        var steps = Math.Max(4, (int)Math.Ceiling(2 * halfAngle * outerRadius / ArcStep));
        var path = new Path64(2 * steps + 2);
        void Add(double radius, int step)
        {
            var angle = Math.PI / 2 - halfAngle + 2 * halfAngle * step / steps;
            path.Add(new Point64(Poly.U(radius * Math.Cos(angle)), Poly.U(centreY + radius * Math.Sin(angle))));
        }
        for (var i = 0; i <= steps; i++) Add(outerRadius, i);
        for (var i = steps; i >= 0; i--) Add(innerRadius, i);
        return path;
    }

    public ItemModel Build(TextShape text, ItemSettings s)
    {
        var (letters, outline) = TagAreas.Build(TagAreas.Position(text, TagAreas.Center), s);
        if (outline.Count == 0) return TagAreas.Solid(outline, letters, s);

        var grip = Grip(s);
        // However short the name is, the plate always covers the whole grip.
        var area = Poly.Clean(Poly.FillHoles(Poly.Union(outline, Poly.Inflate(new Paths64 { grip.Footprint }, PlateBeyondWalls))));
        // Keeps a skin of base colour around the letters, which also keeps every mesh edge shared by exactly two faces.
        letters = Poly.Clean(Poly.Intersect(letters, Poly.Inflate(area, -SkinAroundLetters)));

        // From here on everything is in print position: name side down, so read from above it is mirrored.
        area = Poly.Mirror(area);
        letters = Poly.Mirror(letters);
        var walls = Poly.Mirror(grip.Walls);
        var pockets = Poly.Reversed(letters);

        var plate = s[Keys.BaseHeight];
        var plateZ = Poly.U(plate);
        var letterZ = Poly.U(Math.Min(s[Keys.LetterHeight], Math.Max(plate - ThinnestPlateAboveLetters, plate / 2)));

        var body = new Mesh();
        body.Cap(new Paths64(area.Concat(pockets)), 0, facingUp: false);
        body.Walls(area, 0, plateZ);
        body.Walls(pockets, 0, letterZ);
        body.Cap(letters, letterZ, facingUp: false);
        body.Cap(new Paths64(area.Concat(Poly.Reversed(walls))), plateZ, facingUp: true);
        // Inside and outside the cup can need different depths, so each wall has its own.
        foreach (var (wall, depth) in new[] { (walls[0], s[Keys.GripDepthInner]), (walls[1], s[Keys.GripDepthOuter]) })
        {
            var one = new Paths64 { wall };
            var topZ = plateZ + Poly.U(depth);
            body.Walls(one, plateZ, topZ);
            body.Cap(one, topZ, facingUp: true);
        }

        var flush = new Mesh();
        flush.Extrude(letters, 0, letterZ);
        return new ItemModel(body, flush, area, letters, null, Poly.CountIslands(area), PreviewFlipped: true);
    }
}

/// <summary>A flat C-shaped clip that snaps around a wine glass stem, with the name as a flag beside it.</summary>
public sealed class WineMarkerItem : IItemType
{
    const double RingOverlap = 1.5;

    public string Id => "winemarker";
    public string Name => "Wine glass marker";
    public int Order => 6;
    public override string ToString() => Name;

    public IReadOnlyList<Field> Fields { get; } = new[]
    {
        new Field(Keys.StemDiameter, "Stem size (mm)", Min: 2, Step: 0.5),
        new Field(Keys.StemOpening, "Clip opening (mm)", Min: 1, Step: 0.5),
        new Field(Keys.StemWall, "Clip wall (mm)", Min: 1, Step: 0.5),
        new Field(Keys.StemSide, "Clip position", FieldKind.Choice, Choices: new[] { "Right (start)", "Left (end)" }),
    };

    public IReadOnlyDictionary<string, double> Defaults { get; } = new Dictionary<string, double>
    {
        [Keys.FontSize] = 12, [Keys.BaseHeight] = 3, [Keys.LetterHeight] = 0.8, [Keys.Outline] = 2, [Keys.Weight] = 0, [Keys.ConnectorOutline] = 0, [Keys.FillGaps] = 4,
        [Keys.StemDiameter] = 7, [Keys.StemOpening] = 5.5, [Keys.StemWall] = 2.5, [Keys.StemSide] = 0,
    };

    public ItemModel Build(TextShape text, ItemSettings s)
    {
        var onRight = s[Keys.StemSide] == 0;
        var placed = TagAreas.Position(text, b => onRight ? -b.MaxX : -b.MinX);
        var (letters, outline) = TagAreas.Build(placed, s);
        if (outline.Count == 0) return TagAreas.Solid(outline, letters, s);

        var stem = s[Keys.StemDiameter];
        var wall = s[Keys.StemWall];
        var radius = stem / 2;
        var outer = radius + wall;
        var outward = onRight ? 1.0 : -1.0;
        var centreX = outward * (s[Keys.Outline] + s[Keys.Weight] + outer - RingOverlap);

        var ring = Poly.Circle(centreX, 0, outer * 2);
        var (near, far) = onRight ? (-outer, s[Keys.Outline] + s[Keys.Weight] + 1) : (-(s[Keys.Outline] + s[Keys.Weight] + 1), outer);
        var bridge = TagAreas.Bridge(outline, ring, near, far);
        var area = Poly.Union(Poly.Union(outline, new Paths64 { ring }), bridge);

        // The opening is narrower than the stem, so the clip has to flex to go on and then holds.
        var halfOpen = Math.Min(s[Keys.StemOpening], stem * 0.95) / 2;
        var lip = Math.Sqrt(radius * radius - halfOpen * halfOpen);
        var straightEnd = lip + wall * 0.3;
        var flareEnd = outer + 1;
        var flare = halfOpen + wall * 0.6;
        Point64 At(double along, double y) => new(Poly.U(centreX + outward * along), Poly.U(y));
        var opening = new Paths64
        {
            new Path64 { At(0, -halfOpen), At(straightEnd, -halfOpen), At(flareEnd, -flare), At(flareEnd, flare), At(straightEnd, halfOpen), At(0, halfOpen) },
            Poly.Circle(centreX, 0, stem),
        };

        area = Poly.Difference(area, opening);
        letters = Poly.Difference(letters, opening);
        return TagAreas.Solid(area, letters, s, new Point64(Poly.U(centreX), 0));
    }
}
