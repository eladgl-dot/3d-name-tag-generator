using NameTagGenerator.Items;
using NameTagGenerator.Output;
using NameTagGenerator.Text;

namespace NameTagGenerator.Core.Tests;

public class FontLibraryTests
{
    const string YoursSuffix = " (yours)";

    static byte[] FontFile(string family)
    {
        foreach (var resource in BundledFonts.ResourceNames)
        {
            using var stream = BundledFonts.OpenResource(resource);
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            if (OutlineFont.FromStreams(copy).Name == family) return copy.ToArray();
        }
        throw new InvalidOperationException("No bundled font called " + family);
    }

    public static TheoryData<string, byte[]> DamagedFiles()
    {
        var random = new byte[5000];
        new Random(7).NextBytes(random);
        var font = FontFile("Rubik");
        var headerOnly = font[..64];
        var half = font[..(font.Length / 2)];
        var zeros = new byte[5000];
        zeros[1] = 1;
        return new TheoryData<string, byte[]>
        {
            { "empty", Array.Empty<byte>() },
            { "random", random },
            { "header only", headerOnly },
            { "first half of a font", half },
            { "font signature then zeros", zeros },
        };
    }

    [Fact]
    public void An_added_font_is_listed_and_found()
    {
        var library = new FontLibrary();

        var name = library.Add(FontFile("Gan CLM"));

        Assert.NotNull(name);
        Assert.Equal(new[] { name }, library.AddedNames);
        Assert.True(library.Exists(name));
        Assert.NotSame(BundledFonts.Fallback, library.Get(name));
    }

    [Fact]
    public void An_added_font_draws_a_name()
    {
        var library = new FontLibrary();
        var name = library.Add(FontFile("Rubik"))!;
        var keychain = ItemRegistry.All.Single(i => i.Id == "keychain");
        var settings = ItemSettings.DefaultsFor(keychain);
        settings.Font = name;

        var result = new TagBuilder(library.Get).Build(new[] { "נויה" }, keychain, settings, new ArrangeSettings(), CancellationToken.None);

        Assert.Single(result.Tags);
        Assert.False(result.Tags[0].Model.Letters.IsEmpty);
    }

    [Fact]
    public void A_font_named_like_a_built_in_one_keeps_both()
    {
        var library = new FontLibrary();

        var name = library.Add(FontFile("Rubik"));

        Assert.Equal("Rubik" + YoursSuffix, name);
        Assert.Same(BundledFonts.All["Rubik"], library.Get("Rubik"));
        Assert.NotSame(BundledFonts.All["Rubik"], library.Get(name!));
    }

    [Fact]
    public void The_same_font_added_twice_is_listed_once_with_the_same_name()
    {
        var library = new FontLibrary();

        var first = library.Add(FontFile("Gan CLM"));
        var second = library.Add(FontFile("Gan CLM"));

        Assert.Equal(first, second);
        Assert.Single(library.AddedNames);
    }

    [Theory]
    [MemberData(nameof(DamagedFiles))]
    public void A_damaged_file_is_refused_and_changes_nothing(string kind, byte[] file)
    {
        var library = new FontLibrary();
        var good = library.Add(FontFile("Gan CLM"))!;

        var name = library.Add(file);

        Assert.True(name is null, kind);
        Assert.Equal(new[] { good }, library.AddedNames);
    }

    [Fact]
    public void Built_in_fonts_exist_and_unknown_ones_do_not()
    {
        var library = new FontLibrary();

        Assert.True(library.Exists("Rubik"));
        Assert.False(library.Exists("No Such Font"));
    }

    [Fact]
    public void An_unknown_font_draws_with_the_fallback()
    {
        Assert.Same(BundledFonts.Fallback, new FontLibrary().Get("No Such Font"));
    }
}
