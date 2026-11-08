namespace NameTagGenerator;

/// <summary>The app's name and version. The number comes from Version in the project file.</summary>
public static class AppInfo
{
    public const string Name = "3D name tag generator";

    public static string WindowTitle { get; } = TitleFor(typeof(AppInfo).Assembly.GetName().Version);

    public static string TitleFor(Version? version)
    {
        var text = VersionText(version);
        return text.Length == 0 ? Name : $"{Name} {text}";
    }

    public static string VersionText(Version? version) =>
        version is null ? "" : $"v{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
}
