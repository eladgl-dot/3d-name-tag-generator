using Clipper2Lib;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Pictures;
using NameTagGenerator.Text;

namespace NameTagGenerator.Output;

/// <summary>A finished tag moved by X, Y (mm) to its place. Bed: which bed it is on (Fill the bed only).</summary>
public sealed record PlacedTag(string Name, ItemModel Model, double X, double Y, int Bed = 0);

public sealed record BuildResult(IReadOnlyList<PlacedTag> Tags, ArrayResult Array)
{
    public double VolumeMm3 => Tags.Sum(t => t.Model.Parts.Sum(p => p.VolumeMm3));
    public double HeightMm => Tags.Count == 0 ? 0 : Tags.Max(t => Poly.Mm(t.Model.Letters.Vertices.Concat(t.Model.Base.Vertices).Max(v => v.Z)));
    public IEnumerable<string> NamesWithLooseParts => Tags.Where(t => t.Model.LooseParts > 1).Select(t => t.Name);
}

/// <summary>Builds every name into a model and places them in the array. Finished models are reused until settings change.</summary>
public sealed class TagBuilder
{
    readonly ConcurrentDictionary<string, ItemModel> _models = new();
    readonly Func<string, OutlineFont> _fonts;
    string _modelsKey = "";

    /// <param name="fonts">Finds a font by name. Without one, only the built-in fonts are available.</param>
    public TagBuilder(Func<string, OutlineFont>? fonts = null) => _fonts = fonts ?? BundledFonts.Get;

    /// <param name="picture">Goes beside every name, when there is one.</param>
    public BuildResult Build(IReadOnlyList<string> names, IItemType item, ItemSettings settings, ArrangeSettings arrange, CancellationToken cancel, NamePicture? picture = null)
    {
        var key = item.Id + JsonSerializer.Serialize(settings) + picture?.Key;
        if (key != _modelsKey)
        {
            _models.Clear();
            _modelsKey = key;
        }

        var family = _fonts(settings.Font);
        // The item's text settings (the candle's back text), drawn once in the name's font and shared by every name.
        var texts = new Dictionary<string, TextShape>();
        foreach (var field in item.Fields.Where(f => f.Kind == FieldKind.Text))
        {
            var value = settings.Text(field.Key).Trim();
            // No connector: words in these texts stay apart, as typed.
            if (value.Length > 0) texts[field.Key] = TextOutliner.Build(value, family, settings.Bold, settings[Keys.FontSize], "", settings.Hollow);
        }
        var models = new List<(string Name, ItemModel Model)>();
        foreach (var name in names)
        {
            cancel.ThrowIfCancellationRequested();
            var model = _models.GetOrAdd(name, n =>
            {
                var text = TextOutliner.Build(n, family, settings.Bold, settings[Keys.FontSize], settings.Connector, settings.Hollow);
                return item.Build(picture?.AddTo(text) ?? text, settings, texts);
            });
            if (!model.Base.IsEmpty) models.Add((name, model));
        }

        var widths = models.Select(m => (m.Name, Poly.Mm(Poly.Bounds(m.Model.BaseArea).Width))).ToList();
        models = ArrayLayout.Order(widths, arrange.Order).Select(i => models[i]).ToList();

        var boxes = models.Select(m => Poly.Bounds(m.Model.BaseArea)).ToList();
        var result = ArrayLayout.Place(boxes.Select(b => (Poly.Mm(b.Width), Poly.Mm(b.Height))).ToList(), arrange);

        var tags = new List<PlacedTag>();
        for (var i = 0; i < models.Count; i++)
        {
            var (cx, cy) = result.Centers[i];
            var model = result.Turned[i] ? Turn(models[i].Model) : models[i].Model;
            var b = result.Turned[i] ? Poly.Bounds(model.BaseArea) : boxes[i];
            tags.Add(new PlacedTag(models[i].Name, model, cx - Poly.Mm(b.MinX + b.MaxX) / 2, cy - Poly.Mm(b.MinY + b.MaxY) / 2, result.Beds[i]));
        }
        return new BuildResult(tags, result);
    }

