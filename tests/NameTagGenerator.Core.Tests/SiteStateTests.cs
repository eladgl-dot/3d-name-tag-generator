using System.Text.Json;
using NameTagGenerator.Items;

namespace NameTagGenerator.Core.Tests;

public class SiteStateTests
{
    static bool AnyFont(string name) => true;

    static SiteState Load(string? json) => SiteState.FromJson(json, AnyFont);

    [Fact]
    public void Saving_and_loading_keeps_every_setting()
    {
        var state = new SiteState { ActiveItem = "bookmark", NamesText = "נויה\nNoa", BaseColour = "#112233", LetterColour = "#445566" };
        state.Settings.Font = "Rubik";
        state.Settings.Bold = true;
        state.Settings.Connector = "_";
        state.Settings.Hollow = true;
        state.Settings.Values[Keys.FontSize] = 17.5;
        state.Arrange.Shape = ArrangeShape.Square;
        state.Arrange.Order = ArrangeOrder.BySize;
        state.Arrange.Columns = 3;
        state.Arrange.GapX = 7;
        state.Arrange.PlateHeight = 180;

        var loaded = Load(state.ToJson());

        Assert.Equal("bookmark", loaded.ActiveItem);
        Assert.Equal("נויה\nNoa", loaded.NamesText);
        Assert.Equal("#112233", loaded.BaseColour);
        Assert.Equal("#445566", loaded.LetterColour);
        Assert.Equal("Rubik", loaded.Settings.Font);
        Assert.True(loaded.Settings.Bold);
        Assert.Equal("_", loaded.Settings.Connector);
        Assert.True(loaded.Settings.Hollow);
        Assert.Equal(17.5, loaded.Settings[Keys.FontSize]);
        Assert.Equal(ArrangeShape.Square, loaded.Arrange.Shape);
        Assert.Equal(ArrangeOrder.BySize, loaded.Arrange.Order);
        Assert.Equal(3, loaded.Arrange.Columns);
        Assert.Equal(7, loaded.Arrange.GapX);
        Assert.Equal(180, loaded.Arrange.PlateHeight);
    }

    [Fact]
    public void Each_item_keeps_its_own_settings()
    {
        var state = new SiteState { ActiveItem = "keychain" };
        state.Settings.Values[Keys.FontSize] = 11;
        state.ActiveItem = "pencil";
        state.Settings.Values[Keys.FontSize] = 22;

        var loaded = Load(state.ToJson());

        Assert.Equal(22, loaded.Settings[Keys.FontSize]);
        loaded.ActiveItem = "keychain";
        Assert.Equal(11, loaded.Settings[Keys.FontSize]);
    }

    [Fact]
    public void Only_the_real_settings_are_saved()
    {
        using var saved = JsonDocument.Parse(new SiteState().ToJson());

        var names = saved.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Contains("NamesText", names);
        Assert.DoesNotContain("Names", names);
        Assert.DoesNotContain("Item", names);
        Assert.DoesNotContain("Settings", names);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"Arrange\":{\"Columns\":\"many\"}}")]
    public void Damaged_saved_settings_give_the_defaults(string? json)
    {
        var loaded = Load(json);

        Assert.Equal(new SiteState().NamesText, loaded.NamesText);
        Assert.Equal("keychain", loaded.Item.Id);
        Assert.Equal(SiteState.DefaultBaseColour, loaded.BaseColour);
    }

    [Fact]
    public void Empty_parts_of_saved_settings_become_the_defaults()
    {
        var loaded = Load("""{"ActiveItem":null,"NamesText":null,"Items":{"keychain":null},"Arrange":null,"BaseColour":null,"LetterColour":null}""");

        Assert.Equal("keychain", loaded.Item.Id);
        Assert.Equal(new SiteState().NamesText, loaded.NamesText);
        Assert.Equal(new ArrangeSettings().PlateWidth, loaded.Arrange.PlateWidth);
        Assert.Equal(SiteState.DefaultBaseColour, loaded.BaseColour);
        Assert.Equal(SiteState.DefaultLetterColour, loaded.LetterColour);
        Assert.Equal(new ItemSettings().Font, loaded.Settings.Font);
        Assert.True(loaded.Settings[Keys.FontSize] > 0);
    }

    [Fact]
    public void Empty_parts_of_one_item_become_the_defaults()
    {
        var loaded = Load("""{"Items":{"keychain":{"Font":null,"Connector":null,"Values":null}}}""");

        Assert.Equal(new ItemSettings().Font, loaded.Settings.Font);
        Assert.Equal(new ItemSettings().Connector, loaded.Settings.Connector);
        Assert.True(loaded.Settings[Keys.FontSize] > 0);
    }

    [Fact]
    public void An_empty_names_box_stays_empty()
    {
        Assert.Equal("", Load("""{"NamesText":""}""").NamesText);
    }

    [Fact]
    public void A_setting_added_in_a_later_version_gets_its_default()
    {
        var keychain = ItemRegistry.All.Single(i => i.Id == "keychain");

        var loaded = Load("""{"Items":{"keychain":{"Values":{"fontSize":33}}}}""");

        Assert.Equal(33, loaded.Settings[Keys.FontSize]);
        Assert.Equal(keychain.Defaults[Keys.BaseHeight], loaded.Settings[Keys.BaseHeight]);
    }

    [Fact]
    public void A_setting_renamed_in_a_later_version_keeps_its_value()
    {
        var loaded = Load("""{"ActiveItem":"cupmarker","Items":{"cupmarker":{"Values":{"gripDepth":14}}}}""");

        Assert.Equal(14, loaded.Settings[Keys.GripDepthInner]);
        Assert.Equal(14, loaded.Settings[Keys.GripDepthOuter]);
    }

    [Fact]
    public void A_font_that_is_gone_goes_back_to_the_default()
    {
        const string json = """{"Items":{"keychain":{"Font":"Deleted Font"}}}""";

        Assert.Equal(new ItemSettings().Font, SiteState.FromJson(json, name => name != "Deleted Font").Settings.Font);
    }

    [Fact]
    public void A_font_that_exists_is_kept()
    {
        const string json = """{"Items":{"keychain":{"Font":"My Upload"}}}""";

        Assert.Equal("My Upload", SiteState.FromJson(json, name => name == "My Upload").Settings.Font);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#12345g")]
    public void A_colour_the_picker_cannot_show_goes_back_to_the_default(string colour)
    {
        var loaded = Load($$"""{"BaseColour":"{{colour}}","LetterColour":"{{colour}}"}""");

        Assert.Equal(SiteState.DefaultBaseColour, loaded.BaseColour);
        Assert.Equal(SiteState.DefaultLetterColour, loaded.LetterColour);
    }

    [Fact]
    public void Invisible_direction_marks_are_not_part_of_a_name()
    {
        var state = new SiteState { NamesText = "‏נויה‎\n‫דניאל‬\n⁧Noa⁩\n‏\n ‎ " };

        Assert.Equal(new[] { "נויה", "דניאל", "Noa" }, state.Names);
    }

    [Fact]
    public void An_item_that_is_gone_shows_the_first_item()
    {
        Assert.Equal(ItemRegistry.All[0].Id, Load("""{"ActiveItem":"spaceship"}""").Item.Id);
    }
}
