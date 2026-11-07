using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NameTagGenerator.Text;

namespace NameTagGenerator.Output;

/// <summary>Keeps a record of unexpected errors, so a rare failure can be diagnosed afterwards.</summary>
public static class CrashLog
{
    const int OutOfMemoryHResult = unchecked((int)0x8007000E);

    public static string FilePath => Path.Combine(FontCatalog.AppDataFolder, "crash.log");

    public static void Record(Exception error, string source) => Append(FilePath, Describe(error, DateTime.Now, source));

    public static string Describe(Exception error, DateTime when, string source)
    {
        using var process = Process.GetCurrentProcess();
        var text = new StringBuilder();
        text.AppendLine($"=== {when:yyyy-MM-dd HH:mm:ss}  {source}");
        text.AppendLine($"Open since {process.StartTime:yyyy-MM-dd HH:mm:ss}");
        text.AppendLine($"Private memory {process.PrivateMemorySize64 >> 20} MB, managed {GC.GetTotalMemory(false) >> 20} MB, handles {process.HandleCount}");
        text.AppendLine(error.ToString());
        return text.ToString();
    }

    /// <summary>Adds an entry. When the file would pass the limit, the oldest text goes. Never throws.</summary>
    public static void Append(string path, string entry, long maxBytes = 262144)
    {
        try
        {
            var existing = File.Exists(path) ? File.ReadAllText(path) : "";
            var text = existing + entry + Environment.NewLine;
            // Characters are at most 3 UTF-8 bytes here, so this many always fit the limit.
            var keep = (int)(maxBytes / 3);
            if (Encoding.UTF8.GetByteCount(text) > maxBytes && text.Length > keep) text = text[^keep..];
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Failing to write the log must not become a second failure.
        }
    }

    public static bool IsOutOfMemory(Exception error)
    {
        if (error is OutOfMemoryException || (error is COMException && error.HResult == OutOfMemoryHResult)) return true;
        if (error is AggregateException many) return many.InnerExceptions.Any(IsOutOfMemory);
        return error.InnerException is { } inner && IsOutOfMemory(inner);
    }
}
