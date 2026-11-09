using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Output;
using NameTagGenerator.Pictures;
using NameTagGenerator.Text;
using Xunit;

namespace NameTagGenerator.Tests;

public class SvgReaderTests
{
    static string Svg(string body) => $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">{body}</svg>";

    static (double Width, double Height, double Area) Measure(Paths64 paths)
    {
        var box = Poly.Bounds(paths);
        var perUnit = (double)box.Width;
        return (box.Width, box.Height, Math.Abs(Clipper.Area(paths)) / (perUnit * perUnit));
    }

    [Fact]
    public void Rectangle_KeepsItsProportions()
    {
        var (width, height, area) = Measure(SvgReader.Read(Svg("<rect x='10' y='10' width='40' height='20'/>"))!);
        Assert.Equal(2.0, width / height, 2);
        Assert.Equal(0.5, area, 2);
    }

    [Fact]
    public void Circle_HasTheAreaOfACircle() =>
        Assert.InRange(Measure(SvgReader.Read(Svg("<circle cx='50' cy='50' r='40'/>"))!).Area, Math.PI / 4 * 0.99, Math.PI / 4);

    [Fact]
    public void RoundedRectangle_LosesItsCorners()
    {
        var (_, _, area) = Measure(SvgReader.Read(Svg("<rect width='100' height='100' rx='50'/>"))!);
        Assert.InRange(area, Math.PI / 4 * 0.99, Math.PI / 4);
    }

    [Fact]
    public void ShapeNearTheTopOfTheFile_EndsUpHigher()
    {
        var both = SvgReader.Read(Svg("<rect x='0' y='0' width='100' height='10'/><rect x='0' y='90' width='10' height='10'/>"))!;
        var box = Poly.Bounds(both);
        var top = Clipper.Intersect(both, new Paths64 { new Path64 { new(box.MinX, box.MaxY - 1000), new(box.MaxX, box.MaxY - 1000), new(box.MaxX, box.MaxY), new(box.MinX, box.MaxY) } }, FillRule.NonZero);
        Assert.Equal(box.Width, Poly.Bounds(top).Width);
    }

    [Theory]
    [InlineData("M10 10 H90 V90 H10 Z", 1.0)]
    [InlineData("M10,10h80v80h-80z", 1.0)]
    [InlineData("M10 10 90 10 90 90 10 90z", 1.0)]
    [InlineData("M10 10L90 10L10 90Z", 0.5)]
    [InlineData("M10 90Q50 -70 90 90Z", 0.6667)]
    [InlineData("M10 50 A40 40 0 1 1 90 50 A40 40 0 1 1 10 50Z", 0.7854)]
    [InlineData("M10 50a40 40 0 1190 0 40 40 0 11-80 0z", -1)]
    public void PathCommands_AreFollowed(string d, double expectedShare)
    {
        var paths = SvgReader.Read(Svg($"<path d='{d}'/>"));
        if (expectedShare < 0) { Assert.NotNull(paths); return; }
        var box = Poly.Bounds(paths!);
        var share = Math.Abs(Clipper.Area(paths!)) / ((double)box.Width * box.Height);
        Assert.Equal(expectedShare, share, 1);
    }

    [Fact]
    public void CompactNumbers_AreRead()
    {
        var paths = SvgReader.Read(Svg("<path d='M.5.5h1.5-.5v1e1z'/>"));
        Assert.NotNull(paths);
    }

    [Fact]
    public void CubicCurves_AreFlattenedSmoothly()
    {
        var paths = SvgReader.Read(Svg("<path d='M10 50C10 20 90 20 90 50S10 80 10 50Z'/>"))!;
        Assert.True(paths.Sum(p => p.Count) > 12);
    }

    [Fact]
    public void Transforms_Apply()
    {
        var plain = Measure(SvgReader.Read(Svg("<rect width='40' height='20'/>"))!);
        var scaled = Measure(SvgReader.Read(Svg("<g transform='scale(1 4)'><rect width='40' height='20'/></g>"))!);
        var turned = Measure(SvgReader.Read(Svg("<rect width='40' height='20' transform='rotate(90)'/>"))!);
        Assert.Equal(2.0, plain.Width / plain.Height, 2);
        Assert.Equal(0.5, scaled.Width / scaled.Height, 2);
        Assert.Equal(0.5, turned.Width / turned.Height, 2);
    }

