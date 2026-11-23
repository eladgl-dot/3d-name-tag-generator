using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Text;

namespace NameTagGenerator.Tests;

/// <summary>The Hanukkah candle keychain: sizing, flame, ring, reading direction and the two print parts.</summary>
public class CandleTests
{
    static readonly CandleItem Item = new();

    static ItemSettings Defaults(Action<ItemSettings>? change = null)
    {
        var s = ItemSettings.DefaultsFor(Item);
        change?.Invoke(s);
        return s;
    }

    static TextShape Text(string name, ItemSettings s, string font = "Gan CLM") =>
        TextOutliner.Build(name, BundledFonts.Get(font), false, s[Keys.FontSize], "-", false);

    static (ItemModel Model, CandleLayout Layout) Build(string name, Action<ItemSettings>? change = null, string font = "Gan CLM")
    {
        var s = Defaults(change);
        var text = Text(name, s, font);
        return (Item.Build(text, s), CandleItem.Measure(text, s));
    }

    static double AreaMm2(Paths64 area) => Clipper.Area(area) / (Poly.Scale * Poly.Scale);

    [Fact]
    public void Registry_ListsTheCandle()
    {
        Assert.Contains(ItemRegistry.All, i => i.Id == "candle" && i.Name == "Hanukkah candle");
    }

    [Theory]
    [InlineData("נוי")]
    [InlineData("נויה")]
    [InlineData("Ben")]
    public void ShortNames_GetTheShortestBody_AtFullSize(string name)
    {
        var (_, l) = Build(name);
        Assert.Equal(45, l.BodyLength, 2);
        Assert.Equal(16, l.Width, 2);
        Assert.Equal(11.2, l.LetterSize, 2);
    }

    [Fact]
    public void MediumName_BodyIsNamePlusMargins()
    {
        var (_, l) = Build("רונית");
        Assert.Equal(11.2, l.LetterSize, 2);
        Assert.InRange(l.BodyLength, 45.01, 85);
        Assert.Equal(l.NameLength + 12, l.BodyLength, 2);
    }

    [Theory]
    [InlineData("Ben Ben Ben")]
    [InlineData("אלכסנדרה")] // Gan CLM is wide, so this already reaches the longest body
    public void LongName_ShrinksLettersAndWidthTogether_ToTheLongestBody(string name)
    {
        var (_, l) = Build(name);
        Assert.Equal(85, l.BodyLength, 2);
        Assert.InRange(l.LetterSize, 7, 11.19);
        Assert.InRange(l.Width, 10, 15.99);
        Assert.True(l.LetterSize / l.Width <= 0.7001);
    }

    [Fact]
    public void LongName_WithoutTails_KeepsTheProportions()
    {
        var (_, l) = Build("Ben Ben Ben");
        Assert.Equal(0.7, l.LetterSize / l.Width, 3);
    }

    [Fact]
    public void TailedName_ShrinksOnlyEnoughForTheTail()
    {
        var (m, l) = Build("שלמה");
        Assert.InRange(l.LetterSize, 9.5, 11.19);
        Assert.Equal(16, l.Width, 2);
        // The ל reaches to within about the side rim of the edge, no further.
        var raisedName = Poly.Bounds(Poly.Intersect(m.LetterArea, new Paths64 { Poly.Rect(-50, -100, 50, -0.01) }));
        Assert.Equal(8 - 1.2, Math.Max(-Poly.Mm(raisedName.MinX), Poly.Mm(raisedName.MaxX)), 1);
    }

    [Fact]
    public void VeryLongName_StopsShrinkingAtTheSmallestLetters()
    {
        var (_, l) = Build("Ben Ben Ben Ben Ben Ben Ben Ben Ben");
        Assert.Equal(7, l.LetterSize, 2);
        Assert.Equal(10, l.Width, 2);
        Assert.True(l.BodyLength > 85);
    }

    [Fact]
    public void LetterSize_IsTheSameForDifferentNames_WithoutTails()
    {
        Assert.Equal(Build("נוי").Layout.LetterSize, Build("דנה").Layout.LetterSize, 3);
    }

