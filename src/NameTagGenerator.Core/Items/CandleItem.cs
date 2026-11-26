using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Text;

namespace NameTagGenerator.Items;

/// <summary>Sizes the candle came out at, in mm. NameLength and BackLength are the ink along the body (0 without back text).</summary>
public sealed record CandleLayout(double Width, double LetterSize, double BodyLength, double NameLength, double BackLength = 0);

/// <summary>
/// Hanukkah candle keychain, printed flat: the name runs along an upright body, first letter by the flame.
/// Base colour: body, flame and ring. Raised colour: the name and a big flame inlay, both the same height.
/// Unlike the other items, a name too long for the longest body makes the letters and the candle smaller together.
/// Optional back text: the same on every candle, a third part sunk into the bottom and mirrored, so it reads
/// correctly when the candle is turned over. Like the name, its first letter is by the flame (each text by its own
/// direction, so Hebrew and English both start at the flame). The longer of name and back text sets the body length.
/// </summary>
public sealed class CandleItem : IItemType
{
    public string Id => "candle";
    public string Name => "Hanukkah candle";
    public int Order => 7;
    public override string ToString() => Name;

    // Closest the raised name may come to the candle's side.
    const double SideRim = 1.2;
    const double RingWall = 3, RingOverlap = 1.5;
    // Loop above the flame: its wall, and how far the hole's lower edge sits above the flame's tip.
    const double LoopWall = 2, LoopLift = 0.5;
    // Ring position values, in the order of the "Ring position" list.
    const int BottomTab = 0, UnderFlame = 1, TopOfFlame = 2, NoHole = 3;
    // Ring under the flame: least gap from the body's top edge to the hole, the least wall beside it,
    // and the clearance between the hole and the raised name.
    const double HoleTopGap = 2, HoleSideWall = 1.5, HoleClearance = 1;
    // How far below the body's top edge the flame's big circle reaches, per mm of width (0.6 - √(0.6² - 0.35²)).
    // The flame inlay ends a rim above that, so a hole this far down stays a full rim from the inlay.
    static readonly double FlameDip = 0.6 - Math.Sqrt(0.36 - 0.1225);

    public IReadOnlyList<Field> Fields { get; } = new[]
    {
        new Field(Keys.CandleRing, "Ring position", FieldKind.Choice, Choices: new[] { "Bottom tab", "Under the flame", "Top of the flame", "No hole" }),
        new Field(Keys.ReadUp, "Read upward", FieldKind.Toggle),
        new Field(Keys.BackText, "Back text", FieldKind.Text),
        new Field(Keys.BackFlip, "Flip back text", FieldKind.Toggle),
        new Field(Keys.BackDepth, "Back depth (mm)", Min: 0.2, Step: 0.1),
        new Field(Keys.CandleWidth, "Candle width (mm)", Min: 5, Step: 0.5),
        new Field(Keys.LetterFill, "Letter size (% of width)", Min: 20, Step: 5),
        new Field(Keys.MinLetters, "Smallest letters (mm)", Min: 2, Step: 0.5),
        new Field(Keys.MinLength, "Shortest body (mm)", Min: 10, Step: 1),
        new Field(Keys.MaxLength, "Longest body (mm)", Min: 20, Step: 1),
        new Field(Keys.EndMargin, "End margin (mm)", Step: 0.5),
        new Field(Keys.FlameRim, "Flame rim (mm)", Min: 0.8, Step: 0.1),
        new Field(Keys.RingHole, "Ring hole (mm)", Min: 1, Step: 0.5),
    };

    public IReadOnlyDictionary<string, double> Defaults { get; } = new Dictionary<string, double>
    {
        [Keys.FontSize] = 10, [Keys.BaseHeight] = 2, [Keys.LetterHeight] = 1, [Keys.Outline] = 0, [Keys.Weight] = 0, [Keys.ConnectorOutline] = 0, [Keys.FillGaps] = 0,
        [Keys.CandleRing] = 0, [Keys.ReadUp] = 0, [Keys.CandleWidth] = 16, [Keys.LetterFill] = 70, [Keys.MinLetters] = 7,
        [Keys.MinLength] = 45, [Keys.MaxLength] = 85, [Keys.EndMargin] = 6, [Keys.FlameRim] = 1.5, [Keys.RingHole] = 4,
        [Keys.BackDepth] = 0.6, [Keys.BackFlip] = 0,
    };

    static readonly IReadOnlyDictionary<string, TextShape> NoTexts = new Dictionary<string, TextShape>();

    public IReadOnlyCollection<string> HiddenCommonFields { get; } = new[] { Keys.FontSize, Keys.Outline, Keys.FillGaps };

    // The chosen ring position. Anything not in the list (an old or hand-edited value) means the bottom tab, as before.
    static int Ring(ItemSettings s)
    {
        var v = s[Keys.CandleRing];
        return v is UnderFlame or TopOfFlame or NoHole ? (int)v : BottomTab;
    }