    [Fact]
    public void NoFill_IsNotDrawn() =>
        Assert.Null(SvgReader.Read(Svg("<rect width='40' height='20' fill='none'/>")));

    [Fact]
    public void WhiteShape_CutsOutWhatIsBeneathIt()
    {
        var area = Measure(SvgReader.Read(Svg("<rect width='100' height='100'/><rect x='25' y='25' width='50' height='50' fill='#ffffff'/>"))!).Area;
        Assert.Equal(0.75, area, 2);
    }

    [Fact]
    public void WhiteBackground_LeavesOnlyTheDrawing()
    {
        var paths = SvgReader.Read(Svg("<rect width='100' height='100' fill='white'/><rect x='40' y='40' width='20' height='20' fill='#c00'/>"))!;
        var (width, height, area) = Measure(paths);
        // Only the small square is left: square, and filling its own box.
        Assert.Equal(1.0, width / height, 2);
        Assert.Equal(1.0, area, 2);
        Assert.Single(paths);
    }

    [Fact]
    public void Stroke_HasItsWidth()
    {
        var paths = SvgReader.Read(Svg("<line x1='10' y1='50' x2='90' y2='50' stroke='black' stroke-width='10'/>"))!;
        var box = Poly.Bounds(paths);
        Assert.Equal(9.0, (double)box.Width / box.Height, 0);
    }

    [Fact]
    public void EvenOddRule_LeavesAHole()
    {
        const string twoSquares = "M0 0H100V100H0Z M25 25H75V75H25Z";
        Assert.Equal(1.0, Measure(SvgReader.Read(Svg($"<path d='{twoSquares}'/>"))!).Area, 2);
        Assert.Equal(0.75, Measure(SvgReader.Read(Svg($"<path d='{twoSquares}' fill-rule='evenodd'/>"))!).Area, 2);
    }

    [Fact]
    public void ClassStyles_AreRead()
    {
        var area = Measure(SvgReader.Read(Svg("<style>.hole{fill:#FFF}</style><rect width='100' height='100'/><rect class='hole' x='25' y='25' width='50' height='50'/>"))!).Area;
        Assert.Equal(0.75, area, 2);
    }

    [Fact]
    public void StyleAttribute_WinsOverFillAttribute() =>
        Assert.Null(SvgReader.Read(Svg("<rect width='40' height='20' fill='black' style='fill:none'/>")));

    [Fact]
    public void InheritedFill_ComesFromTheGroup() =>
        Assert.Null(SvgReader.Read(Svg("<g fill='none'><rect width='40' height='20'/></g>")));

    [Fact]
    public void Definitions_AreNotDrawn() =>
        Assert.Null(SvgReader.Read(Svg("<defs><rect width='40' height='20'/></defs>")));

    [Fact]
    public void HiddenShapes_AreNotDrawn() =>
        Assert.Null(SvgReader.Read(Svg("<rect width='40' height='20' display='none'/>")));

    [Theory]
    [InlineData("not xml at all")]
    [InlineData("<svg><rect")]
    [InlineData("<html></html>")]
    [InlineData("")]
    public void DamagedFile_GivesNull(string text) => Assert.Null(SvgReader.Read(text));

    [Fact]
    public void FileWithEntityDefinitions_IsRefused() =>
        Assert.Null(SvgReader.Read("<!DOCTYPE svg [<!ENTITY a \"aaaa\">]><svg xmlns=\"http://www.w3.org/2000/svg\"><rect width='4' height='2'/></svg>"));

