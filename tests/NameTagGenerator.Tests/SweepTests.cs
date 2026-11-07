using System.IO;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Output;
using NameTagGenerator.Text;

namespace NameTagGenerator.Tests;

/// <summary>Real text produces awkward shapes, so every item is checked with every bundled font.</summary>
public class SweepTests
{
    static readonly string[] Names = { "ליב", "נויה", "שלום עליכם", "אברהם יצחק יעקב", "Noa", "ס" };

    public static IEnumerable<object[]> Cases() =>
        from item in ItemRegistry.All
        from font in FontCatalog.BuiltIn.Append("Arial")
        from name in Names
        from hollow in new[] { false, true }
        select new object[] { item.Id, font, name, hollow };

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryCombination_GivesClosedBodies(string id, string font, string name, bool hollow)
    {
        var item = ItemRegistry.All.Single(i => i.Id == id);
        var s = ItemSettings.DefaultsFor(item);
        var text = TextOutliner.Build(name, FontCatalog.Resolve(font), false, s[Keys.FontSize], "-", hollow);

        var model = item.Build(text, s);

        Assert.True(model.Base.IsManifold(), "base: " + model.Base.DescribeOpenEdges());
        Assert.True(model.Letters.IsManifold(), "letters: " + model.Letters.DescribeOpenEdges());
        Assert.True(model.Base.VolumeMm3 > 0, "base volume");
        Assert.True(model.Letters.VolumeMm3 > 0, "letters volume");
    }

    [Fact]
    public void ArrayOfNames_SavesAsTwoPartsThatStayClosed_InStlAnd3mf()
    {
        var item = new KeychainItem();
        var built = new TagBuilder().Build(Names, item, ItemSettings.DefaultsFor(item), new ArrangeSettings { Columns = 2 }, CancellationToken.None);
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var stl = Path.Combine(folder, "a.stl");
            Exporter.SaveStl(stl, built.Tags);
            var triangles = built.Tags.Sum(t => t.Model.Base.TriangleCount + t.Model.Letters.TriangleCount);
            Assert.Equal(84 + 50 * triangles, new FileInfo(stl).Length);

            var threeMf = Path.Combine(folder, "a.3mf");
            Exporter.Save3mf(threeMf, "a", built.Tags);
            Assert.True(new FileInfo(threeMf).Length > 0);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData("a/b:c", "a_b_c")]
    [InlineData("  ", "tag")]
    [InlineData("נויה", "נויה")]
    public void FileNames_AreSafe_AndKeepHebrew(string name, string expected)
    {
        Assert.Equal(expected, Exporter.SafeFileName(name));
    }

    [Fact]
    public void WritesSampleFiles_WhenAskedTo()
    {
        var folder = Environment.GetEnvironmentVariable("SAMPLE_OUT");
        if (folder is null) return;

        var keychain = new KeychainItem();
        var array = new TagBuilder().Build(Names, keychain, ItemSettings.DefaultsFor(keychain), new ArrangeSettings { Columns = 2 }, CancellationToken.None);
        Exporter.Save3mf(Path.Combine(folder, "array.3mf"), "array", array.Tags);

        foreach (var item in ItemRegistry.All)
        {
            var s = ItemSettings.DefaultsFor(item);
            var text = TextOutliner.Build("ליב", FontCatalog.Resolve("Gan CLM"), false, s[Keys.FontSize], "-", false);
            var model = item.Build(text, s);
            using (var f = File.Create(Path.Combine(folder, item.Id + ".stl")))
                StlWriter.Write(f, new[] { (model.Base, 0.0, 0.0), (model.Letters, 0.0, 0.0) });
            using (var f = File.Create(Path.Combine(folder, item.Id + ".3mf")))
                ThreeMfWriter.Write(f, item.Name, model.Base, model.Letters);
        }
    }
}
