using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Text;
using NameTagGenerator.Views;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NameTagGenerator.Tests;

/// <summary>Gaps in the base, the three newer items and the preview colours.</summary>
public class ShapeTests
{
    static TextShape TwoSquares(double gap) => new(
        new Paths64 { Poly.Rect(0, 0, 10, 10), Poly.Rect(10 + gap, 0, 20 + gap, 10) }, new Paths64());

    static ItemSettings Settings(double fill) => new()
    {
        Values = new Dictionary<string, double> { [Keys.Outline] = 0, [Keys.FillGaps] = fill },
    };

    static (ItemModel Model, ItemSettings S) Build(IItemType item, string name, Action<ItemSettings>? change = null)
    {
        var s = ItemSettings.DefaultsFor(item);
        change?.Invoke(s);
        return (item.Build(TextOutliner.Build(name, FontCatalog.Resolve("Gan CLM"), false, s[Keys.FontSize], "-", false), s), s);
    }

    static bool Solid(ItemModel m, double x, double y) =>
        Poly.PointInArea(new Point64(Poly.U(x), Poly.U(y)), m.BaseArea) == PipResult.Inside;

    [Fact]
    public void NarrowGapBetweenLetters_IsFilledInTheBase_ButTheLettersStayApart()
    {
        var (letters, baseArea) = TagAreas.Build(TwoSquares(2), Settings(4));
        Assert.Equal(1, Poly.CountIslands(baseArea));
        Assert.Equal(2, Poly.CountIslands(letters));
    }

    [Fact]
    public void WideGap_StaysOpen()
    {
        var (_, baseArea) = TagAreas.Build(TwoSquares(10), Settings(4));
        Assert.Equal(2, Poly.CountIslands(baseArea));
    }

    [Fact]
    public void FillGapsZero_LeavesAGapAlone()
    {
        var (_, baseArea) = TagAreas.Build(TwoSquares(2), Settings(0));
        Assert.Equal(2, Poly.CountIslands(baseArea));
    }

    [Fact]
    public void HoleInsideALetter_IsFilledInTheBase()
    {
        // A square ring, like a letter with a big opening: the print must not see through it.
        var ring = Poly.Difference(new Paths64 { Poly.Rect(0, 0, 30, 30) }, new Paths64 { Poly.Rect(10, 10, 20, 20) });
        var (_, baseArea) = TagAreas.Build(new TextShape(ring, new Paths64()), Settings(0));
        Assert.Single(baseArea);
        Assert.Equal(PipResult.Inside, Poly.PointInArea(new Point64(Poly.U(15), Poly.U(15)), baseArea));
    }

    [Theory]
    [InlineData("ליב")]
    [InlineData("נויה")]
    [InlineData("אבי")]
    [InlineData("ס")]
    public void Bookmark_HasNoPocketsOrThinJoinsAroundTheName(string name)
    {
        var item = new BookmarkItem();
        var (model, s) = Build(item, name);
        var slitTop = SlitTop(name, s);

        Assert.Equal(1, Poly.CountIslands(model.BaseArea));

        // The U-shaped slit is meant to be there, so only the part above it must be free of holes.
        var box = Poly.Bounds(model.BaseArea);
        var top = Poly.Intersect(model.BaseArea, new Paths64 { Poly.Rect(-200, slitTop, 200, Poly.Mm(box.MaxY) + 1) });
        Assert.DoesNotContain(top, p => !Clipper.IsPositive(p));

        // Above the slits, closing up any narrow gap must add almost nothing.
        var closed = Poly.Inflate(Poly.Inflate(top, 1.5), -1.5);
        var pockets = Clipper.Area(Poly.Difference(closed, top)) / (Poly.Scale * Poly.Scale);
        Assert.True(pockets < 4, $"pockets add up to {pockets:0.0} mm2");
    }

