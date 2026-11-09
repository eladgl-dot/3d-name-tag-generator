using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Text;

namespace NameTagGenerator.Pictures;

/// <summary>The picture settings the website remembers. The file itself is stored apart, because it can be large.</summary>
public sealed class PictureChoice
{
    public string? FileName { get; set; }
    public bool PlaceRight { get; set; } = true;
    public double HeightMm { get; set; } = 14;
    public double GapMm { get; set; } = 2;
    public int LevelPercent { get; set; } = 50;
    public bool Swap { get; set; }

    /// <summary>Brings damaged saved values back into a range that can build.</summary>
    public void Repair()
    {
        var defaults = new PictureChoice();
        HeightMm = double.IsFinite(HeightMm) ? Math.Clamp(HeightMm, 5, 200) : defaults.HeightMm;
        GapMm = double.IsFinite(GapMm) ? Math.Clamp(GapMm, 0, 50) : defaults.GapMm;
        LevelPercent = Math.Clamp(LevelPercent, 1, 99);
    }
}

/// <summary>A picture file after reading: a drawing, or pixels still to be turned into outlines.</summary>
public sealed class PictureSource
{
    readonly Paths64? _drawing;
    readonly byte[] _gray = Array.Empty<byte>();
    readonly int _width, _height;

    PictureSource(Paths64 drawing) => _drawing = drawing;

    PictureSource(byte[] gray, int width, int height) => (_gray, _width, _height) = (gray, width, height);

    /// <summary>False for photos and other pixel images, which have a black and white level to choose.</summary>
    public bool IsDrawing => _drawing is not null;

    /// <returns>Null when the file is damaged or has nothing to print.</returns>
    public static PictureSource? FromSvg(string text) => SvgReader.Read(text.TrimStart('﻿')) is { } drawing ? new PictureSource(drawing) : null;

    /// <param name="gray">One byte per pixel, row by row from the top, 0 is black.</param>
    public static PictureSource FromPixels(byte[] gray, int width, int height) => new(gray, width, height);

    public Paths64 Outlines(int levelPercent, bool swap) =>
        _drawing ?? MaskTracer.Trace(_gray, _width, _height, (byte)Math.Clamp(levelPercent * 255 / 100, 1, 254), swap);
}

/// <summary>A picture sized for printing, ready to stand beside every name.</summary>
public sealed class NamePicture
{
    // The print rule used for fonts: parts under 0.8 mm (two nozzle widths) vanish when shrunk and regrown by 0.4 mm.
    const double ThinnestHalfWidthMm = 0.4;

    readonly bool _placeRight;
    readonly double _gapMm;

    NamePicture(Paths64 outlines, double thinShareRemoved, bool placeRight, double gapMm, string key)
    {
        Outlines = outlines;
        ThinShareRemoved = thinShareRemoved;
        _placeRight = placeRight;
        _gapMm = gapMm;
        Key = key;
    }

    /// <summary>What gets printed, in millimetre units. Empty when the whole picture was too thin to print.</summary>
    public Paths64 Outlines { get; }

    /// <summary>How much of the picture was dropped for being too thin to print, from 0 to 1.</summary>
    public double ThinShareRemoved { get; }

    /// <summary>Changes whenever the printed result would, so finished tags are not reused by mistake.</summary>
    public string Key { get; }

    /// <param name="source">Outlines of any size.</param>
    /// <param name="key">Must differ for every different picture or setting.</param>
    public static NamePicture Fit(Paths64 source, double heightMm, bool placeRight, double gapMm, string key)
    {
        if (source.Count == 0) return new NamePicture(new Paths64(), 1, placeRight, gapMm, key);
        var box = Poly.Bounds(source);
        var factor = box.Height > 0 ? heightMm * Poly.Scale / box.Height : 1;
        var sized = new Paths64(source.Select(path =>
            new Path64(path.Select(p => new Point64((long)Math.Round((p.X - box.MinX) * factor), (long)Math.Round((p.Y - box.MinY) * factor))))));

        var whole = Poly.Union(sized);
        var kept = Poly.Clean(Poly.Inflate(Poly.Inflate(whole, -ThinnestHalfWidthMm), ThinnestHalfWidthMm));
        var wholeArea = Math.Abs(Clipper.Area(whole));
        var removed = wholeArea > 0 ? Math.Max(0, 1 - Math.Abs(Clipper.Area(kept)) / wholeArea) : 0;
        return new NamePicture(kept, removed, placeRight, gapMm, key);
    }

    public TextShape AddTo(TextShape text)
    {
        if (Outlines.Count == 0) return text;
        var name = new Paths64(text.Letters.Concat(text.Connector));
        if (name.Count == 0) return text;

        var nameBox = Poly.Bounds(name);
        var box = Poly.Bounds(Outlines);
        var gap = Poly.U(_gapMm);
        var dx = _placeRight ? nameBox.MaxX + gap - box.MinX : nameBox.MinX - gap - box.MaxX;
        var dy = (nameBox.MinY + nameBox.MaxY - box.MinY - box.MaxY) / 2;
        var moved = Clipper.TranslatePaths(Outlines, dx, dy);
        return new TextShape(new Paths64(text.Letters.Concat(moved)), text.Connector);
    }
}
