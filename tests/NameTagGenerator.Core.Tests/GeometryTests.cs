using System.IO;
using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Output;
using NameTagGenerator.Text;

namespace NameTagGenerator.Tests;

public class MeshTests
{
    [Fact]
    public void ExtrudedSquare_IsClosedAndHasExpectedVolume()
    {
        var mesh = new Mesh();
        mesh.Extrude(new Paths64 { Poly.Rect(0, 0, 10, 20) }, Poly.U(0), Poly.U(3));

        Assert.True(mesh.IsManifold());
        Assert.Equal(600, mesh.VolumeMm3, 3);
    }

    [Fact]
    public void ExtrudedSquareWithHole_IsClosedAndHoleReducesVolume()
    {
        var area = Poly.Difference(new Paths64 { Poly.Rect(0, 0, 10, 10) }, new Paths64 { Poly.Rect(2, 2, 8, 8) });
        var mesh = new Mesh();
        mesh.Extrude(area, Poly.U(0), Poly.U(2));

        Assert.True(mesh.IsManifold());
        Assert.Equal((100 - 36) * 2, mesh.VolumeMm3, 3);
    }
}

public class StlTests
{
    [Fact]
    public void BinaryStl_HasCorrectTriangleCountAndMillimetreCoordinates()
    {
        var mesh = new Mesh();
        mesh.Extrude(new Paths64 { Poly.Rect(0, 0, 10, 20) }, Poly.U(0), Poly.U(3));
        using var ms = new MemoryStream();

        StlWriter.Write(ms, new[] { (mesh, 0.0, 0.0) });

        var bytes = ms.ToArray();
        var count = BitConverter.ToUInt32(bytes, 80);
        Assert.Equal((uint)mesh.TriangleCount, count);
        Assert.Equal(84 + 50 * mesh.TriangleCount, bytes.Length);
        var maxZ = 0f;
        for (var t = 0; t < count; t++)
            for (var v = 0; v < 3; v++)
                maxZ = Math.Max(maxZ, BitConverter.ToSingle(bytes, 84 + t * 50 + 12 + v * 12 + 8));
        Assert.Equal(3f, maxZ, 3);
    }
}

public class TextTests
{
    static readonly OutlineFont Arial = BundledFonts.Get("Rubik");

    [Fact]
    public void HebrewName_IsNotMirrored_FirstLetterIsOnTheRight()
    {
        // Lamed rises above the other letters, so the highest ink shows where it landed.
        var shape = TextOutliner.Build("לא", Arial, false, 18, "-", false);
        var b = Poly.Bounds(shape.Letters);
        var top = shape.Letters.SelectMany(p => p).MaxBy(v => v.Y);

        Assert.True(top.X > (b.MinX + b.MaxX) / 2, "Lamed should be in the right half");
    }

    [Fact]
    public void LongerName_MakesTagWiderButKeepsLetterHeight()
    {
        var shortName = Poly.Bounds(TextOutliner.Build("נננ", Arial, false, 18, "-", false).Letters);
        var longName = Poly.Bounds(TextOutliner.Build("נננננננננ", Arial, false, 18, "-", false).Letters);

        Assert.True(longName.Width > shortName.Width * 2);
        Assert.Equal(shortName.Height, longName.Height);
    }

    [Fact]
    public void HollowConnector_IsLeftOutOfTheLetters_AndWordsKeepTheirPosition()
    {
        var solid = TextOutliner.Build("אב גד", Arial, false, 18, "-", false);
        var hollow = TextOutliner.Build("אב גד", Arial, false, 18, "-", true);

        Assert.Empty(solid.Connector);
        Assert.NotEmpty(hollow.Connector);
        var solidBounds = Poly.Bounds(solid.Letters);
        var hollowBounds = Poly.Bounds(hollow.Letters);
        Assert.Equal(solidBounds.MinX, hollowBounds.MinX);
        Assert.Equal(solidBounds.MaxX, hollowBounds.MaxX);
    }

    static string SampleFor(string font) =>
        BundledFonts.Get(font).HasHebrew ? "שלום" : "Hello";

    [Fact]
    public void BuiltInFonts_AllDrawDifferently()
    {
        // Each font in its own script, so a font never falls back to a Windows font here.
        var widths = BundledFonts.Names
            .Select(n => Poly.Bounds(TextOutliner.Build(SampleFor(n), BundledFonts.Get(n), false, 18, "-", false).Letters).Width)
            .ToList();

        Assert.Equal(BundledFonts.Names.Count, widths.Distinct().Count());
    }

