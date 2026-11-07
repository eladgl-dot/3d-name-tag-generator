using System.IO;
using System.Runtime.InteropServices;
using NameTagGenerator.Output;

namespace NameTagGenerator.Tests;

public class CrashLogTests
{
    static Exception Thrown(Exception error)
    {
        try { throw error; }
        catch (Exception caught) { return caught; }
    }

    [Fact]
    public void Describe_includes_time_source_message_and_stack()
    {
        var text = CrashLog.Describe(Thrown(new InvalidOperationException("boom")), new DateTime(2026, 11, 7, 16, 30, 5), "UI");
        Assert.Contains("2026-11-07 16:30:05", text);
        Assert.Contains("UI", text);
        Assert.Contains("boom", text);
        Assert.Contains(nameof(Thrown), text);
        Assert.Contains("Private memory", text);
    }

    [Fact]
    public void Append_adds_entries_in_order()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            CrashLog.Append(path, "first");
            CrashLog.Append(path, "second");
            var text = File.ReadAllText(path);
            Assert.True(text.IndexOf("first", StringComparison.Ordinal) < text.IndexOf("second", StringComparison.Ordinal));
            Assert.Contains("first", text);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Append_keeps_the_newest_entries_when_the_file_gets_too_big()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            CrashLog.Append(path, "oldest" + new string('x', 2000), maxBytes: 1000);
            CrashLog.Append(path, "newest", maxBytes: 1000);
            var text = File.ReadAllText(path);
            Assert.Contains("newest", text);
            Assert.DoesNotContain("oldest", text);
            Assert.True(new FileInfo(path).Length <= 1000);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Append_never_throws_when_the_file_cannot_be_written()
    {
        // A folder path cannot be written as a file; logging must not cause a second failure.
        CrashLog.Append(Path.GetTempPath(), "entry");
    }

    [Fact]
    public void IsOutOfMemory_recognises_the_forms_this_failure_takes()
    {
        Assert.True(CrashLog.IsOutOfMemory(new OutOfMemoryException()));
        Assert.True(CrashLog.IsOutOfMemory(new InvalidOperationException("wrapped", new OutOfMemoryException())));
        Assert.True(CrashLog.IsOutOfMemory(new AggregateException(new OutOfMemoryException())));
        Assert.True(CrashLog.IsOutOfMemory(new COMException("graphics", unchecked((int)0x8007000E))));
        Assert.False(CrashLog.IsOutOfMemory(new InvalidOperationException("other")));
        Assert.False(CrashLog.IsOutOfMemory(new COMException("other", unchecked((int)0x80004005))));
    }
}
