using System.IO;
using System.Windows;
using System.Windows.Media;

namespace NameTagGenerator.Text;

public sealed record FontOption(string Name, string Group);

/// <summary>Fonts inside the app, fonts installed on the PC and font files the user adds.</summary>
public static class FontCatalog
{
    public const string BuiltInBothGroup = "Built in: Hebrew and English";
    public const string BuiltInHebrewGroup = "Built in: Hebrew";
    public const string BuiltInEnglishGroup = "Built in: English";
    public const string AddedGroup = "Fonts you added";
    public const string HebrewGroup = "Hebrew fonts on this PC";
    public const string OtherGroup = "Other fonts on this PC";

    static readonly object Gate = new();
    static readonly Dictionary<string, FontFamily> Private = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, OutlineFont> Outlines = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<string> Added = new();
    static readonly Dictionary<string, string> BuiltInGroups = new(StringComparer.OrdinalIgnoreCase);
    static bool _loaded;

    public static string AppDataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "3D name tag generator");

    static string FontFolder => Path.Combine(AppDataFolder, "fonts");
    static string AddedFolder => Path.Combine(FontFolder, "added");

    public static IReadOnlyList<string> BuiltIn
    {
        get
        {
            EnsureLoaded();
            lock (Gate) return Private.Keys.Where(k => !Added.Contains(k)).ToList();
        }
    }

    /// <summary>Finds a font by name. Windows only says which file holds it. An unknown name gets the built-in fallback.</summary>
    public static OutlineFont Resolve(string name)
    {
        EnsureLoaded();
        lock (Gate)
            if (Outlines.TryGetValue(name, out var cached)) return cached;

        var font = LoadOutlines(WindowsFamily(name)) ?? BundledFonts.Fallback;
        lock (Gate) Outlines[name] = font;
        return font;
    }

    /// <summary>The Windows font object for a name, for showing the font in the app's own lists.</summary>
    public static FontFamily WindowsFamily(string name)
    {
        EnsureLoaded();
        lock (Gate) return Private.TryGetValue(name, out var family) ? family : new FontFamily(name);
    }

    static OutlineFont? LoadOutlines(FontFamily family)
    {
        try
        {
            if (FaceFile(family, FontWeights.Normal) is not { } regular) return null;
            var bold = FaceFile(family, FontWeights.Bold);
            var realBold = bold is { Simulated: false } && bold.Path != regular.Path ? bold.Path : null;
            return OutlineFont.FromFiles(regular.Path, realBold, regular.Family);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or InvalidDataException or SixLabors.Fonts.InvalidFontFileException)
        {
            // A damaged or unusual font must not stop the app.
            return null;
        }
    }

    sealed record FontFace(string Path, string? Family, bool Simulated);

    static FontFace? FaceFile(FontFamily family, FontWeight weight)
    {
        var typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphs) || !glyphs.FontUri.IsFile) return null;
        var names = glyphs.Win32FamilyNames;
        var familyName = names.Values.FirstOrDefault();
        return new FontFace(glyphs.FontUri.LocalPath, familyName, (glyphs.StyleSimulations & StyleSimulations.BoldSimulation) != 0);
    }

    public static IReadOnlyList<FontOption> ListAll()
    {
        EnsureLoaded();
        var options = new List<FontOption>();
        HashSet<string> taken;
        lock (Gate)
        {
            var groupOrder = new[] { BuiltInBothGroup, BuiltInHebrewGroup, BuiltInEnglishGroup };
            options.AddRange(Private.Keys.Where(k => !Added.Contains(k))
                .Select(k => new FontOption(k, BuiltInGroups[k]))
                .OrderBy(f => Array.IndexOf(groupOrder, f.Group)).ThenBy(f => f.Name));
            options.AddRange(Added.OrderBy(k => k).Select(k => new FontOption(k, AddedGroup)));
            taken = new HashSet<string>(Private.Keys, StringComparer.OrdinalIgnoreCase);
        }

        var hebrew = new List<FontOption>();
        var other = new List<FontOption>();
        foreach (var family in Fonts.SystemFontFamilies)
        {
            var name = family.Source;
            if (taken.Contains(name)) continue;
            (HasHebrew(family) ? hebrew : other).Add(new FontOption(name, HasHebrew(family) ? HebrewGroup : OtherGroup));
        }
        options.AddRange(hebrew.OrderBy(f => f.Name));
        options.AddRange(other.OrderBy(f => f.Name));
        return options;
    }

    /// <summary>Copies a font file into the app's folder so it keeps working. Returns its name, or null if unreadable.</summary>
    public static string? AddFontFile(string path)
    {
        Directory.CreateDirectory(AddedFolder);
        var copy = Path.Combine(AddedFolder, Path.GetFileName(path));
        File.Copy(path, copy, overwrite: true);
        lock (Gate) return Register(copy, added: true);
    }

    static string GroupFor(GlyphTypeface glyphs)
    {
        var hebrew = glyphs.CharacterToGlyphMap.ContainsKey(0x05D0);
        var english = glyphs.CharacterToGlyphMap.ContainsKey('a');
        return hebrew && english ? BuiltInBothGroup : hebrew ? BuiltInHebrewGroup : BuiltInEnglishGroup;
    }

    static bool HasHebrew(FontFamily family)
    {
        const int alef = 0x05D0;
        foreach (var typeface in family.GetTypefaces())
        {
            try
            {
                if (typeface.TryGetGlyphTypeface(out var glyphs) && glyphs.CharacterToGlyphMap.ContainsKey(alef)) return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // A damaged font must not stop the app; it is simply not listed as Hebrew.
            }
        }
        return false;
    }

    static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;
            Directory.CreateDirectory(FontFolder);

            foreach (var resource in BundledFonts.ResourceNames)
            {
                var file = Path.Combine(FontFolder, resource[BundledFonts.ResourcePrefix.Length..]);
                using (var stream = BundledFonts.OpenResource(resource))
                {
                    if (!File.Exists(file) || new FileInfo(file).Length != stream.Length)
                    {
                        using var output = File.Create(file);
                        stream.CopyTo(output);
                    }
                }
                Register(file, added: false);
            }

            if (Directory.Exists(AddedFolder))
                foreach (var file in Directory.GetFiles(AddedFolder))
                    Register(file, added: true);
        }
    }

    /// <summary>
    /// Loads one font file. The name comes from the file itself, because asking Windows for
    /// the families at a file's path returns every font in that file's folder.
    /// </summary>
    static string? Register(string file, bool added)
    {
        string? name;
        GlyphTypeface glyphs;
        try
        {
            glyphs = new GlyphTypeface(new Uri(file));
            name = glyphs.FamilyNames.Values.FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NullReferenceException)
        {
            return null;
        }
        if (name is null) return null;

        var folder = new Uri(Path.GetDirectoryName(file) + Path.DirectorySeparatorChar);
        Private[name] = new FontFamily(folder, $"./{Path.GetFileName(file)}#{name}");
        Outlines.Remove(name);
        if (added && !Added.Contains(name)) Added.Add(name);
        if (!added) BuiltInGroups[name] = GroupFor(glyphs);
        return name;
    }
}