    [Fact]
    public void BuiltInFontList_ContainsAllBundledFonts()
    {
        Assert.Equal(new[]
        {
            "Alef", "Assistant", "Bebas Neue", "Chewy", "Comfortaa", "Fredoka", "Gan CLM", "Heebo", "Lobster",
            "MigdalHaemeq", "Noot", "Pacifico", "Righteous", "Rubik", "Secular One", "Suez One", "Varela Round",
        }, BundledFonts.Names.OrderBy(n => n));
    }

    [Fact]
    public void BuiltInFonts_HaveNoPartsTooThinToPrint()
    {
        // Parts under 0.8 mm (two nozzle widths) vanish when shrunk and regrown by 0.4 mm.
        foreach (var font in BundledFonts.Names)
        {
            var letters = TextOutliner.Build(SampleFor(font) + " נויה Noa", BundledFonts.Get(font), false, 12, "", false).Letters;
            var opened = Poly.Inflate(Poly.Inflate(letters, -0.4), 0.4);
            var lost = 1 - Clipper.Area(opened) / Clipper.Area(letters);
            Assert.True(lost < 0.03, $"{font}: {lost:P1} of the letters is thinner than 0.8 mm");
        }
    }
}

public class ItemTests
{
    static TextShape Shape(string name = "ליב") =>
        TextOutliner.Build(name, BundledFonts.Get("Rubik"), false, 18, "-", false);

    static ItemSettings Defaults(IItemType item) => ItemSettings.DefaultsFor(item);

    [Fact]
    public void Registry_FindsTheThreeItemsAutomatically()
    {
        var ids = ItemRegistry.All.Select(i => i.Id).ToArray();
        Assert.Contains("keychain", ids);
        Assert.Contains("pencil", ids);
        Assert.Contains("bookmark", ids);
    }

    [Theory]
    [InlineData("keychain")]
    [InlineData("pencil")]
    [InlineData("bookmark")]
    public void EveryItem_BuildsClosedBaseAndLetterBodies(string id)
    {
        var item = ItemRegistry.All.Single(i => i.Id == id);
        var model = item.Build(Shape(), Defaults(item));

        Assert.True(model.Base.IsManifold(), $"{id} base: {model.Base.DescribeOpenEdges()}");
        Assert.True(model.Letters.IsManifold(), $"{id} letters: {model.Letters.DescribeOpenEdges()}");
        Assert.True(model.Base.VolumeMm3 > 0);
        Assert.True(model.Letters.VolumeMm3 > 0);
    }

    [Fact]
    public void Keychain_RingHoleReallyGoesThrough()
    {
        var item = new KeychainItem();
        var model = item.Build(Shape(), Defaults(item));

        Assert.Equal(PipResult.Outside, Poly.PointInArea(model.RingHoleCenter!.Value, model.BaseArea));
        Assert.Equal(PipResult.Outside, Poly.PointInArea(model.RingHoleCenter!.Value, model.LetterArea));
    }

    [Fact]
    public void Keychain_RingIsOnTheChosenSide()
    {
        var item = new KeychainItem();
        var s = Defaults(item);

        s.Values[Keys.RingSide] = 0;
        Assert.True(item.Build(Shape(), s).RingHoleCenter!.Value.X > 0);
        s.Values[Keys.RingSide] = 1;
        Assert.True(item.Build(Shape(), s).RingHoleCenter!.Value.X < 0);
    }

    [Fact]
    public void Keychain_WithoutRing_HasNoHole()
    {
        var item = new KeychainItem();
        var s = Defaults(item);
        s.Values[Keys.AddRing] = 0;

        Assert.Null(item.Build(Shape(), s).RingHoleCenter);
    }

    [Fact]
    public void Keychain_Defaults_MatchTheOldApp()
    {
        var s = Defaults(new KeychainItem());

        Assert.Equal(18, s[Keys.FontSize]);
        Assert.Equal(3, s[Keys.BaseHeight]);
        Assert.Equal(4, s[Keys.LetterHeight]);
        Assert.Equal(3, s[Keys.Outline]);
        Assert.Equal(-2, s[Keys.RingAdjust]);
    }

    [Fact]
    public void Bookmark_IsTallerThanItsClipLength()
    {
        var item = new BookmarkItem();
        var s = Defaults(item);
        var b = Poly.Bounds(item.Build(Shape(), s).BaseArea);

        Assert.True(Poly.Mm(b.Height) >= s[Keys.ClipLength]);
    }