    static void FinishesQuickly(string file)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        SvgReader.Read(file);
        Assert.True(clock.Elapsed.TotalSeconds < 5, $"took {clock.Elapsed.TotalSeconds:0.0} s");
    }

    [Theory]
    [InlineData("1e300")]
    [InlineData("1e999")]
    public void Hostile_HugeStroke_Finishes(string width) =>
        FinishesQuickly(Svg($"<line x1='10' y1='50' x2='90' y2='50' stroke='black' stroke-width='{width}'/>"));

    [Fact]
    public void Hostile_InfiniteScale_GivesNull() =>
        Assert.Null(SvgReader.Read(Svg("<rect width='40' height='20' transform='scale(1e999)'/>")));

    [Fact]
    public void Hostile_CoordinatesTooLargeToMeasure_GiveNull() =>
        Assert.Null(SvgReader.Read(Svg("<path d='M1e308 0L-1e308 0L0 1e308Z'/>")));

    [Fact]
    public void Hostile_VeryDeepNesting_GivesNull()
    {
        const int depth = 200_000;
        var file = "<svg xmlns=\"http://www.w3.org/2000/svg\">" + string.Concat(Enumerable.Repeat("<g>", depth)) + "<rect width='4' height='2'/>" + string.Concat(Enumerable.Repeat("</g>", depth)) + "</svg>";
        Assert.Null(SvgReader.Read(file));
    }

    [Fact]
    public void Hostile_EnormousPath_IsRefusedQuickly()
    {
        var file = Svg("<path d='M0 0" + string.Concat(Enumerable.Repeat("C1 1 2 2 3 3", 400_000)) + "Z'/>");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        Assert.Null(SvgReader.Read(file));
        Assert.True(clock.Elapsed.TotalSeconds < 3, $"took {clock.Elapsed.TotalSeconds:0.0} s");
    }

    [Fact]
    public void Hostile_ThousandsOfShapes_Finish() =>
        FinishesQuickly(Svg(string.Concat(Enumerable.Range(0, 20_000).Select(i => $"<rect x='{i % 200}' y='{i / 200}' width='0.5' height='0.5'/>"))));

    [Fact]
    public void Hostile_StyleWithoutBraces_Finishes() =>
        FinishesQuickly(Svg("<style>" + new string('a', 2_000_000) + "</style><rect width='4' height='2'/>"));

    [Fact]
    public void Hostile_CommentsThatNeverEnd_Finish() =>
        FinishesQuickly(Svg("<style>" + string.Concat(Enumerable.Repeat("/* ", 500_000)) + "</style><rect width='4' height='2'/>"));

    [Fact]
    public void Hostile_LongTransform_Finishes() =>
        FinishesQuickly(Svg("<rect width='4' height='2' transform='" + new string('a', 2_000_000) + "'/>"));

    [Fact]
    public void AlternatingColours_StillPaintInOrder()
    {
        var area = 0.0;
        var paths = SvgReader.Read(Svg("<rect width='100' height='100'/><rect x='25' y='25' width='50' height='50' fill='#fff'/><rect x='40' y='40' width='20' height='20'/>"))!;
        var box = Poly.Bounds(paths);
        area = Math.Abs(Clipper.Area(paths)) / ((double)box.Width * box.Height);
        Assert.Equal(0.75 + 0.04, area, 2);
    }

    [Fact]
    public void BrokenPath_KeepsWhatWasDrawnBeforeTheError()
    {
        Assert.NotNull(SvgReader.Read(Svg("<path d='M10 10H90V90H10Z M5 x'/>")));
    }
}

public class MaskTracerTests
{
    const byte White = 255, Black = 0;

