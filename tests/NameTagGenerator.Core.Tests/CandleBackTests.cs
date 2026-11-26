using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Text;

namespace NameTagGenerator.Tests;

/// <summary>Candle back text: a third part sunk into the bottom, flush with the print bed.</summary>
public class CandleBackTests
{
    static readonly CandleItem Item = new();

    static ItemSettings Settings(string back, Action<ItemSettings>? change = null)
    {
        var s = ItemSettings.DefaultsFor(Item);
        s.Texts[Keys.BackText] = back;
        change?.Invoke(s);
        return s;
    }

    static TextShape Text(string text, ItemSettings s) =>
        TextOutliner.Build(text, BundledFonts.Get("Gan CLM"), false, s[Keys.FontSize], "-", false);

    static (ItemModel Model, CandleLayout Layout) Build(string name, string back, Action<ItemSettings>? change = null)
    {
        var s = Settings(back, change);
        var extras = new Dictionary<string, TextShape>();
        if (back.Length > 0) extras[Keys.BackText] = TextOutliner.Build(back, BundledFonts.Get("Gan CLM"), false, s[Keys.FontSize], "", false);
        var text = Text(name, s);
        return (Item.Build(text, s, extras), CandleItem.Measure(text, s, extras.GetValueOrDefault(Keys.BackText)));
    }

    static double AreaMm2(Paths64 area) => Clipper.Area(area) / (Poly.Scale * Poly.Scale);

    static double MinZ(Mesh m) => Poly.Mm(m.Vertices.Min(v => v.Z));
    static double MaxZ(Mesh m) => Poly.Mm(m.Vertices.Max(v => v.Z));

    [Fact]
    public void BackText_IsListedAsTextField_WithDepthAndFlip()
    {
        Assert.Contains(Item.Fields, f => f.Key == Keys.BackText && f.Kind == FieldKind.Text);
        Assert.Contains(Item.Fields, f => f.Key == Keys.BackFlip && f.Kind == FieldKind.Toggle);
        Assert.Equal(0.6, Item.Defaults[Keys.BackDepth]);
        Assert.Equal(0, Item.Defaults[Keys.BackFlip]);
    }

    [Fact]
    public void NoBackText_IsTheCandleAsBefore()
    {
        var s = ItemSettings.DefaultsFor(Item);
        var text = Text("נויה", s);
        var before = Item.Build(text, s);
        var (after, _) = Build("נויה", "");
        Assert.Null(after.Back);
        Assert.Equal(before.Base.VolumeMm3, after.Base.VolumeMm3, 6);
        Assert.Equal(before.Letters.VolumeMm3, after.Letters.VolumeMm3, 6);
    }

    [Theory]
    [InlineData("נוי", "חנוכה שמח")]
    [InlineData("Ben", "Happy Hanukkah")]
    [InlineData("אלכסנדרה", "חג")]
    [InlineData("נוי", "חג Dana")]
    [InlineData("Ben", "Happy חג")]
    public void BackText_IsItsOwnClosedPart_FlushWithTheBottom(string name, string back)
    {
        var (m, _) = Build(name, back);
        Assert.NotNull(m.Back);
        Assert.True(m.Back!.IsManifold(), m.Back.DescribeOpenEdges());
        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
        Assert.Equal(0, MinZ(m.Back), 3);
        Assert.Equal(0.6, MaxZ(m.Back), 3);
        Assert.Equal(0, MinZ(m.Base), 3);
        Assert.Equal(1, m.LooseParts);
    }

    [Fact]
    public void BackText_FillsAPocketCutIntoTheBase()
    {
        // The back text is shorter than the name, so the candle's size does not change.
        var (plain, _) = Build("אלכסנדרה", "");
        var (with, _) = Build("אלכסנדרה", "חג");
        Assert.True(with.Back!.VolumeMm3 > 5);
        Assert.Equal(plain.Base.VolumeMm3, with.Base.VolumeMm3 + with.Back.VolumeMm3, 2);
        Assert.Equal(AreaMm2(with.BackArea!) * 0.6, with.Back.VolumeMm3, 2);
    }

    [Fact]
    public void ThinBase_LeavesOutTheBackPart_InsteadOfABrokenPocket()
    {
        var (m, _) = Build("נוי", "חג", s => s.Values[Keys.BaseHeight] = 0.2);
        Assert.Null(m.Back);
        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
    }

    [Fact]
    public void ThinBase_KeepsAtLeast0_2mmAboveThePocket()
    {
        var (m, _) = Build("נוי", "חג", s => s.Values[Keys.BaseHeight] = 0.3);
        Assert.Equal(0.1, MaxZ(m.Back!), 3);
        Assert.True(m.Back!.IsManifold(), m.Back.DescribeOpenEdges());
        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
    }

    [Fact]
    public void BackDepth_IsASetting()
    {
        var (m, _) = Build("נוי", "חג", s => s.Values[Keys.BackDepth] = 1);
        Assert.Equal(1, MaxZ(m.Back!), 3);
    }

