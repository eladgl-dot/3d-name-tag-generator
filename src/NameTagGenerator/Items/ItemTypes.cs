using System.Reflection;
using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Text;

namespace NameTagGenerator.Items;

public static class Keys
{
    public const string FontSize = "fontSize", BaseHeight = "baseHeight", LetterHeight = "letterHeight";
    public const string Outline = "outline", Weight = "weight", ConnectorOutline = "connectorOutline";
    public const string AddRing = "addRing", RingHole = "ringHole", RingWall = "ringWall", RingSide = "ringSide", RingAdjust = "ringAdjust";
    public const string PencilHole = "pencilHole", PencilOutline = "pencilOutline";
    public const string ClipWidth = "clipWidth", ClipLength = "clipLength", ClipBorder = "clipBorder", ClipGap = "clipGap";
    public const string FillGaps = "fillGaps";
    public const string Tilt = "tilt", StandDepth = "standDepth";
    public const string RimThickness = "rimThickness", CupDiameter = "cupDiameter", GripLength = "gripLength", GripDepthInner = "gripDepthInner", GripDepthOuter = "gripDepthOuter", SlotWall = "slotWall";
    public const string StemDiameter = "stemDiameter", StemOpening = "stemOpening", StemWall = "stemWall", StemSide = "stemSide";
}

public enum FieldKind { Number, Toggle, Choice }

/// <summary>One setting shown on screen. The panel is generated from these.</summary>
public sealed record Field(string Key, string Label, FieldKind Kind = FieldKind.Number, double Min = 0, double Step = 0.5, string[]? Choices = null);

public sealed class ItemSettings
{
    public string Font { get; set; } = "Gan CLM";
    public bool Bold { get; set; }
    public string Connector { get; set; } = "-";
    public bool Hollow { get; set; }
    public Dictionary<string, double> Values { get; set; } = new();

    public double this[string key] => Values.GetValueOrDefault(key);

    public static ItemSettings DefaultsFor(IItemType item) => new() { Values = new Dictionary<string, double>(item.Defaults) };

    /// <summary>Moves values saved by older versions to the settings that replaced them.</summary>
    public void Upgrade()
    {
        // v0.1.2 and earlier: one Grip depth for both cup marker walls.
        if (Values.Remove("gripDepth", out var depth))
        {
            Values.TryAdd(Keys.GripDepthInner, depth);
            Values.TryAdd(Keys.GripDepthOuter, depth);
        }
    }
}

/// <summary>
/// A finished item: two closed bodies in the position they print, plus the outlines used to check it.
/// PreviewFlipped means it prints name side down, so the preview turns it over to show the name.
/// </summary>
public sealed record ItemModel(Mesh Base, Mesh Letters, Paths64 BaseArea, Paths64 LetterArea, Point64? RingHoleCenter, int LooseParts, bool PreviewFlipped = false);

/// <summary>
/// One printable item. To add a new item, add one class implementing this:
/// it is found automatically and appears in the Item list with its own settings.
/// </summary>
public interface IItemType
{
    string Id { get; }
    string Name { get; }
    int Order { get; }
    IReadOnlyList<Field> Fields { get; }
    IReadOnlyDictionary<string, double> Defaults { get; }
    ItemModel Build(TextShape text, ItemSettings settings);
}

public static class ItemRegistry
{
    public static IReadOnlyList<IItemType> All { get; } = Assembly.GetExecutingAssembly().GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IItemType).IsAssignableFrom(t))
        .Select(t => (IItemType)Activator.CreateInstance(t)!)
        .OrderBy(i => i.Order)
        .ToList();

    /// <summary>Settings shared by every item.</summary>
    public static IReadOnlyList<Field> CommonFields { get; } = new[]
    {
        new Field(Keys.FontSize, "Font size", Min: 1, Step: 0.5),
        new Field(Keys.Weight, "Text weight (mm)", Step: 0.1),
        new Field(Keys.BaseHeight, "Base height (mm)", Min: 0.2, Step: 0.1),
        new Field(Keys.LetterHeight, "Letter height (mm)", Min: 0.1, Step: 0.1),
        new Field(Keys.Outline, "Outline (mm)", Step: 0.5),
        new Field(Keys.FillGaps, "Fill gaps up to (mm)", Step: 0.5),
    };
}

/// <summary>Outlines shared by items that place a name on a base.</summary>
public static class TagAreas
{
    public static (Paths64 Letters, Paths64 BaseText) Build(TextShape text, ItemSettings s)
    {
        var weight = s[Keys.Weight];
        var outline = s[Keys.Outline] + weight;
        var letters = Poly.Inflate(text.Letters, weight);
        var baseText = Poly.Union(
            Poly.Inflate(text.Letters, outline),
            Poly.Inflate(text.Connector, outline + s[Keys.ConnectorOutline]));
        baseText = Poly.Close(baseText, s[Keys.FillGaps]);
        return (letters, Poly.FillHoles(baseText));
    }

