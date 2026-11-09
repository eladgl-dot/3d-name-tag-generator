using System.Globalization;
using System.Numerics;
using Clipper2Lib;
using NameTagGenerator.Geometry;
using SixLabors.Fonts;

namespace NameTagGenerator.Text;

/// <summary>Letter outlines of a name. <see cref="Connector"/> holds a hollow connector kept apart from the letters.</summary>
public sealed record TextShape(Paths64 Letters, Paths64 Connector);

/// <summary>Turns a name into outlines. Runs anywhere .NET runs, including in a browser.</summary>
public static class TextOutliner
{
    // Same meaning of "size" as the old app: 18 means letters about 18 mm tall.
    const double EmPerSize = 100.0 / 72.0;
    const double FlattenToleranceMm = 0.03;

    // Fonts with no bold face: Windows thickens each side by 1/100 em and widens each letter by 1/50 em.
    const double SimulatedBoldPerSideEm = 0.01;
    const double SimulatedBoldWideningEm = 0.02;

    public static TextShape Build(string name, OutlineFont font, bool bold, double sizeMm, string connector, bool hollow)
    {
        var emSize = sizeMm * EmPerSize;
        var rendered = font.Create((float)emSize, bold);
        var simulateBold = bold && font.SimulatesBold;
        var rtl = IsRightToLeft(name);
        var words = InDisplayOrder(name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries), rtl);
        var hasConnector = !string.IsNullOrWhiteSpace(connector);

        var letters = new Paths64();
        var joins = new Paths64();
        var cursor = 0.0;

        void Place(string token, Paths64 target)
        {
            var options = new TextOptions(rendered)
            {
                Dpi = 72,
                // Each piece follows its own letters. Forcing the name's direction would draw an English word backwards in a Hebrew name.
                TextDirection = TextDirection.Auto,
                HintingMode = HintingMode.None,
                KerningMode = KerningMode.Standard,
                FallbackFontFamilies = new[] { BundledFonts.Fallback.Family },
            };
            var letterWidening = simulateBold ? emSize * SimulatedBoldWideningEm : 0;
            var advance = TextMeasurer.MeasureAdvance(token, options).Width + letterWidening * new StringInfo(token).LengthInTextElements;
            var outlines = new OutlineCollector(FlattenToleranceMm);
            TextRenderer.RenderTextTo(outlines, token, options);
            // Right-to-left text extends leftward from the cursor; left-to-right extends rightward.
            var dx = rtl ? cursor - advance : cursor;
            target.AddRange(Clipper.TranslatePaths(outlines.SpreadLetters(letterWidening), Poly.U(dx), 0));
            cursor += rtl ? -advance : advance;
        }

        for (var i = 0; i < words.Length; i++)
        {
            Place(words[i], letters);
            if (i >= words.Length - 1) continue;
            if (hasConnector) Place(connector, hollow ? joins : letters);
            else Place(" ", letters);
        }

        var thicken = simulateBold ? emSize * SimulatedBoldPerSideEm : 0;
        return new TextShape(Finish(letters, thicken), Finish(joins, thicken));
    }

    /// <summary>
    /// Words run in the name's direction, but neighbouring words in the other language keep their own reading order,
    /// so they swap places, as in a text box. A number between two of them stays with them.
    /// </summary>
    static string[] InDisplayOrder(string[] words, bool rtl)
    {
        bool IsOtherLanguage(string word) => word.Any(char.IsLetter) && IsRightToLeft(word) != rtl;

        var ordered = (string[])words.Clone();
        for (var start = 0; start < words.Length; start++)
        {
            if (!IsOtherLanguage(words[start])) continue;
            var end = start;
            for (var next = start + 1; next < words.Length; next++)
            {
                if (IsOtherLanguage(words[next])) end = next;
                else if (words[next].Any(char.IsLetter)) break;
            }
            Array.Reverse(ordered, start, end - start + 1);
            start = end;
        }
        return ordered;
    }

    static Paths64 Finish(Paths64 outlines, double thickenMm)
    {
        var joined = Poly.Union(outlines);
        return thickenMm > 0 ? Poly.Inflate(joined, thickenMm) : joined;
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

    /// <summary>Collects glyph outlines as flat polygons. Text coordinates grow downward; models grow upward.</summary>
    sealed class OutlineCollector(double toleranceMm) : IGlyphRenderer
    {
        readonly List<Paths64> _letters = new();
        Paths64 _current = new();
        Path64 _path = new();
        Vector2 _last;

        static Point64 Convert(Vector2 p) => new(Poly.U(p.X), Poly.U(-p.Y));

        void Finish()
        {
            if (_path.Count >= 3) _current.Add(_path);
            _path = new Path64();
        }

        /// <summary>All outlines, with each letter moved right by its place from the left times <paramref name="widening"/>.</summary>
        public Paths64 SpreadLetters(double widening)
        {
            var result = new Paths64();
            var place = 0;
            foreach (var letter in _letters.Where(l => l.Count > 0).OrderBy(l => l.Min(p => p.Min(v => v.X))))
                result.AddRange(Clipper.TranslatePaths(letter, Poly.U(widening * place++), 0));
            return result;
        }

        public void MoveTo(Vector2 point)
        {
            Finish();
            _path.Add(Convert(point));
            _last = point;
        }

        public void LineTo(Vector2 point)
        {
            _path.Add(Convert(point));
            _last = point;
        }

        public void QuadraticBezierTo(Vector2 c, Vector2 end)
        {
            var start = _last;
            var bend = (start - 2 * c + end).Length();
            var steps = Steps(bend / 4);
            for (var i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var u = 1 - t;
                _path.Add(Convert(u * u * start + 2 * u * t * c + t * t * end));
            }
            _last = end;
        }

        public void CubicBezierTo(Vector2 c1, Vector2 c2, Vector2 end)
        {
            var start = _last;
            var bend = Math.Max((start - 2 * c1 + c2).Length(), (c1 - 2 * c2 + end).Length());
            var steps = Steps(bend * 3 / 4);
            for (var i = 1; i <= steps; i++)
            {
                var t = (float)i / steps;
                var u = 1 - t;
                _path.Add(Convert(u * u * u * start + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * end));
            }
            _last = end;
        }

        // A curve split into n parts strays from its chords by at most error / n².
        int Steps(double error) => Math.Max(1, (int)Math.Ceiling(Math.Sqrt(error / toleranceMm)));

        public void BeginFigure() { }
        public void EndFigure() => Finish();
        public void BeginText(in FontRectangle bounds) { }
        public void EndText() => Finish();

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
        {
            // Marks such as niqqud belong to their letter, so they move together.
            var isMark = CharUnicodeInfo.GetUnicodeCategory(parameters.CodePoint.Value) == UnicodeCategory.NonSpacingMark;
            if (!isMark || _letters.Count == 0) _letters.Add(new Paths64());
            _current = _letters[^1];
            return true;
        }

        public void EndGlyph() => Finish();
        public TextDecorations EnabledDecorations() => TextDecorations.None;
        public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness) { }
    }
}