    [Fact]
    public void LongerBackText_SetsTheBodyLength()
    {
        var (_, nameOnly) = Build("נוי", "");
        var (_, l) = Build("נוי", "חג שמח");
        Assert.Equal(45, nameOnly.BodyLength, 2);
        Assert.Equal(11.2, l.LetterSize, 2);
        Assert.InRange(l.BodyLength, 45.01, 85);
        Assert.Equal(l.BackLength + 12, l.BodyLength, 2);
    }

    [Fact]
    public void LongerName_StillSetsTheBodyLength()
    {
        var (_, plain) = Build("אלכסנדרה", "");
        var (_, l) = Build("אלכסנדרה", "חג");
        Assert.Equal(plain.BodyLength, l.BodyLength, 3);
        Assert.Equal(plain.LetterSize, l.LetterSize, 3);
    }

    [Fact]
    public void LongBackText_ShrinksLettersLikeALongName()
    {
        var (_, l) = Build("נוי", "חנוכה שמח");
        Assert.Equal(85, l.BodyLength, 2);
        Assert.InRange(l.LetterSize, 7, 11.19);
    }

    [Fact]
    public void VeryLongBackText_StopsAtTheSmallestLetters_AndTheBodyGrows()
    {
        var (_, l) = Build("נוי", "חנוכה שמח לכל המשפחה");
        Assert.Equal(7, l.LetterSize, 3);
        Assert.True(l.BodyLength > 85);
    }

    // With the same text front and back, turning the candle over (x → -x) shows the back text exactly where the name is.
    [Theory]
    [InlineData("נויה")]
    [InlineData("Ben")]
    public void BackText_IsMirrored_SoItReadsLikeTheNameWhenTurnedOver(string text)
    {
        var (m, _) = Build(text, text);
        // The raised part also holds the flame inlay; compare only the body.
        var body = new Paths64 { Poly.Rect(-50, -200, 50, -3) };
        var name = Poly.Intersect(m.LetterArea, body);
        var turned = Poly.Mirror(m.BackArea!);
        var xor = Clipper.Xor(turned, name, FillRule.NonZero);
        Assert.True(AreaMm2(xor) < 0.02 * AreaMm2(name), $"mismatch {AreaMm2(xor):0.00} mm²");
    }

    // Flip = half a turn in the bed's plane: the first letter moves from the flame end to the ring end.
    [Theory]
    [InlineData("נויה")]
    [InlineData("Ben")]
    public void FlipBackText_TurnsItEndOverEnd(string text)
    {
        var (plain, _) = Build("נוי", text);
        var (flipped, _) = Build("נוי", text, s => s.Values[Keys.BackFlip] = 1);
        var a = Poly.Bounds(plain.BackArea!);
        var b = Poly.Bounds(flipped.BackArea!);
        // Half a turn about the flipped text's middle, then moved onto the plain text's middle.
        long sx = b.MinX + b.MaxX, sy = b.MinY + b.MaxY;
        long dx = (a.MinX + a.MaxX - (b.MinX + b.MaxX)) / 2, dy = (a.MinY + a.MaxY - (b.MinY + b.MaxY)) / 2;
        var turned = new Paths64(flipped.BackArea!.Select(p => new Path64(p.Select(q => new Point64(sx - q.X + dx, sy - q.Y + dy)))));
        var xor = Clipper.Xor(turned, plain.BackArea!, FillRule.NonZero);
        Assert.True(AreaMm2(xor) < 0.02 * AreaMm2(plain.BackArea!), $"mismatch {AreaMm2(xor):0.00} mm²");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BackText_StaysInsideTheBody_AndClearOfTheHole(int ring)
    {
        var (m, _) = Build("נוי", "חנוכה שמח", s => s.Values[Keys.CandleRing] = ring);
        var inside = Poly.Inflate(m.BaseArea, -1);
        Assert.True(AreaMm2(Poly.Difference(m.BackArea!, inside)) < 0.01);
        if (m.RingHoleCenter is { } c)
        {
            var near = new Paths64 { Poly.Circle(Poly.Mm(c.X), Poly.Mm(c.Y), 4 + 2) };
            Assert.True(AreaMm2(Poly.Intersect(m.BackArea!, near)) < 0.01);
        }
    }

    [Fact]
    public void BackText_WithTextWeightAndReadUpward_StaysClosed()
    {
        var (m, _) = Build("Ben", "Happy Hanukkah", s => { s.Values[Keys.Weight] = 0.3; s.Values[Keys.ReadUp] = 1; });
        Assert.True(m.Back!.IsManifold(), m.Back.DescribeOpenEdges());
        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
    }

    [Fact]
    public void BackText_IsSavedWithTheSettings()
    {
        var s = Settings("חג שמח");
        var json = System.Text.Json.JsonSerializer.Serialize(s);
        var back = System.Text.Json.JsonSerializer.Deserialize<ItemSettings>(json)!;
        Assert.Equal("חג שמח", back.Texts[Keys.BackText]);
    }
}