    /// <summary>
    /// Solid join from an anchor shape to the part of the name's outline above it, between two X positions.
    /// However short the letters are there, this leaves no slit and no thin neck.
    /// </summary>
    public static Paths64 Bridge(Paths64 outline, Path64 anchor, double fromX, double toX)
    {
        var box = Poly.Bounds(outline);
        var midY = Poly.Mm((box.MinY + box.MaxY) / 2);
        var nearby = Poly.Intersect(outline, new Paths64 { Poly.Rect(fromX, Poly.Mm(box.MinY), toX, midY) });
        return new Paths64 { Poly.ConvexHull(nearby.Append(anchor).ToArray()) };
    }

    /// <summary>Moves the name so its ink is centred vertically and placed horizontally by the anchor.</summary>
    public static TextShape Position(TextShape text, Func<Box, long> dxFor)
    {
        var box = Poly.Bounds(Poly.Union(text.Letters, text.Connector));
        var dx = dxFor(box);
        var dy = -(box.MinY + box.MaxY) / 2;
        return new TextShape(Poly.Translate(text.Letters, dx, dy), Poly.Translate(text.Connector, dx, dy));
    }

    public static long Center(Box b) => -(b.MinX + b.MaxX) / 2;

    public static ItemModel Solid(Paths64 baseArea, Paths64 letterArea, ItemSettings s, Point64? ringHole = null)
    {
        baseArea = Poly.Clean(baseArea);
        letterArea = Poly.Clean(letterArea);
        var baseZ = Poly.U(s[Keys.BaseHeight]);
        var baseMesh = new Mesh();
        baseMesh.Extrude(baseArea, 0, baseZ);
        var letters = new Mesh();
        letters.Extrude(letterArea, baseZ, baseZ + Poly.U(s[Keys.LetterHeight]));
        return new ItemModel(baseMesh, letters, baseArea, letterArea, ringHole, Poly.CountIslands(baseArea));
    }
}

public sealed class KeychainItem : IItemType
{
    public string Id => "keychain";
    public string Name => "Keychain";
    public int Order => 1;
    public override string ToString() => Name;

    public IReadOnlyList<Field> Fields { get; } = new[]
    {
        new Field(Keys.AddRing, "Keyring", FieldKind.Toggle),
        new Field(Keys.RingSide, "Ring position", FieldKind.Choice, Choices: new[] { "Right (start)", "Left (end)" }),
        new Field(Keys.RingHole, "Hole size (mm)", Min: 0.5, Step: 0.5),
        new Field(Keys.RingWall, "Ring wall (mm)", Min: 0.4, Step: 0.5),
        new Field(Keys.RingAdjust, "Distance adjust (mm)", Min: -100, Step: 0.5),
    };

    public IReadOnlyDictionary<string, double> Defaults { get; } = new Dictionary<string, double>
    {
        [Keys.FontSize] = 18, [Keys.BaseHeight] = 3, [Keys.LetterHeight] = 4, [Keys.Outline] = 3, [Keys.Weight] = 0, [Keys.ConnectorOutline] = 0, [Keys.FillGaps] = 4,
        [Keys.AddRing] = 1, [Keys.RingSide] = 0, [Keys.RingHole] = 4, [Keys.RingWall] = 3, [Keys.RingAdjust] = -2,
    };

    public ItemModel Build(TextShape text, ItemSettings s)
    {
        var onRight = s[Keys.RingSide] == 0;
        var placed = TagAreas.Position(text, b => onRight ? -b.MaxX : -b.MinX);
        var (letters, baseArea) = TagAreas.Build(placed, s);
        if (s[Keys.AddRing] == 0) return TagAreas.Solid(baseArea, letters, s);

        // The ring slightly overlaps the outline, so the two print as one piece.
        var holeD = s[Keys.RingHole];
        var ringOuterR = holeD / 2 + s[Keys.RingWall];
        const double overlap = 1.5;
        var offset = s[Keys.Outline] + s[Keys.Weight] + ringOuterR - overlap + s[Keys.RingAdjust];
        var ringX = onRight ? offset : -offset;

        var hole = new Paths64 { Poly.Circle(ringX, 0, holeD) };
        baseArea = Poly.Union(baseArea, new Paths64 { Poly.Circle(ringX, 0, ringOuterR * 2) });
        baseArea = Poly.Difference(baseArea, hole);
        letters = Poly.Difference(letters, hole);
        return TagAreas.Solid(baseArea, letters, s, new Point64(Poly.U(ringX), 0));
    }
}