    /// <param name="back">The back text drawn in the name's font, or null for none.</param>
    public static CandleLayout Measure(TextShape text, ItemSettings s, TextShape? back = null) => Plan(text, s, back).Layout;

    public ItemModel Build(TextShape text, ItemSettings s) => Build(text, s, NoTexts);

    public ItemModel Build(TextShape text, ItemSettings s, IReadOnlyDictionary<string, TextShape> texts)
    {
        var (layout, name, backText, hole, holeY) = Plan(text, s, texts.GetValueOrDefault(Keys.BackText));
        double w = layout.Width, body = layout.BodyLength;
        var ring = Ring(s);

        // Body with rounded corners, its top edge at y = 0.
        var corner = Math.Min(2, w / 4);
        var bodyArea = Poly.Inflate(Poly.Inflate(new Paths64 { Poly.Rect(-w / 2, -body, w / 2, 0) }, -corner), corner);

        // Flame: hull of a big circle and a small round tip. The big circle crosses the body's top edge 0.7 × width wide.
        var r = 0.6 * w;
        var centreY = Math.Sqrt(r * r - 0.35 * w * (0.35 * w));
        var tip = 0.18 * w;
        var flame = new Paths64 { Poly.ConvexHull(Poly.Circle(0, centreY, 2 * r), Poly.Circle(0, 1.3 * w - tip, 2 * tip)) };
        var inlay = Poly.Inflate(flame, -s[Keys.FlameRim]);

        var (letters, baseText) = TagAreas.Build(name, s);
        var baseArea = Poly.Union(Poly.Union(bodyArea, flame), baseText);
        var raised = Poly.Union(letters, inlay);
        var back = backText is null ? null : Poly.Inflate(backText.Letters, Math.Max(0, s[Keys.Weight]));
        var depth = s[Keys.BackDepth];
        if (ring == NoHole) return TagAreas.Solid(baseArea, raised, s, null, back, depth);

        if (ring == BottomTab) baseArea = Poly.Union(baseArea, new Paths64 { Poly.Circle(0, holeY, hole + 2 * RingWall) });
        if (ring == TopOfFlame) baseArea = Poly.Union(baseArea, new Paths64 { Poly.Circle(0, holeY, hole + 2 * LoopWall) });
        baseArea = Poly.Difference(baseArea, new Paths64 { Poly.Circle(0, holeY, hole) });
        var clear = new Paths64 { Poly.Circle(0, holeY, hole + 2 * HoleClearance) };
        raised = Poly.Difference(raised, clear);
        if (back is not null) back = Poly.Difference(back, clear);
        return TagAreas.Solid(baseArea, raised, s, new Point64(0, Poly.U(holeY)), back, depth);
    }

    /// <summary>A text's size per mm of letter size, so it does not change as the text is scaled.</summary>
    /// <param name="Low">Lowest tail across the candle, from the middle of the plain letters (High: highest).</param>
    sealed record Ink(Box Box, double Mid, double Band, double Low, double High, double Length, bool Empty)
    {
        public double Span => High - Low;
    }

    static Ink Inspect(TextShape text)
    {
        var ink = Poly.Union(text.Letters, text.Connector);
        var box = ink.Count == 0 ? new Box(0, 0, 0, 0) : Poly.Bounds(ink);
        // "Letter size" is the height of a plain letter in this font (ה or H), so it means the same in every font.
        var (bandLow, bandHigh) = text.LetterBand ?? (box.MinY, box.MaxY);
        var band = Poly.Mm(Math.Max(1, bandHigh - bandLow));
        var mid = (bandLow + bandHigh) / 2.0;
        // Across the candle the plain letters are centred; tails (ל, ק, g) reach Low / High.
        if (ink.Count == 0) return new Ink(box, mid, band, 0, 0, 0, true);
        return new Ink(box, mid, band,
            Math.Min(-0.5, (box.MinY - mid) / Poly.Scale / band),
            Math.Max(0.5, (box.MaxY - mid) / Poly.Scale / band),
            Poly.Mm(box.Width) / band, false);
    }