    static byte[] Image(int size, Func<int, int, byte> pixel)
    {
        var gray = new byte[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                gray[y * size + x] = pixel(x, y);
        return gray;
    }

    [Fact]
    public void DarkSquare_BecomesOneOutlineOfItsSize()
    {
        var paths = MaskTracer.Trace(Image(100, (x, y) => x is >= 20 and < 60 && y is >= 30 and < 90 ? Black : White), 100, 100, 128, swap: false);
        Assert.Single(paths);
        var box = Poly.Bounds(paths);
        Assert.Equal(40.0 / 60.0, (double)box.Width / box.Height, 2);
    }

    [Fact]
    public void Ring_KeepsItsHole()
    {
        var paths = MaskTracer.Trace(Image(100, (x, y) => x is >= 10 and < 90 && y is >= 10 and < 90 && !(x is >= 40 and < 60 && y is >= 40 and < 60) ? Black : White), 100, 100, 128, false);
        var box = Poly.Bounds(paths);
        Assert.Equal((80.0 * 80 - 20 * 20) / (80 * 80), Math.Abs(Clipper.Area(paths)) / ((double)box.Width * box.Height), 2);
    }

    [Fact]
    public void Level_DecidesWhatCountsAsDark()
    {
        var image = Image(100, (x, _) => x < 50 ? (byte)100 : (byte)200);
        Assert.Empty(MaskTracer.Trace(image, 100, 100, 50, false));
        Assert.Single(MaskTracer.Trace(image, 100, 100, 150, false));
    }

    [Fact]
    public void Swap_KeepsTheLightPartInstead()
    {
        var image = Image(100, (x, _) => x < 25 ? Black : White);
        var dark = MaskTracer.Trace(image, 100, 100, 128, false);
        var light = MaskTracer.Trace(image, 100, 100, 128, true);
        Assert.Equal(3.0, (double)Poly.Bounds(light).Width / Poly.Bounds(dark).Width, 1);
    }

    [Fact]
    public void TopOfTheImage_EndsUpHigher()
    {
        var paths = MaskTracer.Trace(Image(100, (x, y) => y < 10 || (y >= 90 && x < 10) ? Black : White), 100, 100, 128, false);
        var box = Poly.Bounds(paths);
        var top = Clipper.Intersect(paths, new Paths64 { new Path64 { new(box.MinX, box.MaxY - 100), new(box.MaxX, box.MaxY - 100), new(box.MaxX, box.MaxY), new(box.MinX, box.MaxY) } }, FillRule.NonZero);
        Assert.Equal(box.Width, Poly.Bounds(top).Width);
    }

    [Fact]
    public void AllWhite_GivesNothing() => Assert.Empty(MaskTracer.Trace(Image(50, (_, _) => White), 50, 50, 128, false));

    [Fact]
    public void Diagonals_AreNotLeftAsStairs()
    {
        var paths = MaskTracer.Trace(Image(200, (x, y) => x + y < 200 ? Black : White), 200, 200, 128, false);
        Assert.True(paths.Sum(p => p.Count) < 12, $"{paths.Sum(p => p.Count)} points");
    }
}

public class NamePictureTests
{
    static Paths64 Square(double size = 100) => new() { Poly.Rect(0, 0, size, size) };

    static TextShape Name(string text = "Noa") => TextOutliner.Build(text, BundledFonts.Get("Rubik"), false, 18, "-", false);

    [Fact]
    public void Picture_IsMadeTheChosenHeight()
    {
        var picture = NamePicture.Fit(Square(), 14, placeRight: true, 2, "a");
        Assert.Equal(14, Poly.Mm(Poly.Bounds(picture.Outlines).Height), 1);
    }

    [Fact]
    public void Right_PutsThePictureRightOfTheName_AtTheGap()
    {
        var name = Name();
        var box = Poly.Bounds(name.Letters);
        var whole = Poly.Bounds(NamePicture.Fit(Square(), 14, true, 2, "a").AddTo(name).Letters);
        Assert.Equal(box.MinX, whole.MinX);
        Assert.Equal(Poly.Mm(box.MaxX) + 2 + 14, Poly.Mm(whole.MaxX), 1);
    }

    [Fact]
    public void Left_PutsThePictureLeftOfTheName_AtTheGap()
    {
        var name = Name();
        var box = Poly.Bounds(name.Letters);
        var whole = Poly.Bounds(NamePicture.Fit(Square(), 14, false, 2, "a").AddTo(name).Letters);
        Assert.Equal(box.MaxX, whole.MaxX);
        Assert.Equal(Poly.Mm(box.MinX) - 2 - 14, Poly.Mm(whole.MinX), 1);
    }

    [Fact]
    public void Picture_IsCentredOnTheName()
    {
        var name = Name();
        var box = Poly.Bounds(name.Letters);
        var picture = NamePicture.Fit(Square(), 30, true, 2, "a");
        var whole = Poly.Bounds(picture.AddTo(name).Letters);
        Assert.Equal(Poly.Mm(box.MinY + box.MaxY) / 2, Poly.Mm(whole.MinY + whole.MaxY) / 2, 1);
    }

    [Fact]
    public void ThinParts_AreDroppedAndReported()
    {
        var withHair = new Paths64 { Poly.Rect(0, 0, 100, 100), Poly.Rect(100, 49, 300, 51) };
        var picture = NamePicture.Fit(withHair, 14, true, 2, "a");
        Assert.True(picture.ThinShareRemoved > 0);
        Assert.InRange(Poly.Mm(Poly.Bounds(picture.Outlines).Width), 13, 15.5);
    }

