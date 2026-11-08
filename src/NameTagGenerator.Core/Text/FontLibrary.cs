namespace NameTagGenerator.Text;

/// <summary>The built-in fonts plus any the user added.</summary>
public sealed class FontLibrary
{
    const string SameNameSuffix = " (yours)";

    readonly Dictionary<string, OutlineFont> _added = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AddedNames => _added.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();

    public bool Exists(string name) => _added.ContainsKey(name) || BundledFonts.All.ContainsKey(name);

    /// <summary>An unknown name gets the fallback font.</summary>
    public OutlineFont Get(string name) => _added.TryGetValue(name, out var font) ? font : BundledFonts.Get(name);

    /// <summary>Adds a font file. Returns its name in the list, or null when it can't be used; then nothing changes.</summary>
    public string? Add(byte[] file)
    {
        var font = Read(file);
        if (font is null) return null;
        var name = BundledFonts.All.ContainsKey(font.Name) ? font.Name + SameNameSuffix : font.Name;
        _added[name] = font;
        return name;
    }

    /// <summary>Draws one letter as the test, because a damaged font can open fine and only fail on its outlines.</summary>
    static OutlineFont? Read(byte[] file)
    {
        try
        {
            using var stream = new MemoryStream(file);
            var font = OutlineFont.FromStreams(stream);
            if (string.IsNullOrWhiteSpace(font.Name) || !(font.HasHebrew || font.HasEnglish)) return null;
            var letter = TextOutliner.Build(font.HasHebrew ? "א" : "a", font, bold: false, sizeMm: 12, connector: "-", hollow: false);
            return letter.Letters.Count > 0 ? font : null;
        }
        catch (Exception)
        {
            // The font reader fails in many ways on a bad file, and any of them means the same thing here.
            return null;
        }
    }
}