    [Theory]
    [InlineData("ליב")]
    [InlineData("אבי")]
    public void Bookmark_SlitsStartAtTheSameDistanceBelowTheNameWhateverTheName(string name)
    {
        var (model, s) = Build(new BookmarkItem(), name);
        var slitTop = SlitTop(name, s);
        var slitX = s[Keys.ClipWidth] / 2 - s[Keys.ClipBorder] - s[Keys.ClipGap] / 2;
        foreach (var x in new[] { -slitX, slitX })
        {
            Assert.True(Solid(model, x, slitTop + 0.2), "solid just above the slit");
            Assert.False(Solid(model, x, slitTop - 1), "open just below");
        }
    }

    static double SlitTop(string name, ItemSettings s)
    {
        var text = TextOutliner.Build(name, FontCatalog.Resolve("Gan CLM"), false, s[Keys.FontSize], "-", false);
        var (_, baseText) = TagAreas.Build(TagAreas.Position(text, TagAreas.Center), s);
        return Poly.Mm(Poly.Bounds(baseText).MinY) - s[Keys.ClipBorder];
    }

    [Theory]
    [InlineData("deskplate")]
    [InlineData("cupmarker")]
    [InlineData("winemarker")]
    public void NewItems_AreListed_AndOnePiece(string id)
    {
        var item = ItemRegistry.All.Single(i => i.Id == id);
        foreach (var name in new[] { "ליב", "אבי", "ס", "שלום עליכם" })
        {
            var (model, _) = Build(item, name);
            Assert.Equal(1, model.LooseParts);
            Assert.NotEmpty(model.BaseArea);
            Assert.DoesNotContain(model.BaseArea, p => !Clipper.IsPositive(p));
        }
    }

    [Fact]
    public void DeskPlate_SitsFlat_LeansBack_AndWillNotTip()
    {
        var item = new DeskPlateItem();
        var (m, s) = Build(item, "נויה");
        var body = m.Base.Vertices;

        Assert.Equal(0, body.Min(v => v.Z));
        // The foot is a real flat area: the whole width on the plate, front and back.
        var foot = body.Where(v => v.Z == 0).ToList();
        Assert.True(Poly.Mm(foot.Max(v => v.Y) - foot.Min(v => v.Y)) >= s[Keys.StandDepth] - 0.01);
        Assert.True(foot.Max(v => v.X) > 0 && foot.Min(v => v.X) < 0);

        // The face leans back by the chosen angle: going up the face also goes backward.
        var tilt = s[Keys.Tilt] * Math.PI / 180;
        var heights = m.Letters.Vertices.Select(v => Poly.Mm(v.Z)).ToList();
        Assert.True(heights.Max() > heights.Min() + 5);
        var low = m.Letters.Vertices.Where(v => Poly.Mm(v.Z) * Math.Sin(tilt) - Poly.Mm(v.Y) * Math.Cos(tilt) < 0.01).OrderBy(v => v.Z).First();
        var high = m.Letters.Vertices.Where(v => Poly.Mm(v.Z) * Math.Sin(tilt) - Poly.Mm(v.Y) * Math.Cos(tilt) < 0.01).OrderBy(v => v.Z).Last();
        var lean = Math.Atan2(high.Y - low.Y, high.Z - low.Z) * 180 / Math.PI;
        Assert.InRange(lean, s[Keys.Tilt] - 0.5, s[Keys.Tilt] + 0.5);

        Assert.InRange(CentreOfMassY(m.Base), 0, s[Keys.StandDepth]);
    }

    [Fact]
    public void DeskPlate_LettersSitOnTheFace_NotInsideOrFloating()
    {
        var (m, s) = Build(new DeskPlateItem(), "נויה");
        var tilt = s[Keys.Tilt] * Math.PI / 180;
        // Height of each letter corner above the face, measured square to the face.
        var heights = m.Letters.Vertices.Select(v => Poly.Mm(v.Z) * Math.Sin(tilt) - Poly.Mm(v.Y) * Math.Cos(tilt)).ToList();
        Assert.InRange(heights.Min(), -0.01, 0.01);
        Assert.InRange(heights.Max(), s[Keys.LetterHeight] - 0.01, s[Keys.LetterHeight] + 0.01);
    }