    /// <summary>The same model given a quarter turn anticlockwise on the bed. A turn keeps every closed body closed.</summary>
    public static ItemModel Turn(ItemModel m)
    {
        static (long, long, long) Point((long X, long Y, long Z) v) => (-v.Y, v.X, v.Z);
        static Paths64 Area(Paths64 a) => new(a.Select(path => new Path64(path.Select(p => new Point64(-p.Y, p.X)))));
        return m with
        {
            Base = m.Base.Mapped(Point),
            Letters = m.Letters.Mapped(Point),
            Back = m.Back?.Mapped(Point),
            BaseArea = Area(m.BaseArea),
            LetterArea = Area(m.LetterArea),
            BackArea = m.BackArea is null ? null : Area(m.BackArea),
            RingHoleCenter = m.RingHoleCenter is { } c ? new Point64(-c.Y, c.X) : null,
        };
    }
}

public static class Exporter
{
    public static void SaveStl(string path, IEnumerable<PlacedTag> tags)
    {
        using var file = File.Create(path);
        WriteStl(file, tags);
    }

    public static void Save3mf(string path, string title, IEnumerable<PlacedTag> tags, bool separate = true, ArrangeSettings? arrange = null)
    {
        using var file = File.Create(path);
        Write3mf(file, title, tags, separate, arrange);
    }

    public static void WriteStl(Stream output, IEnumerable<PlacedTag> tags) =>
        StlWriter.Write(output, tags.SelectMany(t => t.Model.Parts.Select(p => (p, t.X, t.Y))));

    /// <param name="separate">True: every tag is its own object. False: one object per bed, as in version 0.1.</param>
    /// <param name="arrange">The bed size, to place the tags on Bambu's plates. Without it, a 256 × 256 bed.</param>
    public static void Write3mf(Stream output, string title, IEnumerable<PlacedTag> tags, bool separate = true, ArrangeSettings? arrange = null)
    {
        var list = tags.ToList();
        arrange ??= new ArrangeSettings();
        var beds = list.Count == 0 ? 1 : list.Max(t => t.Bed) + 1;

        // Where the middle of bed b lands in Bambu: its plate's front left corner plus half a bed.
        (double X, double Y) PlateMiddle(int bed)
        {
            var (px, py) = BambuPlateOrigin(bed, beds, arrange);
            return (px + arrange.PlateWidth / 2, py + arrange.PlateHeight / 2);
        }

        var objects = new List<ThreeMfObject>();
        if (separate)
            foreach (var t in list)
            {
                var (bx, by) = ArrayLayout.BedCentre(t.Bed, arrange);
                var (mx, my) = PlateMiddle(t.Bed);
                objects.Add(new ThreeMfObject(t.Name, PartsOf(t.Model.Base, t.Model.Letters, t.Model.Back), mx + t.X - bx, my + t.Y - by, t.Bed));
            }
        else
            foreach (var bed in list.GroupBy(t => t.Bed).OrderBy(g => g.Key))
            {
                var (bx, by) = ArrayLayout.BedCentre(bed.Key, arrange);
                var (mx, my) = PlateMiddle(bed.Key);
                Mesh bases = new(), letters = new(), back = new();
                foreach (var t in bed)
                {
                    long dx = Poly.U(t.X - bx), dy = Poly.U(t.Y - by);
                    bases.AddMoved(t.Model.Base, dx, dy);
                    letters.AddMoved(t.Model.Letters, dx, dy);
                    if (t.Model.Back is { } b) back.AddMoved(b, dx, dy);
                }
                var name = beds > 1 ? $"{title} {bed.Key + 1}" : title;
                objects.Add(new ThreeMfObject(name, PartsOf(bases, letters, back.IsEmpty ? null : back), mx, my, bed.Key));
            }
        ThreeMfWriter.Write(output, title, objects, plates: beds > 1);
    }

    static IReadOnlyList<ThreeMfPart> PartsOf(Mesh bases, Mesh letters, Mesh? back)
    {
        var parts = new List<ThreeMfPart> { new("Base", bases, 1) };
        if (!letters.IsEmpty) parts.Add(new("Letters", letters, 2));
        if (back is { IsEmpty: false }) parts.Add(new("Back text", back, 3));
        return parts;
    }

    /// <summary>
    /// Front left corner of Bambu Studio's plate number <paramref name="plate"/> (0 = first) when there are <paramref name="count"/> plates.
    /// Bambu lays plates in a grid about √count wide, each a fifth of a bed apart, further rows in front of the first.
    /// </summary>
    public static (double X, double Y) BambuPlateOrigin(int plate, int count, ArrangeSettings arrange)
    {
        var root = Math.Sqrt(count);
        var columns = Math.Max(1, (int)Math.Round(root));
        if (root > columns) columns++;
        return (plate % columns * arrange.PlateWidth * 1.2, -(plate / columns) * arrange.PlateHeight * 1.2);
    }

    public static string SafeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "tag" : clean;
    }
}
