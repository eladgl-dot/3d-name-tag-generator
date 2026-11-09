using Clipper2Lib;

namespace NameTagGenerator.Pictures;

/// <summary>Turns a greyscale image into outlines around its dark parts.</summary>
public static class MaskTracer
{
    const long UnitsPerPixel = 100;
    // Within one pixel of the true edge, which is far below what a printer shows, and it removes the pixel stairs.
    const double SmoothingUnits = UnitsPerPixel * 1.0;

    /// <param name="gray">One byte per pixel, row by row from the top, 0 is black.</param>
    /// <param name="level">Pixels darker than this count as part of the shape.</param>
    /// <param name="swap">Keep the light parts instead of the dark ones.</param>
    public static Paths64 Trace(byte[] gray, int width, int height, byte level, bool swap)
    {
        // One rectangle per run of dark pixels; joining them gives outlines, with holes, and needs no edge following.
        var runs = new Paths64();
        for (var y = 0; y < height; y++)
        {
            var top = (height - y) * UnitsPerPixel;
            var bottom = top - UnitsPerPixel;
            for (var x = 0; x < width; x++)
            {
                if (!IsShape(gray[y * width + x], level, swap)) continue;
                var start = x;
                while (x + 1 < width && IsShape(gray[y * width + x + 1], level, swap)) x++;
                var left = start * UnitsPerPixel;
                var right = (x + 1) * UnitsPerPixel;
                runs.Add(new Path64 { new(left, bottom), new(right, bottom), new(right, top), new(left, top) });
            }
        }
        return runs.Count == 0 ? new Paths64() : Clipper.SimplifyPaths(Clipper.Union(runs, FillRule.NonZero), SmoothingUnits);
    }

    static bool IsShape(byte value, byte level, bool swap) => value < level != swap;
}