    static double CentreOfMassY(Mesh mesh)
    {
        double volume = 0, moment = 0;
        for (var i = 0; i < mesh.Indices.Count; i += 3)
        {
            var a = mesh.Vertices[mesh.Indices[i]];
            var b = mesh.Vertices[mesh.Indices[i + 1]];
            var c = mesh.Vertices[mesh.Indices[i + 2]];
            var v = (a.X * ((double)b.Y * c.Z - (double)b.Z * c.Y) - a.Y * ((double)b.X * c.Z - (double)b.Z * c.X) + a.Z * ((double)b.X * c.Y - (double)b.Y * c.X)) / 6;
            volume += v;
            moment += v * (a.Y + b.Y + c.Y) / 4.0;
        }
        return Poly.Mm((long)(moment / volume));
    }

    static double Radius(Point64 p, double centreY) => Math.Sqrt(Math.Pow(Poly.Mm(p.X), 2) + Math.Pow(Poly.Mm(p.Y) - centreY, 2));

    [Theory]
    [InlineData(3.0, 80.0)]
    [InlineData(2.0, 70.0)]
    [InlineData(5.5, 100.0)]
    public void CupMarker_GrooveFollowsTheRimCurve_AndFitsItsThickness(double rim, double cup)
    {
        var s = ItemSettings.DefaultsFor(new CupMarkerItem());
        s.Values[Keys.RimThickness] = rim;
        s.Values[Keys.CupDiameter] = cup;
        var grip = CupMarkerItem.Grip(s);
        var rimCentre = (cup - rim) / 2;
        Assert.Equal(rimCentre, grip.Radius, 6);

        var radii = grip.Walls.SelectMany(p => p).Select(p => Radius(p, grip.CentreY)).ToList();
        var inner = radii.Where(r => r < rimCentre).ToList();
        var outer = radii.Where(r => r > rimCentre).ToList();
        // At every point along the arc, the gap between the two walls is the rim plus a small clearance.
        Assert.InRange(inner.Max(), rimCentre - rim / 2 - 0.11, rimCentre - rim / 2 - 0.09);
        Assert.InRange(outer.Min(), rimCentre + rim / 2 + 0.09, rimCentre + rim / 2 + 0.11);
    }

    [Theory]
    [InlineData("ס")]
    [InlineData("ליב")]
    [InlineData("שלום עליכם")]
    public void CupMarker_GripsAtLeast25MmOfRim_WhateverTheNameLength(string name)
    {
        var (m, s) = Build(new CupMarkerItem(), name);
        var grip = CupMarkerItem.Grip(s);
        Assert.True(2 * grip.HalfAngle * grip.Radius >= 25, "The whole point of the design: it must not be able to rock.");
        Assert.Equal(2, grip.Walls.Count);

        // Both walls stand fully on the plate, so nothing hangs in the air while printing.
        var mirrored = Poly.Mirror(grip.Walls);
        var outside = Poly.Difference(mirrored, m.BaseArea);
        Assert.Empty(outside);
    }

    [Fact]
    public void CupMarker_GripIsCentredUnderTheName_SoItDoesNotLeanSideways()
    {
        var (m, s) = Build(new CupMarkerItem(), "דניאלה");
        var walls = Poly.Bounds(CupMarkerItem.Grip(s).Walls);
        var plate = Poly.Bounds(m.BaseArea);

        Assert.InRange(Poly.Mm(walls.MinX + walls.MaxX), -0.01, 0.01);
        Assert.InRange(Poly.Mm(plate.MinX + plate.MaxX), -1.5, 1.5);
    }

