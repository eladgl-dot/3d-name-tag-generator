using System.IO;
using System.Text.Json;
using NameTagGenerator.Items;
using NameTagGenerator.Text;

namespace NameTagGenerator.Output;

/// <summary>What the app remembers between runs.</summary>
public sealed class AppState
{
    public string ActiveItem { get; set; } = "keychain";
    public List<string> Names { get; set; } = new() { "ליב", "נויה" };
    public Dictionary<string, ItemSettings> Items { get; set; } = new();
    public ArrangeSettings Arrange { get; set; } = new();
    public string BaseColour { get; set; } = Views.PreviewBuilder.DefaultBaseColour;
    public string LetterColour { get; set; } = Views.PreviewBuilder.DefaultLetterColour;

    static string FilePath => Path.Combine(FontCatalog.AppDataFolder, "settings.json");

    public static AppState Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath)) ?? new AppState();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged settings file only costs the remembered settings.
        }
        return new AppState();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(FontCatalog.AppDataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember settings should never interrupt the user's work.
        }
    }
}
