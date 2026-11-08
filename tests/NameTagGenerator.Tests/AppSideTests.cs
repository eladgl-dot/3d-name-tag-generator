using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Text;
using NameTagGenerator.Views;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace NameTagGenerator.Tests;

/// <summary>The parts only the Windows app has: its font list and its 3D preview.</summary>
public class AppSideTests
{
    static (ItemModel Model, ItemSettings S) Build(IItemType item, string name)
    {
        var s = ItemSettings.DefaultsFor(item);
        return (item.Build(TextOutliner.Build(name, FontCatalog.Resolve("Gan CLM"), false, s[Keys.FontSize], "-", false), s), s);
    }

    [Theory]
    [InlineData("Gan CLM")]
    [InlineData("MigdalHaemeq")]
    [InlineData("Noot")]
    [InlineData("Suez One")]
    public void BuiltInFont_LoadsFromInsideTheApp_AndDrawsHebrew(string fontName)
    {
        var builtIn = TextOutliner.Build("שלום", FontCatalog.Resolve(fontName), false, 18, "-", false);
        var family = FontCatalog.WindowsFamily(fontName);

        Assert.NotEmpty(builtIn.Letters);
        // It must be the bundled file itself, not a font Windows substituted.
        var typeface = new System.Windows.Media.Typeface(family, System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
        Assert.True(typeface.TryGetGlyphTypeface(out var glyphs));
        Assert.Contains(fontName, glyphs.FamilyNames.Values);
        Assert.Contains(FontCatalog.AppDataFolder.Replace('\\', '/'), Uri.UnescapeDataString(glyphs.FontUri.AbsoluteUri), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Rubik", FontCatalog.BuiltInBothGroup)]
    [InlineData("Alef", FontCatalog.BuiltInBothGroup)]
    [InlineData("Gan CLM", FontCatalog.BuiltInHebrewGroup)]
    [InlineData("Pacifico", FontCatalog.BuiltInEnglishGroup)]
    [InlineData("Bebas Neue", FontCatalog.BuiltInEnglishGroup)]
    public void BuiltInFonts_AreGroupedByTheLettersTheyHave(string font, string group)
    {
        Assert.Equal(group, FontCatalog.ListAll().Single(f => f.Name == font).Group);
    }

    [Fact]
    public void BuiltInFonts_ComeFirst_GroupedTogether()
    {
        var groups = FontCatalog.ListAll().Select(f => f.Group).Distinct().ToList();
        Assert.Equal(new[] { FontCatalog.BuiltInBothGroup, FontCatalog.BuiltInHebrewGroup, FontCatalog.BuiltInEnglishGroup }, groups.Take(3));
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
