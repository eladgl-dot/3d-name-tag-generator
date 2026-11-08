using System.Text.Json;
using System.Text.Json.Serialization;

namespace NameTagGenerator.Items;

/// <summary>Everything the website lets you change, and what it remembers between visits.</summary>
public sealed class SiteState
{
    public const string DefaultBaseColour = "#1e90ff", DefaultLetterColour = "#f4f6f8";

    public string ActiveItem { get; set; } = "keychain";
    public string NamesText { get; set; } = "נויה\nדניאל\nNoa";
    public Dictionary<string, ItemSettings> Items { get; set; } = new();
    public ArrangeSettings Arrange { get; set; } = new();
    public string BaseColour { get; set; } = DefaultBaseColour;
    public string LetterColour { get; set; } = DefaultLetterColour;

    [JsonIgnore]
    public List<string> Names => NamesText
        .Split('\n')
        .Select(CleanName)
        .Where(name => name.Length > 0)
        .ToList();

    /// <summary>Removes spaces around a name and the invisible direction marks that come along when pasting Hebrew.</summary>
    public static string CleanName(string line) =>
        new string(line.Where(c => !IsDirectionMark(c)).ToArray()).Trim();

    static bool IsDirectionMark(char c) => c is '‎' or '‏' or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩');

    [JsonIgnore]
    public IItemType Item => ItemRegistry.All.FirstOrDefault(i => i.Id == ActiveItem) ?? ItemRegistry.All[0];

    /// <summary>Each item keeps its own settings, created from its defaults the first time.</summary>
    [JsonIgnore]
    public ItemSettings Settings
    {
        get
        {
            var item = Item;
            if (!Items.TryGetValue(item.Id, out var settings))
                Items[item.Id] = settings = ItemSettings.DefaultsFor(item);
            return settings;
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>Reads saved settings. Damaged ones only cost what was remembered; anything missing gets its default.</summary>
    public static SiteState FromJson(string? json, Func<string, bool> fontExists)
    {
        if (string.IsNullOrEmpty(json)) return new SiteState();
        SiteState? state;
        try
        {
            state = JsonSerializer.Deserialize<SiteState>(json);
        }
        catch (JsonException)
        {
            return new SiteState();
        }
        if (state is null) return new SiteState();
        state.Repair(fontExists);
        return state;
    }

    void Repair(Func<string, bool> fontExists)
    {
        var defaults = new SiteState();
        ActiveItem ??= defaults.ActiveItem;
        NamesText ??= defaults.NamesText;
        Items ??= new Dictionary<string, ItemSettings>();
        Arrange ??= new ArrangeSettings();
        if (!IsColour(BaseColour)) BaseColour = DefaultBaseColour;
        if (!IsColour(LetterColour)) LetterColour = DefaultLetterColour;

        var itemDefaults = new ItemSettings();
        foreach (var (id, settings) in Items.ToList())
        {
            if (settings is null)
            {
                Items.Remove(id);
                continue;
            }
            if (settings.Font is null || !fontExists(settings.Font)) settings.Font = itemDefaults.Font;
            settings.Connector ??= itemDefaults.Connector;
            settings.Values ??= new Dictionary<string, double>();
            settings.Upgrade();
            var item = ItemRegistry.All.FirstOrDefault(i => i.Id == id);
            if (item is null) continue;
            foreach (var (key, value) in item.Defaults) settings.Values.TryAdd(key, value);
        }
    }

    static bool IsColour(string? colour) => colour is { Length: 7 } && colour[0] == '#' && colour.Skip(1).All(Uri.IsHexDigit);
}