    [Theory]
    [InlineData("נוי", "Gan CLM")]
    [InlineData("שלמה קוקי", "Gan CLM")]
    [InlineData("ליקה", "Gan CLM")]
    [InlineData("ליקה", "Heebo")]
    [InlineData("Ben", "Heebo")]
    [InlineData("Gloria", "Pacifico")]
    [InlineData("אלכסנדרה", "Gveret Levin")]
    public void LettersAndInlay_StayInsideTheBase_WithARim(string name, string font)
    {
        var (m, _) = Build(name, font: font);
        var outside = Poly.Difference(Poly.Inflate(m.LetterArea, 0.99), m.BaseArea);
        Assert.True(AreaMm2(outside) < 0.01, $"{AreaMm2(outside):0.000} mm² of raised part closer than 1 mm to the edge");
    }

    [Theory]
    [InlineData("נוי", 0)]
    [InlineData("נויה", 0)]
    [InlineData("אלכסנדרה", 0)]
    [InlineData("Ben", 0)]
    [InlineData("נוי", 1)]
    [InlineData("Ben", 1)]
    public void BuildsTwoClosedParts_BaseInOnePiece(string name, double ring)
    {
        var (m, _) = Build(name, s => s.Values[Keys.CandleRing] = ring);
        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
        Assert.True(m.Letters.IsManifold(), m.Letters.DescribeOpenEdges());
        Assert.Equal(1, m.LooseParts);
    }

    [Fact]
    public void BaseIs2mm_RaisedPartIs1mmOnTop()
    {
        var (m, _) = Build("נויה");
        Assert.Equal(0, Poly.Mm(m.Base.Vertices.Min(v => v.Z)), 3);
        Assert.Equal(2, Poly.Mm(m.Base.Vertices.Max(v => v.Z)), 3);
        Assert.Equal(2, Poly.Mm(m.Letters.Vertices.Min(v => v.Z)), 3);
        Assert.Equal(3, Poly.Mm(m.Letters.Vertices.Max(v => v.Z)), 3);
    }

    [Fact]
    public void Flame_Is1point2WideAnd1point3Tall_WithABigInlay()
    {
        var (m, l) = Build("נוי");
        var box = Poly.Bounds(m.BaseArea);
        Assert.Equal(1.2 * l.Width, Poly.Mm(box.Width), 1);
        Assert.Equal(1.3 * l.Width, Poly.Mm(box.MaxY), 1); // the body's top edge is at y = 0

        // The inlay is the part of the raised area above the body.
        var inlay = Poly.Intersect(m.LetterArea, new Paths64 { Poly.Rect(-50, 0.01, 50, 50) });
        var flame = Poly.Intersect(m.BaseArea, new Paths64 { Poly.Rect(-50, 0.01, 50, 50) });
        Assert.InRange(AreaMm2(inlay) / AreaMm2(flame), 0.6, 0.95);
    }

    [Fact]
    public void FlameJoint_Is70PercentOfTheWidth()
    {
        var (m, l) = Build("נוי");
        var slice = Poly.Bounds(Poly.Intersect(m.BaseArea, new Paths64 { Poly.Rect(-50, 0.05, 50, 0.15) }));
        Assert.InRange(Poly.Mm(slice.Width), 0.68 * l.Width, 0.74 * l.Width);
    }

    [Fact]
    public void BottomRing_HangsBelowTheBody_WithAClearHole()
    {
        var (m, l) = Build("נוי");
        var hole = m.RingHoleCenter!.Value;
        Assert.True(Poly.Mm(hole.Y) < -l.BodyLength);
        Assert.Equal(PipResult.Outside, Poly.PointInArea(hole, m.BaseArea));
        Assert.Equal(PipResult.Outside, Poly.PointInArea(hole, m.LetterArea));
    }