    [Fact]
    public void SolidPicture_ReportsNothingRemoved() =>
        Assert.True(NamePicture.Fit(Square(), 14, true, 2, "a").ThinShareRemoved < 0.02);

    [Fact]
    public void PictureOfOnlyThinLines_IsEmpty() =>
        Assert.Empty(NamePicture.Fit(new Paths64 { Poly.Rect(0, 0, 200, 1), Poly.Rect(0, 199, 200, 200) }, 14, true, 2, "a").Outlines);

    public static IEnumerable<object[]> Items => ItemRegistry.All.Select(i => new object[] { i.Id });

    [Theory]
    [MemberData(nameof(Items))]
    public void PictureBesideName_BuildsClosedAndInOnePiece(string id)
    {
        var item = ItemRegistry.All.First(i => i.Id == id);
        var settings = ItemSettings.DefaultsFor(item);
        var paw = SvgReader.Read("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 64 64'><ellipse cx='32' cy='44' rx='16' ry='13'/><ellipse cx='13' cy='28' rx='6' ry='9'/><ellipse cx='51' cy='28' rx='6' ry='9'/><ellipse cx='25' cy='14' rx='6' ry='9'/><ellipse cx='39' cy='14' rx='6' ry='9'/></svg>")!;
        foreach (var name in new[] { "Noa", "דנה" })
        {
            var text = TextOutliner.Build(name, BundledFonts.Get("Rubik"), false, settings[Keys.FontSize], "-", false);
            var model = item.Build(NamePicture.Fit(paw, settings[Keys.FontSize], true, 2, "a").AddTo(text), settings);
            Assert.True(model.Base.IsManifold(), $"base: {model.Base.DescribeOpenEdges()}");
            Assert.True(model.Letters.IsManifold(), $"letters: {model.Letters.DescribeOpenEdges()}");
            Assert.Equal(1, model.LooseParts);
        }
    }

    [Fact]
    public void NothingToTrace_GivesAnEmptyPicture_AndLeavesTheNameAlone()
    {
        var picture = NamePicture.Fit(new Paths64(), 14, true, 2, "a");
        Assert.Empty(picture.Outlines);
        var name = Name();
        Assert.Same(name, picture.AddTo(name));
    }

    [Fact]
    public void SvgWithByteOrderMark_IsRead() =>
        Assert.NotNull(PictureSource.FromSvg("﻿<svg xmlns='http://www.w3.org/2000/svg'><rect width='4' height='2'/></svg>"));

    [Fact]
    public void Pixels_UseTheLevel()
    {
        var gray = Enumerable.Repeat((byte)100, 100).ToArray();
        var source = PictureSource.FromPixels(gray, 10, 10);
        Assert.False(source.IsDrawing);
        Assert.Empty(source.Outlines(10, false));
        Assert.NotEmpty(source.Outlines(90, false));
    }

    [Theory]
    [InlineData("{\"Picture\":null}", 14, 2, 50)]
    [InlineData("{\"Picture\":{\"HeightMm\":-5,\"GapMm\":9999,\"LevelPercent\":500}}", 5, 50, 99)]
    public void SavedPictureSettings_AreRepaired(string json, double height, double gap, int level)
    {
        var picture = SiteState.FromJson(json, _ => true).Picture;
        Assert.Equal(height, picture.HeightMm);
        Assert.Equal(gap, picture.GapMm);
        Assert.Equal(level, picture.LevelPercent);
    }

    [Fact]
    public void Builder_RemakesTags_WhenThePictureChanges()
    {
        var item = ItemRegistry.All.First();
        var settings = ItemSettings.DefaultsFor(item);
        var builder = new TagBuilder();
        var names = new[] { "Noa" };
        var plain = builder.Build(names, item, settings, new ArrangeSettings(), CancellationToken.None);
        var pictured = builder.Build(names, item, settings, new ArrangeSettings(), CancellationToken.None, NamePicture.Fit(Square(), 14, true, 2, "one"));
        var bigger = builder.Build(names, item, settings, new ArrangeSettings(), CancellationToken.None, NamePicture.Fit(Square(), 18, true, 2, "two"));
        Assert.True(pictured.VolumeMm3 > plain.VolumeMm3);
        Assert.True(bigger.VolumeMm3 > pictured.VolumeMm3);
    }
}