    /// <summary>Works out the candle's size and places the name (and back text) on it, turned to run along the body.</summary>
    static (CandleLayout Layout, TextShape Name, TextShape? Back, double Hole, double HoleY) Plan(TextShape text, ItemSettings s, TextShape? backText)
    {
        double margin = Math.Max(SideRim, s[Keys.EndMargin]), weight = Math.Max(0, s[Keys.Weight]);
        var sideRims = 2 * (SideRim + weight);
        // Never narrower than the side rims plus a little, so there is always room for letters.
        var w0 = Math.Max(s[Keys.CandleWidth], sideRims + 2);
        var fill = Math.Max(0.05, s[Keys.LetterFill] / 100);
        var ring = Ring(s);
        var ringOnTop = ring == UnderFlame;
        double HoleGap(double width) => Math.Max(HoleTopGap, FlameDip * width);
        // Room the hole takes at the top of the body. Worked out at the full width, which needs the most.
        var reserve = ringOnTop ? HoleGap(w0) + s[Keys.RingHole] + HoleClearance : 0;

        var nameInk = Inspect(text);
        var backInk = backText is null ? null : Inspect(backText);
        if (backInk is { Empty: true }) (backText, backInk) = (null, null);

        // Both texts share one letter size: the longer one sets the length, the one with bigger tails the width.
        var lengthPerMm = Math.Max(nameInk.Length, backInk?.Length ?? 0);
        var spanPerMm = Math.Max(nameInk.Span, backInk?.Span ?? 0);
        // Letters fill their share of the width, unless a tail would then come closer than SideRim to the side.
        double LetterFor(double width) => Math.Max(0.1, Math.Min(width * fill, (width - sideRims) / spanPerMm));

        var width = w0;
        var letter = LetterFor(width);
        // Too long for the longest body: shrink letters and width together, but not below the smallest letters.
        // The letters grow by Text weight at both ends too.
        var room = s[Keys.MaxLength] - 2 * margin - reserve - 2 * weight;
        if (lengthPerMm > 0 && letter * lengthPerMm > room)
        {
            var target = Math.Max(room / lengthPerMm, s[Keys.MinLetters]);
            if (target < letter)
            {
                width = Math.Max(target / fill, target * spanPerMm + sideRims);
                letter = LetterFor(width);
            }
        }

        var half = Math.Max(0, width / 2 - SideRim - weight);
        double Length(Ink ink) => ink.Empty ? 0 : ink.Length * letter + 2 * weight;
        var nameLength = Length(nameInk);
        var backLength = backInk is null ? 0 : Length(backInk);
        var bodyLength = Math.Max(s[Keys.MinLength], Math.Max(nameLength, backLength) + 2 * margin + reserve);
        var centre = Poly.U(-(reserve + (bodyLength - reserve) / 2));
        var readUp = s[Keys.ReadUp] != 0;

        // Scale, centre (along: on the ink; across: on the letter band), then turn so the first letter is at the top.
        // Mirrored: as seen from below. Flipped: half a turn on the bed, so the first letter goes to the bottom.
        TextShape Place(TextShape t, Ink ink, bool mirrored, bool flipped)
        {
            // Move the text across only as far as its tails need.
            double lo = ink.Low * letter, hi = ink.High * letter, offset = 0;
            if (hi > half) offset = half - hi;
            else if (lo < -half) offset = -half - lo;
            var k = letter / ink.Band;
            var firstOnRight = t.RightToLeft != readUp;
            var dx = -(ink.Box.MinX + ink.Box.MaxX) / 2.0;
            Paths64 Move(Paths64 area)
            {
                if (area.Count == 0) return area;
                var placed = new Paths64(area.Select(path => new Path64(path.Select(p =>
                {
                    var x = (p.X + dx) * k;
                    var y = (p.Y - ink.Mid) * k + Poly.U(offset);
                    // First letter on the right: turn a quarter anticlockwise, so the right end goes up. Otherwise clockwise.
                    var px = firstOnRight ? (long)Math.Round(-y) : (long)Math.Round(y);
                    var py = (firstOnRight ? (long)Math.Round(x) : (long)Math.Round(-x)) + centre;
                    if (mirrored) px = -px;
                    if (flipped) (px, py) = (-px, 2 * centre - py);
                    return new Point64(px, py);
                }))));
                // A mirror image runs the other way round; turn the outlines back so outer ones stay anticlockwise.
                return mirrored ? Poly.Reversed(placed) : placed;
            }
            return t with { Letters = Move(t.Letters), Connector = Move(t.Connector) };
        }

        var name = Place(text, nameInk, false, false);
        var back = backText is null ? null : Place(backText, backInk!, true, s[Keys.BackFlip] != 0);
        // Ring under the flame: the hole keeps a wall on each side. Bottom tab: the tab grows with the hole.
        // Loop on top: the hole starts just above the flame's tip, and the loop's wall reaches down into the tip.
        var hole = ringOnTop ? Math.Min(s[Keys.RingHole], width - 2 * HoleSideWall) : s[Keys.RingHole];
        var holeY = ring switch
        {
            UnderFlame => -(HoleGap(width) + hole / 2),
            TopOfFlame => 1.3 * width + LoopLift + hole / 2,
            _ => -bodyLength - (hole / 2 + RingWall - RingOverlap),
        };
        return (new CandleLayout(width, letter, bodyLength, nameLength, backLength), name, back, hole, holeY);
    }
}
