using NameTagGenerator.Items;

namespace NameTagGenerator.Web;

/// <summary>Everything the page lets you change.</summary>
public sealed class SiteState
{
    public const string DefaultBaseColour = "#1e90ff", DefaultLetterColour = "#f4f6f8";

    public string ActiveItem { get; set; } = "keychain";
    public string NamesText { get; set; } = "נויה\nדניאל\nNoa";
    public Dictionary<string, ItemSettings> Items { get; set; } = new();
    public ArrangeSettings Arrange { get; set; } = new();
    public string BaseColour { get; set; } = DefaultBaseColour;
    public string LetterColour { get; set; } = DefaultLetterColour;

    public List<string> Names => NamesText
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToList();

    public IItemType Item => ItemRegistry.All.FirstOrDefault(i => i.Id == ActiveItem) ?? ItemRegistry.All[0];

    /// <summary>Each item keeps its own settings, created from its defaults the first time.</summary>
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
}
