using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Clipper2Lib;
using NameTagGenerator.Geometry;

namespace NameTagGenerator.Text;

/// <summary>Letter outlines of a name. <see cref="Connector"/> holds a hollow connector kept apart from the letters.</summary>
public sealed record TextShape(Paths64 Letters, Paths64 Connector);

/// <summary>Turns a name into outlines using Windows text layout, which orders Hebrew correctly.</summary>
public static class TextOutliner
{
    // Same meaning of "size" as the old app: 18 means letters about 18 mm tall.
    const double EmPerSize = 100.0 / 72.0;
    const double FlattenToleranceMm = 0.03;
    static readonly CultureInfo Hebrew = CultureInfo.GetCultureInfo("he-IL");

    public static TextShape Build(string name, FontFamily family, bool bold, double sizeMm, string connector, bool hollow)
    {
        var typeface = new Typeface(family, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
        var rtl = IsRightToLeft(name);
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var hasConnector = !string.IsNullOrWhiteSpace(connector);

        var letters = new Paths64();
        var joins = new Paths64();
        var cursor = 0.0;

        void Place(string token, Paths64 target)
        {
            var flow = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            var text = new FormattedText(token, Hebrew, flow, typeface, sizeMm * EmPerSize, Brushes.Black, null, TextFormattingMode.Ideal, 1.0);
            // Right-to-left text extends leftward from its origin; left-to-right extends rightward.
            AddOutlines(text.BuildGeometry(new Point(cursor, 0)), target);
            var advance = text.WidthIncludingTrailingWhitespace;
            cursor += rtl ? -advance : advance;
        }

        for (var i = 0; i < words.Length; i++)
        {
            Place(words[i], letters);
            if (i >= words.Length - 1) continue;
            if (hasConnector) Place(connector, hollow ? joins : letters);
            else Place(" ", letters);
        }

        return new TextShape(Poly.Union(letters), Poly.Union(joins));
    }

    public static bool IsRightToLeft(string text)
    {
        foreach (var c in text)
        {
            if (c is >= '֐' and <= 'ࣿ' or >= 'יִ' and <= '﷿' or >= 'ﹰ' and <= '﻿') return true;
            if (char.IsLetter(c)) return false;
        }
        return false;
    }

    static void AddOutlines(System.Windows.Media.Geometry geometry, Paths64 target)
    {
        var flat = geometry.GetFlattenedPathGeometry(FlattenToleranceMm, ToleranceType.Absolute);
        foreach (var figure in flat.Figures)
        {
            var path = new Path64 { Convert(figure.StartPoint) };
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment line:
                        path.Add(Convert(line.Point));
                        break;
                    case PolyLineSegment poly:
                        foreach (var p in poly.Points) path.Add(Convert(p));
                        break;
                }
            }
            if (path.Count >= 3) target.Add(path);
        }
    }

    // Windows text coordinates grow downward; models grow upward.
    static Point64 Convert(Point p) => new(Poly.U(p.X), Poly.U(-p.Y));
}
