using System.Reflection;
using SixLabors.Fonts;

namespace NameTagGenerator.Text;

/// <summary>A font ready to be turned into outlines: its regular face and, when one exists, a real bold face.</summary>
public sealed class OutlineFont
{
    readonly Face _regular;
    readonly Face? _bold;

    OutlineFont(Face regular, Face? bold)
    {
        _regular = regular;
        _bold = bold;
    }

    /// <summary>Family name read from the font file itself.</summary>
    public string Name => _regular.Family.Name;

    public bool HasHebrew => _regular.Has(0x05D0);
    public bool HasEnglish => _regular.Has('a');

    /// <summary>True when bold has to be made by thickening the regular outlines.</summary>
    public bool SimulatesBold => _bold is null && (_regular.Style & FontStyle.Bold) == 0;

    public static OutlineFont FromStreams(Stream regular, Stream? bold = null)
    {
        var fonts = new FontCollection();
        return new OutlineFont(Face.Load(fonts, regular), bold is null ? null : Face.Load(fonts, bold));
    }

    /// <summary>Loads font files. <paramref name="family"/> picks the face inside a .ttc file that holds several.</summary>
    public static OutlineFont FromFiles(string regularPath, string? boldPath, string? family = null)
    {
        var fonts = new FontCollection();
        return new OutlineFont(Face.Load(fonts, regularPath, family, FontStyle.Regular),
            boldPath is null ? null : Face.Load(fonts, boldPath, family, FontStyle.Bold));
    }

    internal Font Create(float emSize, bool bold) =>
        (bold && _bold is not null ? _bold.Value : _regular).Create(emSize);

    internal FontFamily Family => _regular.Family;

    readonly record struct Face(FontFamily Family, FontStyle Style)
    {
        public Font Create(float emSize) => Family.CreateFont(emSize, Style);

        public bool Has(int codePoint) =>
            Create(72).TryGetGlyphs(new SixLabors.Fonts.Unicode.CodePoint(codePoint), out var glyphs) &&
            glyphs.Any(g => g.GlyphMetrics.GlyphType != GlyphType.Fallback);

        public static Face Load(FontCollection fonts, Stream stream)
        {
            var family = fonts.Add(stream, out var description);
            return new Face(family, description.Style);
        }

        public static Face Load(FontCollection fonts, string path, string? familyName, FontStyle wanted)
        {
            if (!path.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
            {
                var family = fonts.Add(path, out var description);
                return new Face(family, description.Style);
            }

            var families = fonts.AddCollection(path, out var faces).ToList();
            var all = faces.ToArray();
            var index = Array.FindIndex(all, f => string.Equals(f.FontFamilyInvariantCulture, familyName, StringComparison.OrdinalIgnoreCase) && f.Style == wanted);
            if (index < 0) index = Array.FindIndex(all, f => string.Equals(f.FontFamilyInvariantCulture, familyName, StringComparison.OrdinalIgnoreCase));
            var face = all[Math.Max(index, 0)];
            return new Face(families.First(f => f.Name == face.FontFamilyInvariantCulture), face.Style);
        }
    }
}

/// <summary>The fonts built into the shared code. They work the same on Windows and in a browser.</summary>
public static class BundledFonts
{
    /// <summary>Draws letters the chosen font does not have, and stands in for an unknown font.</summary>
    public const string FallbackName = "Rubik";

    static readonly Lazy<IReadOnlyDictionary<string, OutlineFont>> Loaded = new(Load);

    public static IReadOnlyDictionary<string, OutlineFont> All => Loaded.Value;

    public static OutlineFont Fallback => All[FallbackName];

    public static IReadOnlyList<string> Names => All.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Finds a built-in font by name. An unknown name gets the fallback font.</summary>
    public static OutlineFont Get(string name) => All.TryGetValue(name, out var font) ? font : Fallback;

    /// <summary>Resource names of the bundled font files.</summary>
    public static IEnumerable<string> ResourceNames =>
        typeof(BundledFonts).Assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal));

    public const string ResourcePrefix = "font:";

    public static Stream OpenResource(string resourceName) =>
        typeof(BundledFonts).Assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"Missing font resource {resourceName}");

    static IReadOnlyDictionary<string, OutlineFont> Load()
    {
        var fonts = new Dictionary<string, OutlineFont>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in ResourceNames)
        {
            using var stream = OpenResource(resource);
            var font = OutlineFont.FromStreams(stream);
            fonts[font.Name] = font;
        }
        return fonts;
    }
}