public sealed class BookmarkItem : IItemType
{
    public string Id => "bookmark";
    public string Name => "Bookmark";
    public int Order => 3;
    public override string ToString() => Name;

    public IReadOnlyList<Field> Fields { get; } = new[]
    {
        new Field(Keys.ClipWidth, "Clip width (mm)", Min: 5, Step: 1),
        new Field(Keys.ClipLength, "Clip length (mm)", Min: 10, Step: 1),
        new Field(Keys.ClipBorder, "Border (mm)", Min: 0.5, Step: 0.5),
        new Field(Keys.ClipGap, "U-cut gap (mm)", Min: 0.2, Step: 0.1),
    };

    public IReadOnlyDictionary<string, double> Defaults { get; } = new Dictionary<string, double>
    {
        [Keys.FontSize] = 14, [Keys.BaseHeight] = 1.3, [Keys.LetterHeight] = 0.3, [Keys.Outline] = 4, [Keys.Weight] = 0, [Keys.ConnectorOutline] = 0, [Keys.FillGaps] = 4,
        [Keys.ClipWidth] = 35, [Keys.ClipLength] = 90, [Keys.ClipBorder] = 3, [Keys.ClipGap] = 1,
    };

    public ItemModel Build(TextShape text, ItemSettings s)
    {
        var placed = TagAreas.Position(text, TagAreas.Center);
        var (letters, baseText) = TagAreas.Build(placed, s);

        double w = s[Keys.ClipWidth], length = s[Keys.ClipLength], border = s[Keys.ClipBorder], gap = s[Keys.ClipGap];
        var tipY = -length + w / 2;

        static Path64 Rounded(double width, double top, double tipY) => Poly.ConvexHull(
            Poly.Rect(-width / 2, top - 0.1, width / 2, top),
            Poly.Circle(0, tipY, width));

        var body = new Paths64 { Rounded(w, 0, tipY) };
        // A solid band as deep as the border, so the slits look the same under every name.
        var outerCut = new Paths64 { Rounded(w - 2 * border, -border, tipY) };
        var tongue = new Paths64 { Rounded(w - 2 * border - 2 * gap, -border, tipY) };
        var clip = Poly.Difference(body, Poly.Difference(outerCut, tongue));
        // Rounds the sharp corners, as the old app did.
        clip = Poly.Inflate(Poly.Inflate(clip, -0.5), 0.5);

        // The clip hangs from the lowest point of the name's outline.
        var nameBottom = Poly.Mm(Poly.Bounds(baseText).MinY);
        clip = Poly.Translate(clip, 0, Poly.U(nameBottom));
        var bridge = TagAreas.Bridge(baseText, Poly.Rect(-w / 2, nameBottom - border, w / 2, nameBottom), -w / 2, w / 2);

        return TagAreas.Solid(Poly.Union(Poly.Union(baseText, clip), bridge), letters, s);
    }
}

public sealed class PencilTopItem : IItemType
{
    public string Id => "pencil";
    public string Name => "Pencil top";
    public int Order => 2;
    public override string ToString() => Name;

    public IReadOnlyList<Field> Fields { get; } = new[]
    {
        new Field(Keys.PencilHole, "Hole size (mm)", Min: 0.5, Step: 0.1),
        new Field(Keys.PencilOutline, "Hole outline (mm)", Min: 0.5, Step: 0.1),
    };

    public IReadOnlyDictionary<string, double> Defaults { get; } = new Dictionary<string, double>
    {
        [Keys.FontSize] = 12.5, [Keys.BaseHeight] = 11, [Keys.LetterHeight] = 1.8, [Keys.Outline] = 1.6, [Keys.Weight] = 0, [Keys.ConnectorOutline] = 0, [Keys.FillGaps] = 4,
        [Keys.PencilHole] = 7.8, [Keys.PencilOutline] = 10,
    };

    public ItemModel Build(TextShape text, ItemSettings s)
    {
        var placed = TagAreas.Position(text, TagAreas.Center);
        var (letters, baseText) = TagAreas.Build(placed, s);
        var height = s[Keys.BaseHeight];
        var baseMesh = PencilBody.Build(baseText, height, s[Keys.PencilHole], s[Keys.PencilOutline]);
        var letterMesh = new Mesh();
        letterMesh.Extrude(letters, Poly.U(height), Poly.U(height + s[Keys.LetterHeight]));
        return new ItemModel(baseMesh, letterMesh, baseText, letters, null, 1);
    }
}