    [Theory]
    [InlineData("ליב")]
    [InlineData("ם")]
    [InlineData("אברהם יצחק יעקב")]
    [InlineData("Noa")]
    public void CupMarker_IsClosed_WithFlushLettersOnThePrintBed(string name)
    {
        var (m, s) = Build(new CupMarkerItem(), name);

        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
        Assert.True(m.Letters.IsManifold(), m.Letters.DescribeOpenEdges());
        Assert.Equal(0, m.Letters.Vertices.Min(v => v.Z));
        Assert.Equal(Poly.U(s[Keys.LetterHeight]), m.Letters.Vertices.Max(v => v.Z));
        Assert.Equal(0, m.Base.Vertices.Min(v => v.Z));
        Assert.Equal(Poly.U(s[Keys.BaseHeight] + s[Keys.GripDepth]), m.Base.Vertices.Max(v => v.Z));
    }

    [Fact]
    public void CupMarker_StaysClosed_WithNoOutlineAndThinPlate()
    {
        var (m, s) = Build(new CupMarkerItem(), "נויה", v =>
        {
            v.Values[Keys.Outline] = 0;
            v.Values[Keys.BaseHeight] = 0.6;
            v.Values[Keys.LetterHeight] = 2;
        });

        Assert.True(m.Base.IsManifold(), m.Base.DescribeOpenEdges());
        // The letters are never deeper than the plate, so the walls still have something under them.
        Assert.True(Poly.Mm(m.Letters.Vertices.Max(v => v.Z)) < s[Keys.BaseHeight]);
    }

    [Fact]
    public void CupMarker_PartsFillThePlateExactly_WithNoOverlap()
    {
        var (m, s) = Build(new CupMarkerItem(), "ליב");
        var plate = Clipper.Area(m.BaseArea) / (Poly.Scale * Poly.Scale) * s[Keys.BaseHeight];
        var walls = Clipper.Area(Poly.Mirror(CupMarkerItem.Grip(s).Walls)) / (Poly.Scale * Poly.Scale) * s[Keys.GripDepth];

        Assert.Equal(plate + walls, m.Base.VolumeMm3 + m.Letters.VolumeMm3, 0);
    }

    [Fact]
    public void CupMarker_IsMirroredForPrinting_SoItReadsCorrectlyOnTheCup()
    {
        var item = new CupMarkerItem();
        var s = ItemSettings.DefaultsFor(item);
        // Two "letters" of different widths show which way round the name ends up.
        var text = new TextShape(new Paths64 { Poly.Rect(5, -4, 25, 4), Poly.Rect(-20, -4, -15, 4) }, new Paths64());

        var m = item.Build(text, s);
        // Read from the name side the wide letter is on the right, so on the print bed it is on the left.
        var wideLetter = m.LetterArea.Select(p => Poly.Bounds(new Paths64 { p })).MaxBy(b => b.Width);
        Assert.True(wideLetter.MaxX < 0);
    }

    [Fact]
    public void Preview_ShowsTheCupMarkerNameSideUp_ReadingCorrectly()
    {
        var item = new CupMarkerItem();
        var s = ItemSettings.DefaultsFor(item);
        var text = new TextShape(new Paths64 { Poly.Rect(5, -4, 25, 4), Poly.Rect(-20, -4, -15, 4) }, new Paths64());
        var m = item.Build(text, s);

        var group = (Model3DGroup)PreviewBuilder.Build(new[] { new NameTagGenerator.Output.PlacedTag("x", m, 0, 0) }, null);
        var letters = (MeshGeometry3D)((GeometryModel3D)group.Children[1]).Geometry;
        var body = (MeshGeometry3D)((GeometryModel3D)group.Children[0]).Geometry;

        // Letters on top of everything, at the face of the plate.
        Assert.Equal(body.Positions.Max(p => p.Z), letters.Positions.Max(p => p.Z), 6);
        Assert.Equal(0, body.Positions.Min(p => p.Z), 6);
        // Centred, the wide letter runs from 2.5 to 22.5. It is back on the right, not mirrored to the left.
        Assert.Contains(letters.Positions, p => Math.Abs(p.X - 2.5) < 0.01);
        Assert.DoesNotContain(letters.Positions, p => Math.Abs(p.X + 2.5) < 0.01);
    }

