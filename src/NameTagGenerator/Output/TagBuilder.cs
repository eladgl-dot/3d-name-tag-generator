using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Text;

namespace NameTagGenerator.Output;

public sealed record PlacedTag(string Name, ItemModel Model, double X, double Y);

public sealed record BuildResult(IReadOnlyList<PlacedTag> Tags, ArrayResult Array)
{
    public double VolumeMm3 => Tags.Sum(t => t.Model.Base.VolumeMm3 + t.Model.Letters.VolumeMm3);
    public double HeightMm => Tags.Count == 0 ? 0 : Tags.Max(t => Poly.Mm(t.Model.Letters.Vertices.Concat(t.Model.Base.Vertices).Max(v => v.Z)));
    public IEnumerable<string> NamesWithLooseParts => Tags.Where(t => t.Model.LooseParts > 1).Select(t => t.Name);
}

/// <summary>Builds every name into a model and places them in the array. Finished models are reused until settings change.</summary>
public sealed class TagBuilder
{
    readonly ConcurrentDictionary<string, ItemModel> _models = new();
    string _modelsKey = "";

    public BuildResult Build(IReadOnlyList<string> names, IItemType item, ItemSettings settings, ArrangeSettings arrange, CancellationToken cancel)
    {
        var key = item.Id + JsonSerializer.Serialize(settings);
        if (key != _modelsKey)
        {
            _models.Clear();
            _modelsKey = key;
        }

        var family = FontCatalog.Resolve(settings.Font);
        var models = new List<(string Name, ItemModel Model)>();
        foreach (var name in names)
        {
            cancel.ThrowIfCancellationRequested();
            var model = _models.GetOrAdd(name, n =>
            {
                var text = TextOutliner.Build(n, family, settings.Bold, settings[Keys.FontSize], settings.Connector, settings.Hollow);
                return item.Build(text, settings);
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
            var b = boxes[i];
            tags.Add(new PlacedTag(models[i].Name, models[i].Model, cx - Poly.Mm(b.MinX + b.MaxX) / 2, cy - Poly.Mm(b.MinY + b.MaxY) / 2));
        }
        return new BuildResult(tags, result);
    }
}

public static class Exporter
{
    public static void SaveStl(string path, IEnumerable<PlacedTag> tags)
    {
        using var file = File.Create(path);
        StlWriter.Write(file, tags.SelectMany(t => new[] { (t.Model.Base, t.X, t.Y), (t.Model.Letters, t.X, t.Y) }));
    }

    public static void Save3mf(string path, string title, IEnumerable<PlacedTag> tags)
    {
        var bases = new Mesh();
        var letters = new Mesh();
        foreach (var t in tags)
        {
            bases.AddMoved(t.Model.Base, Poly.U(t.X), Poly.U(t.Y));
            letters.AddMoved(t.Model.Letters, Poly.U(t.X), Poly.U(t.Y));
        }
        using var file = File.Create(path);
        ThreeMfWriter.Write(file, title, bases, letters);
    }

    public static string SafeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "tag" : clean;
    }
}

/// <summary>Finds Bambu Studio and opens files in it. It never closes or changes a running copy itself.</summary>
public static class BambuStudio
{
    const string ExeName = "bambu-studio.exe";

    public static string? FindExe()
    {
        foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
        {
            var candidate = Path.Combine(Environment.GetFolderPath(root), "Bambu Studio", ExeName);
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            using var uninstall = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                using var app = uninstall.OpenSubKey(name);
                if (app?.GetValue("DisplayIcon") is string icon && icon.EndsWith(ExeName, StringComparison.OrdinalIgnoreCase) && File.Exists(icon))
                    return icon;
            }
        }
        return null;
    }

    public static bool IsRunning => Process.GetProcessesByName("bambu-studio").Length > 0;

    public static void Open(string exe, string file)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        start.ArgumentList.Add(file);
        Process.Start(start)?.Dispose();
    }
}
