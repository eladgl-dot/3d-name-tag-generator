using System.Globalization;
using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Text;
using Xunit;

namespace NameTagGenerator.Tests;

public class EmojiTests
{
    const string Star = "⭐";
    const string Heart = "❤";
    const string HeartWithMark = "❤️";
    const string ThumbsUp = "\U0001F44D";
    const string ThumbsUpMediumSkin = "\U0001F44D\U0001F3FD";
    const string Family = "\U0001F468‍\U0001F469‍\U0001F467";
    const string Man = "\U0001F468";
    const string Israel = "\U0001F1EE\U0001F1F1";
    const string Hebrew = "דנה";

    static Paths64 Draw(string text, string font = "Rubik") =>
        TextOutliner.Build(text, BundledFonts.Get(font), false, 12, "", false).Letters;

    static Paths64 DrawWithEmojiFont(string text) =>
        TextOutliner.Build(text, BundledFonts.Emoji, false, 12, "", false).Letters;

    [Theory]
    [InlineData(Star)]
    [InlineData(Heart)]
    [InlineData(ThumbsUp)]
    [InlineData("\U0001F600")]
    [InlineData("\U0001F31F")]
    [InlineData("☀")]
    public void Emoji_IsDrawnFromTheEmojiFont_WhateverFontIsChosen(string emoji)
    {
        var expected = Clipper.Area(DrawWithEmojiFont(emoji));
        Assert.True(expected > 1, "the emoji font should draw this");
        foreach (var font in new[] { "Rubik", "Gan CLM", "Gveret Levin", "Lobster" })
            Assert.Equal(expected, Clipper.Area(Draw(emoji, font)), expected * 0.001);
    }

    [Fact]
    public void Emoji_WithVariationMark_LooksTheSameAsWithout() =>
        Assert.Equal(Clipper.Area(Draw(Heart)), Clipper.Area(Draw(HeartWithMark)), 1.0);

    [Fact]
    public void JoinedEmoji_IsOneShape_NotPeopleSideBySide()
    {
        var man = Poly.Mm(Poly.Bounds(Draw(Man)).Width);
        var family = Poly.Mm(Poly.Bounds(Draw(Family)).Width);
        Assert.True(family < man * 2, $"family {family:0.0} mm, one person {man:0.0} mm");
    }

    [Fact]
    public void SkinTone_DoesNotAddASecondShape()
    {
        var plain = Poly.Mm(Poly.Bounds(Draw(ThumbsUp)).Width);
        var toned = Poly.Mm(Poly.Bounds(Draw(ThumbsUpMediumSkin)).Width);
        Assert.Equal(plain, toned, 0.5);
    }

    [Fact]
    public void Flag_DrawsSomething() => Assert.True(Clipper.Area(Draw(Israel)) > 1);

    [Fact]
    public void Emoji_AfterHebrewName_IsOnTheLeft()
    {
        var word = Poly.Bounds(Draw(Hebrew));
        var whole = Poly.Bounds(Draw(Hebrew + " " + Star));
        // Hebrew runs leftward from zero, so the emoji extends the left side and the right side stays.
        Assert.True(whole.MinX < word.MinX - Poly.U(5));
        Assert.Equal(Poly.Mm(word.MaxX), Poly.Mm(whole.MaxX), 0.5);
    }

    [Fact]
    public void Emoji_AfterEnglishName_IsOnTheRight()
    {
        var word = Poly.Bounds(Draw("Noa"));
        var whole = Poly.Bounds(Draw("Noa " + Star));
        Assert.True(whole.MaxX > word.MaxX + Poly.U(5));
        Assert.Equal(Poly.Mm(word.MinX), Poly.Mm(whole.MinX), 0.5);
    }

    [Theory]
    [InlineData(Star)]
    [InlineData(Heart)]
    [InlineData("\U0001F600")]
    [InlineData("☀")]
    [InlineData("\U0001F31F")]
    public void CommonEmoji_MostlySurviveThePrintThicknessRule(string emoji)
    {
        var letters = Draw(emoji);
        var opened = Poly.Inflate(Poly.Inflate(letters, -0.4), 0.4);
        var lost = 1 - Clipper.Area(opened) / Clipper.Area(letters);
        Assert.True(lost < 0.15, $"{lost:P1} is thinner than 0.8 mm");
    }

    [Fact]
    public void EmojiFont_IsNotInTheFontList() => Assert.DoesNotContain(BundledFonts.Names, n => n.Contains("Emoji"));

    public static IEnumerable<object[]> Choices => EmojiChoices.All.Select(c => new object[] { c.Emoji, c.Name });

    [Theory]
    [MemberData(nameof(Choices))]
    public void PickerEmoji_AreDrawnAndPrintable(string emoji, string name)
    {
        var letters = DrawWithEmojiFont(emoji);
        Assert.True(Clipper.Area(letters) > 1, name);
        var opened = Poly.Inflate(Poly.Inflate(letters, -0.4), 0.4);
        var lost = 1 - Clipper.Area(opened) / Clipper.Area(letters);
        Assert.True(lost < 0.25, $"{name}: {lost:P1} is thinner than 0.8 mm");
    }

    [Theory]
    [MemberData(nameof(Choices))]
    public void PickerEmoji_BuildClosedMeshes_OnEveryItem(string emoji, string name)
    {
        foreach (var item in ItemRegistry.All)
        {
            var settings = ItemSettings.DefaultsFor(item);
            var model = item.Build(TextOutliner.Build("Noa " + emoji, BundledFonts.Get("Rubik"), false, settings[Keys.FontSize], "-", false), settings);
            Assert.True(model.Base.IsManifold(), $"{name} base on {item.Name}: {model.Base.DescribeOpenEdges()}");
            Assert.True(model.Letters.IsManifold(), $"{name} letters on {item.Name}: {model.Letters.DescribeOpenEdges()}");
        }
    }

    [Theory]
    [InlineData("中")]
    [InlineData("Noa 中")]
    public void CharacterNoFontHas_IsReported(string text) =>
        Assert.Equal(new[] { "中" }, TextOutliner.CharactersNotDrawn(text, BundledFonts.Get("Rubik")));

    [Theory]
    [InlineData("Noa " + Star)]
    [InlineData(Family)]
    [InlineData(HeartWithMark)]
    [InlineData(ThumbsUpMediumSkin)]
    [InlineData("שָׁלוֹם")]
    public void DrawableText_ReportsNothing(string text) =>
        Assert.Empty(TextOutliner.CharactersNotDrawn(text, BundledFonts.Get("Rubik")));

    [Theory]
    [InlineData("Lobster", Hebrew, false)]
    [InlineData("Lobster", "Noa " + Star, true)]
    [InlineData("Rubik", Hebrew, true)]
    [InlineData("Gan CLM", "Noa", false)]
    public void DrawsOwnLetters_IgnoresEmoji(string font, string text, bool expected) =>
        Assert.Equal(expected, TextOutliner.DrawsOwnLetters(text, BundledFonts.Get(font)));

    [Fact]
    public void OutlineImage_FlipsToScreenDirection_WithFixedDecimals()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var image = OutlinePreview.ToImage(new Paths64 { Poly.Rect(1.5, 2, 11.5, 7) })!;
            Assert.Equal(10, image.Width, 2);
            Assert.Equal(5, image.Height, 2);
            Assert.Equal("1.5 -7 10 5", image.ViewBox);
            Assert.StartsWith("M1.5 -2L11.5 -2L11.5 -7L1.5 -7Z", image.Path);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void OutlineImage_OfNothing_IsNull() => Assert.Null(OutlinePreview.ToImage(new Paths64()));
}
