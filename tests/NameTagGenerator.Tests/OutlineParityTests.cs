using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Tests.Reference;
using NameTagGenerator.Text;

namespace NameTagGenerator.Tests;

/// <summary>The shared outliner must draw what Windows drew: same sizes within 0.1 mm, same shapes.</summary>
public class OutlineParityTests
{
    const double ToleranceMm = 0.1;
    const double SizeMm = 18;

    static IEnumerable<string> Fonts() => FontCatalog.BuiltIn.OrderBy(n => n).Append("Arial");

    static IEnumerable<string> NamesFor(string font)
    {
        var loaded = FontCatalog.Resolve(font);
        if (loaded.HasHebrew) { yield return "ליב"; yield return "נויה"; yield return "שלום עליכם"; }
        if (loaded.HasEnglish) { yield return "Noa"; yield return "Anna Maria"; }
    }

    static string? Compare(string font, string name, bool bold, bool hollow)
    {
        var expected = WindowsOutliner.Build(name, FontCatalog.WindowsFamily(font), bold, SizeMm, "-", hollow);
        var actual = TextOutliner.Build(name, FontCatalog.Resolve(font), bold, SizeMm, "-", hollow);

        var e = Poly.Bounds(Poly.Union(expected.Letters, expected.Connector));
        var a = Poly.Bounds(Poly.Union(actual.Letters, actual.Connector));
        var dw = Poly.Mm(a.Width - e.Width);
        var dh = Poly.Mm(a.Height - e.Height);

        var moved = Poly.Translate(actual.Letters, e.MinX - a.MinX, e.MinY - a.MinY);
        var different = Clipper.Area(Clipper.Xor(moved, expected.Letters, FillRule.NonZero));
        var share = different / Clipper.Area(expected.Letters);

        // Righteous: the new reader applies a kerning pair Windows skips, about 0.2 mm tighter.
        var sizeLimit = font == "Righteous" ? 0.25 : ToleranceMm;
        // Thickening is the same size as Windows' but not the same corners, so bold shapes differ a little more.
        var shapeLimit = font == "Righteous" ? 0.06 : bold ? 0.06 : 0.02;

        return Math.Abs(dw) <= sizeLimit && Math.Abs(dh) <= sizeLimit && share <= shapeLimit
            ? null
            : $"{font,-14} bold={bold,-5} hollow={hollow,-5} {name,-12} width {dw:+0.000;-0.000} height {dh:+0.000;-0.000} shape differs {share:P1}";
    }

    static void AssertAllMatch(bool bold, bool hollow)
    {
        var failures = (from font in Fonts() from name in NamesFor(font) select Compare(font, name, bold, hollow))
            .Where(f => f is not null).ToList();
        Assert.True(failures.Count == 0, $"{failures.Count} differ:\n" + string.Join("\n", failures));
    }

    [Fact] public void Regular_MatchesWindows() => AssertAllMatch(bold: false, hollow: false);
    [Fact] public void HollowConnector_MatchesWindows() => AssertAllMatch(bold: false, hollow: true);
    [Fact] public void Bold_MatchesWindows() => AssertAllMatch(bold: true, hollow: false);

    [Fact]
    public void EveryItem_KeepsTheVolumeItHadWithWindowsOutlines()
    {
        var failures = new List<string>();
        foreach (var item in ItemRegistry.All)
        {
            var s = ItemSettings.DefaultsFor(item);
            foreach (var (font, name) in new[] { ("Gan CLM", "ליב"), ("Rubik", "נויה"), ("Suez One", "שלום עליכם"), ("Heebo", "Noa") })
            {
                var size = s[Keys.FontSize];
                var before = item.Build(WindowsOutliner.Build(name, FontCatalog.WindowsFamily(font), false, size, "-", false), s);
                var after = item.Build(TextOutliner.Build(name, FontCatalog.Resolve(font), false, size, "-", false), s);
                var change = (after.Base.VolumeMm3 + after.Letters.VolumeMm3) / (before.Base.VolumeMm3 + before.Letters.VolumeMm3) - 1;
                if (Math.Abs(change) > 0.01) failures.Add($"{item.Id} {font} {name}: {change:P2}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void Arial_HasARealBoldFace_BundledFontsDoNot()
    {
        Assert.False(FontCatalog.Resolve("Arial").SimulatesBold);
        Assert.True(FontCatalog.Resolve("Rubik").SimulatesBold);
    }

    [Fact]
    public void UnknownFont_UsesTheBuiltInFallback()
    {
        Assert.Same(BundledFonts.Fallback, FontCatalog.Resolve("No Such Font Anywhere"));
    }

    [Theory]
    [InlineData("Pacifico", "שלום")]   // English-only font, Hebrew name
    [InlineData("Gan CLM", "Noa")]     // Hebrew-only font, English name
    public void LettersTheFontLacks_ComeFromTheFallback_NotEmptyBoxes(string font, string name)
    {
        var fallback = TextOutliner.Build(name, BundledFonts.Fallback, false, SizeMm, "-", false);
        var actual = TextOutliner.Build(name, FontCatalog.Resolve(font), false, SizeMm, "-", false);

        Assert.NotEmpty(actual.Letters);
        Assert.InRange(Clipper.Area(actual.Letters) / Clipper.Area(fallback.Letters), 0.999, 1.001);
    }
}