    [Fact]
    public void Bookmark_HasAGapForTheClip()
    {
        var item = new BookmarkItem();
        var s = Defaults(item);
        var model = item.Build(Shape(), s);

        // Halfway down the clip, just inside the border, is the U-cut gap.
        var gapX = s[Keys.ClipWidth] / 2 - s[Keys.ClipBorder] - s[Keys.ClipGap] / 2;
        var y = -s[Keys.FontSize] / 2 + 1 - s[Keys.ClipLength] / 2;
        Assert.Equal(PipResult.Outside, Poly.PointInArea(new Point64(Poly.U(gapX), Poly.U(y)), model.BaseArea));
    }

    [Fact]
    public void PencilBody_OnARectangle_MatchesHandCalculatedVolume()
    {
        const double w = 40, depth = 20, h = 11, d = 7.8;
        var area = new Paths64 { Poly.Rect(-w / 2, -depth / 2, w / 2, depth / 2) };

        var mesh = PencilBody.Build(area, h, d, 0);

        var expected = w * depth * h - Math.PI * d * d / 4 * w;
        Assert.True(mesh.IsManifold(), mesh.DescribeOpenEdges());
        Assert.InRange(mesh.VolumeMm3, expected * 0.99, expected * 1.01);
    }

    [Fact]
    public void Pencil_TubeJoinsLettersThatDoNotTouch()
    {
        // Two separate blocks with a gap: the tube must bridge them, adding material in the gap.
        var area = new Paths64 { Poly.Rect(-20, -8, -3, 8), Poly.Rect(3, -8, 20, 8) };

        var withTube = PencilBody.Build(area, 11, 7.8, 10);
        var blocksOnly = 2 * 17 * 16 * 11 - Math.PI * 7.8 * 7.8 / 4 * 34;

        Assert.True(withTube.IsManifold(), withTube.DescribeOpenEdges());
        Assert.True(withTube.VolumeMm3 > blocksOnly);
    }

    [Fact]
    public void Keychain_FarAwayRing_IsReportedAsLoosePart()
    {
        var item = new KeychainItem();
        var s = Defaults(item);
        s.Values[Keys.RingAdjust] = 30;

        Assert.True(item.Build(Shape(), s).LooseParts > 1);
    }
}

public class ArrangeTests
{
    static readonly (double W, double H)[] FourTags = { (60, 20), (60, 20), (60, 20), (60, 20) };

    [Fact]
    public void TwoColumns_MakeATwoByTwoArray()
    {
        var r = ArrayLayout.Place(FourTags, new ArrangeSettings { Columns = 2, GapX = 5, GapY = 5 });

        Assert.Equal(2, r.Columns);
        Assert.Equal(2, r.Rows);
        Assert.Equal(125, r.TotalWidth);
        Assert.Equal(45, r.TotalHeight);
        Assert.Equal(4, r.Centers.Select(c => (c.X, c.Y)).Distinct().Count());
    }

    [Fact]
    public void Array_IsCenteredOnThePlate()
    {
        var r = ArrayLayout.Place(FourTags, new ArrangeSettings { Columns = 2, GapX = 5, GapY = 5 });

        Assert.Equal(0, r.Centers.Average(c => c.X), 6);
        Assert.Equal(0, r.Centers.Average(c => c.Y), 6);
    }

    [Fact]
    public void FirstName_IsTopRight_ForHebrewReadingOrder()
    {
        var r = ArrayLayout.Place(FourTags, new ArrangeSettings { Columns = 2, GapX = 5, GapY = 5 });

        Assert.True(r.Centers[0].X > r.Centers[1].X);
        Assert.True(r.Centers[0].Y > r.Centers[2].Y);
    }

    [Fact]
    public void AutoColumns_FitAsManyAsThePlateAllows()
    {
        var r = ArrayLayout.Place(FourTags, new ArrangeSettings { Columns = 0, GapX = 5, GapY = 5, PlateWidth = 130, PlateHeight = 256 });

        Assert.Equal(2, r.Columns);
        Assert.True(r.FitsPlate);
    }

    [Fact]
    public void OneRow_PutsEveryNameInASingleRow_EvenIfItOverflowsThePlate()
    {
        var r = ArrayLayout.Place(FourTags, new ArrangeSettings { Shape = ArrangeShape.OneRow, PlateWidth = 100 });

        Assert.Equal(4, r.Columns);
        Assert.Equal(1, r.Rows);
        Assert.False(r.FitsPlate);
    }