    [Fact]
    public void Preview_LeavesOtherItemsAsTheyPrint()
    {
        var (m, s) = Build(new DeskPlateItem(), "ליב");
        var group = (Model3DGroup)PreviewBuilder.Build(new[] { new NameTagGenerator.Output.PlacedTag("x", m, 0, 0) }, null);
        var letters = (MeshGeometry3D)((GeometryModel3D)group.Children[1]).Geometry;

        Assert.Equal(Poly.Mm(m.Letters.Vertices.Max(v => v.Z)), letters.Positions.Max(p => p.Z), 6);
        Assert.Equal(Poly.Mm(m.Letters.Vertices.Min(v => v.Z)), letters.Positions.Min(p => p.Z), 6);
    }

    [Fact]
    public void CupMarker_DefaultsToAThreeMillimetreRim()
    {
        Assert.Equal(3, new CupMarkerItem().Defaults[Keys.RimThickness]);
    }

    [Theory]
    [InlineData(7.0, 5.5)]
    [InlineData(10.0, 8.0)]
    public void WineMarker_ClipsAroundTheStemThroughANarrowerOpening(double stem, double opening)
    {
        var item = new WineMarkerItem();
        var (m, s) = Build(item, "ליב", v => { v.Values[Keys.StemDiameter] = stem; v.Values[Keys.StemOpening] = opening; });
        Assert.NotNull(m.RingHoleCenter);
        var cx = Poly.Mm(m.RingHoleCenter!.Value.X);
        var outward = cx > 0 ? 1 : -1;

        // The stem fits, and the wall surrounds it.
        Assert.False(Solid(m, cx, 0));
        Assert.False(Solid(m, cx, stem / 2 - 0.1));
        Assert.True(Solid(m, cx, stem / 2 + 0.3));
        Assert.True(Solid(m, cx - outward * (stem / 2 + 0.3), 0));

        // The opening faces away from the name and is narrower than the stem, so the clip holds.
        var lip = cx + outward * Math.Sqrt(stem * stem / 4 - opening * opening / 4);
        Assert.False(Solid(m, lip, opening / 2 - 0.1));
        Assert.True(Solid(m, lip + outward * 0.3, opening / 2 + 0.3));
        Assert.False(Solid(m, cx + outward * (stem / 2 + s[Keys.StemWall] / 2), 0));
    }

    [Theory]
    [InlineData("#1E90FF", 0x1E, 0x90, 0xFF)]
    [InlineData("#f4f6f8", 0xF4, 0xF6, 0xF8)]
    public void Colours_ReadFromText(string text, byte r, byte g, byte b)
    {
        Assert.Equal(Color.FromRgb(r, g, b), PreviewBuilder.ParseColour(text, Colors.Black));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("blue")]
    [InlineData("#12")]
    public void Colours_FallBackWhenTheSavedTextIsDamaged(string? text)
    {
        Assert.Equal(Colors.Red, PreviewBuilder.ParseColour(text, Colors.Red));
    }

    [Fact]
    public void Preview_UsesTheChosenColours()
    {
        var (m, _) = Build(new KeychainItem(), "ליב");
        var tag = new NameTagGenerator.Output.PlacedTag("ליב", m, 0, 0);
        var group = (Model3DGroup)PreviewBuilder.Build(new[] { tag }, null, "#112233", "#445566");
        Color ColourOf(int i) => ((SolidColorBrush)((DiffuseMaterial)((GeometryModel3D)group.Children[i]).Material).Brush).Color;
        Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), ColourOf(0));
        Assert.Equal(Color.FromRgb(0x44, 0x55, 0x66), ColourOf(1));
    }
}