    [Fact]
    public void RingUnderTheFlame_IsInsideTheBody_ClearOfTheName()
    {
        var (m, l) = Build("אלכסנדרה", s => s.Values[Keys.CandleRing] = 1);
        var hole = m.RingHoleCenter!.Value;
        Assert.InRange(Poly.Mm(hole.Y), -8, -2);
        Assert.Equal(PipResult.Outside, Poly.PointInArea(hole, m.BaseArea));
        Assert.Equal(-l.BodyLength, Poly.Mm(Poly.Bounds(m.BaseArea).MinY), 1); // no tab below
        Assert.InRange(l.BodyLength, 45, 85);

        // The name keeps at least 1.5 mm from the hole's edge.
        var near = Poly.Intersect(m.LetterArea, new Paths64 { Poly.Circle(0, Poly.Mm(hole.Y), 4 + 3) });
        var nameOnly = Poly.Intersect(near, new Paths64 { Poly.Rect(-50, -100, 50, -0.01) });
        Assert.True(AreaMm2(nameOnly) < 0.01);
    }

    [Theory]
    [InlineData("נוי")]
    [InlineData("אלכסנדרה")]
    public void LoopOnTopOfTheFlame_SitsAboveTheTip_InOnePiece(string name)
    {
        var (m, l) = Build(name, s => s.Values[Keys.CandleRing] = 2);
        var hole = m.RingHoleCenter!.Value;
        Assert.Equal(0, Poly.Mm(hole.X), 2);
        // The hole starts above the flame's tip, so the flame inlay stays whole.
        Assert.True(Poly.Mm(hole.Y) - 2 >= 1.3 * l.Width, $"hole bottom {Poly.Mm(hole.Y) - 2:0.00}");
        Assert.Equal(PipResult.Outside, Poly.PointInArea(hole, m.BaseArea));
        Assert.Equal(PipResult.Outside, Poly.PointInArea(hole, m.LetterArea));
        // A 2 mm wall on top of the hole, and no tab below the body.
        Assert.Equal(Poly.Mm(hole.Y) + 2 + 2, Poly.Mm(Poly.Bounds(m.BaseArea).MaxY), 1);
        Assert.Equal(-l.BodyLength, Poly.Mm(Poly.Bounds(m.BaseArea).MinY), 1);
        // Same body as with the bottom tab, and the inlay is as big.
        var (plain, plainLayout) = Build(name);
        Assert.Equal(plainLayout, l);
        var top = new Paths64 { Poly.Rect(-50, 0.01, 50, 80) };
        Assert.Equal(AreaMm2(Poly.Intersect(plain.LetterArea, top)), AreaMm2(Poly.Intersect(m.LetterArea, top)), 1);
        Assert.Equal(1, m.LooseParts);
        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
    }

