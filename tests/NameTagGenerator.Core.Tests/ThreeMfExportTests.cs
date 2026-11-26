using System.IO.Compression;
using System.Text.RegularExpressions;
using NameTagGenerator.Items;
using NameTagGenerator.Output;

namespace NameTagGenerator.Tests;

/// <summary>3MF export: one object or separate objects, back text as a third filament, one Bambu plate per bed.</summary>
public class ThreeMfExportTests
{
    static readonly IItemType Candle = ItemRegistry.All.First(i => i.Id == "candle");

    static BuildResult BuildCandles(int count, string back = "", ArrangeShape shape = ArrangeShape.Columns)
    {
        var s = ItemSettings.DefaultsFor(Candle);
        s.Texts[Keys.BackText] = back;
        var names = Enumerable.Range(0, count).Select(i => "נוי" + new string('ה', i % 3) + i).ToList();
        return new TagBuilder().Build(names, Candle, s, new ArrangeSettings { Shape = shape }, CancellationToken.None);
    }

    static (string Model, string Config) Write(BuildResult r, bool separate)
    {
        using var ms = new MemoryStream();
        Exporter.Write3mf(ms, "tags", r.Tags, separate, new ArrangeSettings());
        ms.Position = 0;
        using var zip = new ZipArchive(ms);
        string Read(string path) { using var sr = new StreamReader(zip.GetEntry(path)!.Open()); return sr.ReadToEnd(); }
        return (Read("3D/3dmodel.model"), Read("Metadata/model_settings.config"));
    }

    static int Count(string text, string pattern) => Regex.Matches(text, pattern).Count;

    [Fact]
    public void Separate_EachTagIsItsOwnObject()
    {
        var r = BuildCandles(3);
        var (model, config) = Write(r, separate: true);
        Assert.Equal(3, Count(model, "<item "));
        Assert.Equal(3, Count(config, "<object "));
        Assert.Contains("value=\"נוי0\"", config);
        Assert.DoesNotContain("<plate>", config);
    }

    [Fact]
    public void OneObject_IsOneObjectWithTwoParts()
    {
        var (model, config) = Write(BuildCandles(3), separate: false);
        Assert.Equal(1, Count(model, "<item "));
        Assert.Equal(2, Count(config, "<part "));
    }

    [Fact]
    public void BackText_IsAThirdPart_OnFilament3()
    {
        var (_, config) = Write(BuildCandles(2, "חג שמח"), separate: true);
        Assert.Equal(6, Count(config, "<part "));
        Assert.Equal(2, Count(config, "value=\"Back text\"/>\\s*<metadata key=\"extruder\" value=\"3\""));
    }

    [Fact]
    public void FillBed_EveryBedIsABambuPlate()
    {
        var r = BuildCandles(40, shape: ArrangeShape.FillBed);
        Assert.Equal(2, r.Array.BedCount);
        var (model, config) = Write(r, separate: true);
        Assert.Equal(40, Count(model, "<item "));
        Assert.Equal(2, Count(config, "<plate>"));
        Assert.Contains("key=\"plater_id\" value=\"2\"", config);
        Assert.Equal(40, Count(config, "<model_instance>"));

        var (oneModel, oneConfig) = Write(r, separate: false);
        Assert.Equal(2, Count(oneModel, "<item "));
        Assert.Equal(2, Count(oneConfig, "<plate>"));
    }

    [Fact]
    public void Separate_TagsLandOnTheirBambuPlate()
    {
        var r = BuildCandles(40, shape: ArrangeShape.FillBed);
        var (model, _) = Write(r, separate: true);
        var xs = Regex.Matches(model, "transform=\"1 0 0 0 1 0 0 0 1 (\\S+) (\\S+) 0\"")
            .Select(m => double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        for (var i = 0; i < r.Tags.Count; i++)
        {
            // Plate 1 spans x 0..256, plate 2 (two plates side by side) x 307.2..563.2.
            var (lo, hi) = r.Tags[i].Bed == 0 ? (0.0, 256.0) : (307.2, 563.2);
            Assert.InRange(xs[i], lo, hi);
        }
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(1, 2, 307.2, 0)]
    [InlineData(2, 3, 0, -307.2)]
    [InlineData(4, 5, 307.2, -307.2)]
    [InlineData(2, 5, 614.4, 0)]
    public void BambuPlateGrid(int plate, int count, double x, double y)
    {
        var (px, py) = Exporter.BambuPlateOrigin(plate, count, new ArrangeSettings());
        Assert.Equal(x, px, 6);
        Assert.Equal(y, py, 6);
    }

    [Fact]
    public void NamesWithXmlCharacters_GiveValidXml()
    {
        var s = ItemSettings.DefaultsFor(Candle);
        var r = new TagBuilder().Build(new[] { "כהן & \"Dana\" <1>", "ab" }, Candle, s, new ArrangeSettings(), CancellationToken.None);
        var (model, config) = Write(r, separate: true);
        var names = System.Xml.Linq.XDocument.Parse(config).Descendants("object")
            .Select(o => o.Elements("metadata").First(m => (string?)m.Attribute("key") == "name").Attribute("value")!.Value).ToList();
        Assert.Contains("כהן & \"Dana\" <1>", names);
        Assert.Contains("ab", names);
        System.Xml.Linq.XDocument.Parse(model);
    }

    [Fact]
    public void Stl_IncludesTheBackText()
    {
        var plain = BuildCandles(1);
        var with = BuildCandles(1, "חג");
        using MemoryStream a = new(), b = new();
        Exporter.WriteStl(a, plain.Tags);
        Exporter.WriteStl(b, with.Tags);
        Assert.True(b.Length > a.Length);
    }
}
