using Clipper2Lib;
using NameTagGenerator.Text;

namespace NameTagGenerator.Core.Tests;

/// <summary>A name mixing Hebrew and English: each word reads correctly, and the words sit where a text box would put them.</summary>
public class MixedDirectionTests
{
    const double AllowedMissing = 0.02;

    static Paths64 Outline(string text, string font) =>
        TextOutliner.Build(text, BundledFonts.All[font], false, 12, "", false).Letters;

    /// <summary>Draws the two end words alone and checks both are found, whole, at the two ends of the name.</summary>
    static void AssertEnds(string name, string font, string leftWord, string rightWord)
    {
        var whole = Outline(name, font);
        var left = Outline(leftWord, font);
        var right = Outline(rightWord, font);
        var bounds = Clipper.GetBounds(whole);

        var expected = Clipper.Union(
            Clipper.TranslatePaths(left, bounds.left - Clipper.GetBounds(left).left, 0),
            Clipper.TranslatePaths(right, bounds.right - Clipper.GetBounds(right).right, 0),
            FillRule.NonZero);
        var missing = Math.Abs(Clipper.Area(Clipper.Difference(expected, whole, FillRule.NonZero))) / Math.Abs(Clipper.Area(expected));

        Assert.True(missing < AllowedMissing, $"{missing:P1} of the expected letters are missing");
    }

    [Theory]
    [InlineData("Rubik", "כהן Dana", "Dana", "כהן")]
    [InlineData("Gan CLM", "כהן Dana", "Dana", "כהן")]
    [InlineData("Rubik", "Dana כהן", "Dana", "כהן")]
    [InlineData("Gan CLM", "Dana כהן", "Dana", "כהן")]
    [InlineData("Rubik", "כהן-Dana", "Dana", "כהן")]
    [InlineData("Rubik", "Dana-כהן", "Dana", "כהן")]
    public void Each_word_reads_correctly_in_a_mixed_name(string font, string name, string left, string right) =>
        AssertEnds(name, font, left, right);

    [Theory]
    [InlineData("Rubik", "כהן Noa Dana", "Noa", "כהן")]
    [InlineData("Rubik", "Dana נויה כהן", "Dana", "נויה")]
    [InlineData("Rubik", "כהן Noa 3 Dana", "Noa", "כהן")]
    public void English_words_in_Hebrew_keep_their_own_order_and_the_reverse(string font, string name, string left, string right) =>
        AssertEnds(name, font, left, right);

    [Theory]
    [InlineData("Rubik", "כהן דניאל", "דניאל", "כהן")]
    [InlineData("Rubik", "Noa Dana", "Noa", "Dana")]
    [InlineData("Rubik", "3 כהן", "כהן", "3")]
    public void Names_in_one_language_stay_as_they_were(string font, string name, string left, string right) =>
        AssertEnds(name, font, left, right);
}
