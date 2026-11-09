using System.Globalization;
using System.Text;
using Clipper2Lib;
using NameTagGenerator.Geometry;

namespace NameTagGenerator.Text;

/// <summary>A printed outline as flat picture data, in millimetres, with the y axis pointing down as a screen's does.</summary>
public sealed record OutlineImage(string Path, double X, double Y, double Width, double Height)
{
    public string ViewBox => string.Create(CultureInfo.InvariantCulture, $"{X:0.##} {Y:0.##} {Width:0.##} {Height:0.##}");
}

/// <summary>Draws names on screen from the same outlines that get printed.</summary>
public static class OutlinePreview
{
    const double SampleSizeMm = 12;

    /// <returns>Null when there is nothing to draw.</returns>
    public static OutlineImage? ToImage(Paths64 outlines)
    {
        var paths = outlines.Where(p => p.Count >= 3).ToList();
        if (paths.Count == 0) return null;

        var box = Poly.Bounds(new Paths64(paths));
        var data = new StringBuilder();
        foreach (var path in paths)
        {
            for (var i = 0; i < path.Count; i++)
                data.Append(CultureInfo.InvariantCulture, $"{(i == 0 ? 'M' : 'L')}{Poly.Mm(path[i].X):0.##} {-Poly.Mm(path[i].Y):0.##}");
            data.Append('Z');
        }
        return new OutlineImage(data.ToString(), Poly.Mm(box.MinX), -Poly.Mm(box.MaxY), Poly.Mm(box.Width), Poly.Mm(box.Height));
    }

    public static OutlineImage? Text(string text, OutlineFont font, bool bold, string connector) =>
        ToImage(TextOutliner.Build(text, font, bold, SampleSizeMm, connector, hollow: false).Letters);
}

/// <summary>The emoji offered by the picker. All of them are checked for printing at small sizes.</summary>
public static class EmojiChoices
{
    public static IReadOnlyList<(string Emoji, string Name)> All { get; } = new[]
    {
        ("⭐", "Star"), ("❤", "Heart"), ("\U0001F600", "Smile"), ("\U0001F60E", "Sunglasses"),
        ("☀", "Sun"), ("\U0001F319", "Moon"), ("☁", "Cloud"), ("\U0001F308", "Rainbow"),
        ("\U0001F340", "Clover"), ("\U0001F338", "Blossom"), ("\U0001F33B", "Sunflower"), ("\U0001F431", "Cat"),
        ("\U0001F436", "Dog"), ("\U0001F43E", "Paw prints"), ("\U0001F34E", "Apple"), ("\U0001F41F", "Fish"),
        ("⚽", "Football"), ("\U0001F3C0", "Basketball"), ("\U0001F3B5", "Music"), ("\U0001F388", "Balloon"),
        ("\U0001F381", "Gift"), ("\U0001F451", "Crown"), ("\U0001F680", "Rocket"), ("✈", "Plane"),
    };
}
