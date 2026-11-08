using Xunit;

namespace NameTagGenerator.Tests;

public class AppInfoTests
{
    [Theory]
    [InlineData(0, 1, 2, 0, "v0.1.2")]
    [InlineData(1, 0, 0, 0, "v1.0.0")]
    [InlineData(2, 10, 3, 7, "v2.10.3")]
    public void Version_text_shows_three_numbers(int major, int minor, int build, int revision, string expected)
    {
        Assert.Equal(expected, AppInfo.VersionText(new Version(major, minor, build, revision)));
    }

    [Fact]
    public void Missing_version_shows_no_number()
    {
        Assert.Equal("", AppInfo.VersionText(null));
        Assert.Equal("3D name tag generator", AppInfo.TitleFor(null));
    }

    [Fact]
    public void Window_title_is_the_name_then_the_version_from_the_project_file()
    {
        var expected = "3D name tag generator " + AppInfo.VersionText(typeof(AppInfo).Assembly.GetName().Version);
        Assert.Equal(expected, AppInfo.WindowTitle);
        Assert.Matches(@"^3D name tag generator v\d+\.\d+\.\d+$", AppInfo.WindowTitle);
    }
}