    [Theory]
    [InlineData("נוי")]
    [InlineData("Ben")]
    [InlineData("Ben", 5, 8)]
    public void NoHole_HasNoHoleAndNoTab(string name, double width = 16, double hole = 4)
    {
        var (m, l) = Build(name, s => { s.Values[Keys.CandleRing] = 3; s.Values[Keys.CandleWidth] = width; s.Values[Keys.RingHole] = hole; });
        Assert.Null(m.RingHoleCenter);
        Assert.All(m.BaseArea, p => Assert.True(Clipper.IsPositive(p), "the base has a hole"));
        var box = Poly.Bounds(m.BaseArea);
        Assert.Equal(-l.BodyLength, Poly.Mm(box.MinY), 1);
        Assert.Equal(1.3 * l.Width, Poly.Mm(box.MaxY), 1);
        Assert.Equal(1, m.LooseParts);
        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
        Assert.True(m.Letters.IsManifold(), m.Letters.DescribeOpenEdges());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    [InlineData(2.7)]
    [InlineData(double.NaN)]
    public void UnknownRingValue_MeansTheBottomTab(double value)
    {
        var (m, l) = Build("נוי", s => s.Values[Keys.CandleRing] = value);
        var (plain, plainLayout) = Build("נוי");
        Assert.Equal(plainLayout, l);
        Assert.Equal(plain.RingHoleCenter, m.RingHoleCenter);
        Assert.Equal(AreaMm2(plain.BaseArea), AreaMm2(m.BaseArea), 3);
    }

    [Fact]
    public void RingChoices_KeepTheOldOrder_WithTheNewOnesAtTheEnd()
    {
        var field = Item.Fields.Single(f => f.Key == Keys.CandleRing);
        Assert.Equal(new[] { "Bottom tab", "Under the flame", "Top of the flame", "No hole" }, field.Choices);
        Assert.Equal(0, Item.Defaults[Keys.CandleRing]);
    }

    // Two squares: a big one on the right and a small one on the left.
    static TextShape BigRightSmallLeft(bool rtl) => new(
        new Paths64 { Poly.Rect(0, 0, 4, 4), Poly.Rect(10, 0, 20, 10) }, new Paths64(), rtl);

    static double BigSquareY(ItemModel m) => Poly.Mm(m.LetterArea
        .Where(Clipper.IsPositive).Where(p => Poly.Mm(Poly.Bounds(new Paths64 { p }).MaxY) < 0)
        .OrderByDescending(p => Math.Abs(Clipper.Area(p))).First()
        .Max(p => p.Y));

    static double SmallSquareY(ItemModel m) => Poly.Mm(m.LetterArea
        .Where(Clipper.IsPositive).Where(p => Poly.Mm(Poly.Bounds(new Paths64 { p }).MaxY) < 0)
        .OrderBy(p => Math.Abs(Clipper.Area(p))).First()
        .Max(p => p.Y));

    [Theory]
    [InlineData(true, false, true)]   // Hebrew: the right-hand (first) letter goes by the flame
    [InlineData(false, false, false)] // English: the left-hand (first) letter goes by the flame
    [InlineData(true, true, false)]   // Read upward flips both
    [InlineData(false, true, true)]
    public void FirstLetter_IsByTheFlame(bool rtl, bool readUp, bool bigOnTop)
    {
        var s = Defaults(x => x.Values[Keys.ReadUp] = readUp ? 1 : 0);
        var m = Item.Build(BigRightSmallLeft(rtl), s);
        Assert.Equal(bigOnTop, BigSquareY(m) > SmallSquareY(m));
    }

    [Fact]
    public void HebrewName_IsMarkedRightToLeft()
    {
        Assert.True(Text("נוי", Defaults()).RightToLeft);
        Assert.False(Text("Ben", Defaults()).RightToLeft);
    }

    [Fact]
    public void CandleHides_TheCommonSettingsItDecides()
    {
        IItemType item = Item;
        Assert.Contains(Keys.FontSize, item.HiddenCommonFields);
        Assert.Contains(Keys.Outline, item.HiddenCommonFields);
        Assert.Empty(((IItemType)new KeychainItem()).HiddenCommonFields);
    }

    // Unusual settings: everything must still print — closed parts, one piece, raised part 1 mm inside the base,
    // and the ring hole open with a wall around it.
    public static IEnumerable<object[]> Unusual() => new[]
    {
        new object[] { "weight 1", "נויה", (Action<ItemSettings>)(x => x.Values[Keys.Weight] = 1) },
        new object[] { "bold", "שלמה", (Action<ItemSettings>)(x => x.Bold = true) },
        new object[] { "narrow, big hole on top", "נוי", (Action<ItemSettings>)(x => { x.Values[Keys.CandleWidth] = 8; x.Values[Keys.RingHole] = 7; x.Values[Keys.CandleRing] = 1; }) },
        new object[] { "narrowest, weight 2", "Ben", (Action<ItemSettings>)(x => { x.Values[Keys.CandleWidth] = 5; x.Values[Keys.Weight] = 2; }) },
        new object[] { "wide, big rim, hole on top", "נויה", (Action<ItemSettings>)(x => { x.Values[Keys.CandleWidth] = 30; x.Values[Keys.FlameRim] = 3; x.Values[Keys.CandleRing] = 1; }) },
        new object[] { "no end margin, hole on top", "אלכסנדרה", (Action<ItemSettings>)(x => { x.Values[Keys.EndMargin] = 0; x.Values[Keys.CandleRing] = 1; }) },
        new object[] { "longest below margins", "נויה", (Action<ItemSettings>)(x => x.Values[Keys.MaxLength] = 5) },
        new object[] { "letter size 0", "נויה", (Action<ItemSettings>)(x => { x.Values[Keys.LetterFill] = 0; x.Values[Keys.MinLetters] = 0; }) },
        new object[] { "hollow connector", "נוי גל", (Action<ItemSettings>)(x => x.Hollow = true) },
        new object[] { "emoji only", "⭐", (Action<ItemSettings>)(_ => { }) },
        new object[] { "narrowest, loop on top", "נוי", (Action<ItemSettings>)(x => { x.Values[Keys.CandleWidth] = 5; x.Values[Keys.CandleRing] = 2; }) },
        new object[] { "wide, big hole, loop on top", "נויה", (Action<ItemSettings>)(x => { x.Values[Keys.CandleWidth] = 30; x.Values[Keys.RingHole] = 8; x.Values[Keys.CandleRing] = 2; }) },
        new object[] { "weight 1, read upward, loop on top", "שלמה", (Action<ItemSettings>)(x => { x.Values[Keys.Weight] = 1; x.Values[Keys.ReadUp] = 1; x.Values[Keys.CandleRing] = 2; }) },
        new object[] { "smallest hole, loop on top", "נוי", (Action<ItemSettings>)(x => { x.Values[Keys.RingHole] = 1; x.Values[Keys.CandleRing] = 2; }) },
        new object[] { "long name, loop on top", "אלכסנדרה מרים", (Action<ItemSettings>)(x => x.Values[Keys.CandleRing] = 2) },
    };

    [Theory]
    [MemberData(nameof(Unusual))]
    public void UnusualSettings_StillPrint(string why, string name, Action<ItemSettings> change)
    {
        var s = Defaults(change);
        var text = TextOutliner.Build(name, BundledFonts.Get("Gan CLM"), s.Bold, s[Keys.FontSize], "-", s.Hollow);
        var m = Item.Build(text, s);
        var l = CandleItem.Measure(text, s);

        Assert.True(double.IsFinite(l.Width) && double.IsFinite(l.BodyLength) && l.LetterSize > 0, why);
        Assert.True(m.Base.IsManifold(), $"{why}: {m.Base.DescribeOpenEdges()}");
        Assert.True(m.Letters.IsManifold(), $"{why}: {m.Letters.DescribeOpenEdges()}");
        Assert.True(m.LooseParts == 1, $"{why}: {m.LooseParts} pieces");
        var outside = Poly.Difference(Poly.Inflate(m.LetterArea, 0.99), m.BaseArea);
        Assert.True(AreaMm2(outside) < 0.01, $"{why}: {AreaMm2(outside):0.000} mm² of raised part near an edge");

        var hole = m.RingHoleCenter!.Value;
        Assert.Equal(PipResult.Outside, Poly.PointInArea(hole, m.BaseArea));
        // A wall of at least 1 mm all round the hole.
        var holeSize = Poly.Mm(Poly.Bounds(Poly.Difference(new Paths64 { Poly.Circle(0, Poly.Mm(hole.Y), 40) }, m.BaseArea)
            .Where(p => Poly.PointInArea(hole, new Paths64 { p }) != PipResult.Outside).ToList() is { Count: > 0 } inner ? new Paths64(inner) : new Paths64()).Width);
        var ring = Poly.Difference(new Paths64 { Poly.Circle(0, Poly.Mm(hole.Y), holeSize + 2) }, new Paths64 { Poly.Circle(0, Poly.Mm(hole.Y), holeSize + 0.05) });
        Assert.True(AreaMm2(Poly.Difference(ring, m.BaseArea)) < 0.05, $"{why}: hole wall thinner than 1 mm");
    }

    [Fact]
    public void TextWeight_KeepsTheEndMargins()
    {
        var (m, l) = Build("אלכסנדרה", s => s.Values[Keys.Weight] = 1);
        // Below the flame inlay, which dips just under the body's top edge.
        var name = Poly.Bounds(Poly.Intersect(m.LetterArea, new Paths64 { Poly.Rect(-50, -200, 50, -1) }));
        Assert.True(Poly.Mm(name.MaxY) <= -6 + 0.05, $"top margin {-Poly.Mm(name.MaxY):0.00}");
        Assert.True(Poly.Mm(name.MinY) >= -l.BodyLength + 6 - 0.05, $"bottom margin {Poly.Mm(name.MinY) + l.BodyLength:0.00}");
        Assert.True(l.BodyLength <= 85.01);
    }
}