    [Fact]
    public void OneColumn_PutsEveryNameInASingleColumn()
    {
        var r = ArrayLayout.Place(FourTags, new ArrangeSettings { Shape = ArrangeShape.OneColumn });

        Assert.Equal(1, r.Columns);
        Assert.Equal(4, r.Rows);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(5, 3, 2)]
    [InlineData(7, 3, 3)]
    [InlineData(9, 3, 3)]
    [InlineData(10, 4, 3)]
    public void Square_IsAsCloseToSquareAsTheCountAllows(int count, int columns, int rows)
    {
        var sizes = Enumerable.Repeat((W: 60.0, H: 20.0), count).ToList();

        var r = ArrayLayout.Place(sizes, new ArrangeSettings { Shape = ArrangeShape.Square });

        Assert.Equal(columns, r.Columns);
        Assert.Equal(rows, r.Rows);
    }

    [Fact]
    public void AShape_FollowsTheNames_WhenMoreAreAdded()
    {
        var settings = new ArrangeSettings { Shape = ArrangeShape.OneRow };

        Assert.Equal(2, ArrayLayout.Place(FourTags.Take(2).ToList(), settings).Columns);
        Assert.Equal(4, ArrayLayout.Place(FourTags, settings).Columns);
    }

    [Fact]
    public void SettingsSavedBeforeShapesExisted_KeepTheirColumns()
    {
        var old = System.Text.Json.JsonSerializer.Deserialize<ArrangeSettings>("""{"Columns":3,"GapX":5,"GapY":5,"PlateWidth":256,"PlateHeight":256}""")!;

        Assert.Equal(ArrangeOrder.AsListed, old.Order);
        Assert.Equal(3, ArrayLayout.Place(FourTags, old).Columns);
    }

    [Fact]
    public void AsListed_KeepsTheListOrder()
    {
        var order = ArrayLayout.Order(new[] { ("נויה", 40.0), ("אבי", 30.0), ("ליב", 50.0) }, ArrangeOrder.AsListed);

        Assert.Equal(new[] { 0, 1, 2 }, order);
    }

    [Fact]
    public void Alphabetical_FollowsTheHebrewAlphabet()
    {
        var order = ArrayLayout.Order(new[] { ("נויה", 40.0), ("אבי", 30.0), ("ליב", 50.0), ("תמר", 45.0), ("בן", 20.0) }, ArrangeOrder.Alphabetical);

        Assert.Equal(new[] { 1, 4, 2, 0, 3 }, order);
    }

    [Fact]
    public void BySize_PutsTheWidestFirst_AndKeepsTiesInListOrder()
    {
        var order = ArrayLayout.Order(new[] { ("א", 30.0), ("ב", 50.0), ("ג", 30.0), ("ד", 40.0) }, ArrangeOrder.BySize);

        Assert.Equal(new[] { 1, 3, 0, 2 }, order);
    }

    [Fact]
    public void Builder_AppliesTheOrder_SoTheFirstAlphabeticalNameIsTopRight()
    {
        var item = new KeychainItem();
        var built = new NameTagGenerator.Output.TagBuilder().Build(new[] { "נויה", "אבי", "ליב" }, item, ItemSettings.DefaultsFor(item),
            new ArrangeSettings { Shape = ArrangeShape.OneRow, Order = ArrangeOrder.Alphabetical }, CancellationToken.None);

        Assert.Equal(new[] { "אבי", "ליב", "נויה" }, built.Tags.Select(t => t.Name));
        Assert.True(built.Tags[0].X > built.Tags[1].X && built.Tags[1].X > built.Tags[2].X);
    }

    [Fact]
    public void TooManyTags_AreReportedAsNotFitting()
    {
        var r = ArrayLayout.Place(FourTags, new ArrangeSettings { Columns = 4, GapX = 5, GapY = 5, PlateWidth = 100, PlateHeight = 100 });

        Assert.False(r.FitsPlate);
    }
}

public class ThreeMfTests
{
    [Fact]
    public void ThreeMf_HoldsBaseAndLettersAsSeparateParts()
    {
        var a = new Mesh();
        a.Extrude(new Paths64 { Poly.Rect(0, 0, 10, 10) }, 0, Poly.U(3));
        var b = new Mesh();
        b.Extrude(new Paths64 { Poly.Rect(2, 2, 8, 8) }, Poly.U(3), Poly.U(5));
        using var ms = new MemoryStream();

        ThreeMfWriter.Write(ms, "tag", a, b);

        ms.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(ms);
        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("_rels/.rels"));
        using var reader = new StreamReader(zip.GetEntry("3D/3dmodel.model")!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("<components>", xml);
        Assert.Contains("unit=\"millimeter\"", xml);
    }
}
