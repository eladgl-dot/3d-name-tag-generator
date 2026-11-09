using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Clipper2Lib;

namespace NameTagGenerator.Pictures;

/// <summary>Reads the drawing in an SVG file as outlines. White parts cut away what is beneath them, as they look on screen.</summary>
public static class SvgReader
{
    const int CurveSteps = 16;
    const double ArcStep = Math.PI / 24;
    // Limits far above any sensible name tag picture. The browser has one thread, so a hostile file must not be able to freeze it.
    const int MaxPoints = 200_000;
    const int MaxShapes = 5_000;
    const int MaxNesting = 200;
    const int MaxTransformLength = 4_096;
    const double OutputSize = 100_000;
    // Colours this close to white read as paper, not as part of the drawing.
    const double PaperBrightness = 0.85;

    static readonly HashSet<string> NotDrawn = new()
    {
        "defs", "clipPath", "mask", "symbol", "title", "desc", "metadata", "style", "pattern", "linearGradient", "radialGradient", "marker", "script",
    };

    enum Paint { None, Dark, Light }

    readonly record struct Style(Paint Fill, Paint Stroke, double StrokeWidth, bool EvenOdd);

    readonly record struct Point(double X, double Y);

    sealed record SubPath(List<Point> Points, bool Closed);

    sealed record Shape(List<SubPath> SubPaths, Style Style, double StrokeWidth);

    sealed class PointBudget
    {
        public int Used;
    }

    readonly record struct Matrix(double A, double B, double C, double D, double E, double F)
    {
        public static readonly Matrix Identity = new(1, 0, 0, 1, 0, 0);
        public Point Apply(Point p) => new(A * p.X + C * p.Y + E, B * p.X + D * p.Y + F);
        public double Scale => Math.Sqrt(Math.Abs(A * D - B * C));
        public Matrix Then(Matrix m) => new(
            A * m.A + C * m.B, B * m.A + D * m.B,
            A * m.C + C * m.D, B * m.C + D * m.D,
            A * m.E + C * m.F + E, B * m.E + D * m.F + F);
    }

    /// <returns>Null when the file is damaged or has nothing to print.</returns>
    public static Paths64? Read(string svg)
    {
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(svg), settings);
            var root = XDocument.Load(reader).Root;
            if (root is null || root.Name.LocalName != "svg") return null;

            var shapes = new List<Shape>();
            var rules = StyleRules(root);
            Walk(root, new Style(Paint.Dark, Paint.None, 1, false), Matrix.Identity, rules, shapes, new PointBudget(), 0);
            return Paint_(shapes);
        }
        catch (Exception ex) when (ex is XmlException or FormatException or OverflowException or InvalidOperationException)
        {
            return null;
        }
    }

    static Dictionary<string, Dictionary<string, string>> StyleRules(XElement root)
    {
        var rules = new Dictionary<string, Dictionary<string, string>>();
        foreach (var style in root.Descendants().Where(e => e.Name.LocalName == "style"))
        {
            var css = WithoutComments(style.Value);
            var at = 0;
            while (true)
            {
                var open = css.IndexOf('{', at);
                if (open < 0) break;
                var close = css.IndexOf('}', open);
                if (close < 0) break;
                foreach (var selector in css[at..open].Split(','))
                {
                    var key = selector.Trim();
                    if (!rules.TryGetValue(key, out var declarations)) rules[key] = declarations = new Dictionary<string, string>();
                    foreach (var (name, value) in Declarations(css[(open + 1)..close])) declarations[name] = value;
                }
                at = close + 1;
            }
        }
        return rules;
    }

    static string WithoutComments(string css)
    {
        var text = new StringBuilder();
        var at = 0;
        while (at < css.Length)
        {
            var open = css.IndexOf("/*", at, StringComparison.Ordinal);
            if (open < 0)
            {
                text.Append(css, at, css.Length - at);
                break;
            }
            text.Append(css, at, open - at);
            var close = css.IndexOf("*/", open + 2, StringComparison.Ordinal);
            if (close < 0) break;
            at = close + 2;
        }
        return text.ToString();
    }

    static IEnumerable<(string Name, string Value)> Declarations(string text) =>
        text.Split(';').Select(d => d.Split(':', 2)).Where(p => p.Length == 2).Select(p => (p[0].Trim(), p[1].Trim()));

    static void Walk(XElement element, Style inherited, Matrix parent, Dictionary<string, Dictionary<string, string>> rules, List<Shape> shapes, PointBudget budget, int nesting)
    {
        if (nesting > MaxNesting || shapes.Count >= MaxShapes) throw new InvalidOperationException("The picture is too complicated.");
        var tag = element.Name.LocalName;
        if (NotDrawn.Contains(tag)) return;

        var declared = new Dictionary<string, string>();
        foreach (var attribute in element.Attributes()) declared[attribute.Name.LocalName] = attribute.Value;
        if (rules.TryGetValue(tag, out var forTag)) foreach (var (k, v) in forTag) declared[k] = v;
        foreach (var name in Attr(element, "class").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            if (rules.TryGetValue("." + name, out var forClass)) foreach (var (k, v) in forClass) declared[k] = v;
        foreach (var (k, v) in Declarations(Attr(element, "style"))) declared[k] = v;

        if (declared.GetValueOrDefault("display") == "none" || declared.GetValueOrDefault("visibility") == "hidden") return;

        var style = inherited;
        if (declared.TryGetValue("fill", out var fill)) style = style with { Fill = ParsePaint(fill) };
        if (declared.TryGetValue("stroke", out var stroke)) style = style with { Stroke = ParsePaint(stroke) };
        if (declared.TryGetValue("stroke-width", out var width)) style = style with { StrokeWidth = Length(width) };
        if (declared.TryGetValue("fill-rule", out var rule)) style = style with { EvenOdd = rule == "evenodd" };

        var matrix = declared.TryGetValue("transform", out var transform) ? parent.Then(ParseTransform(transform)) : parent;

        if (tag is "svg" or "g" or "a" or "switch")
        {
            foreach (var child in element.Elements()) Walk(child, style, matrix, rules, shapes, budget, nesting + 1);
            return;
        }

        var d = AsPath(element, tag);
        if (d is null) return;
        var subPaths = PathReader.Read(d, budget);
        foreach (var path in subPaths)
            for (var i = 0; i < path.Points.Count; i++)
                path.Points[i] = matrix.Apply(path.Points[i]);
        shapes.Add(new Shape(subPaths, style, style.StrokeWidth * matrix.Scale));
    }

    static string Attr(XElement e, string name) => e.Attribute(name)?.Value ?? "";

    static double Num(XElement e, string name) => e.Attribute(name) is { } a ? Length(a.Value) : 0;

    static double Length(string text)
    {
        var match = Regex.Match(text, @"^\s*[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?");
        return match.Success ? double.Parse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture) : 0;
    }

    static string F(FormattableString s) => FormattableString.Invariant(s);

    /// <summary>Every shape is drawn as a path, so only the path reader has to deal with curves.</summary>
    static string? AsPath(XElement e, string tag)
    {
        switch (tag)
        {
            case "path":
                return Attr(e, "d");
            case "rect":
            {
                double x = Num(e, "x"), y = Num(e, "y"), w = Num(e, "width"), h = Num(e, "height");
                if (w <= 0 || h <= 0) return null;
                var rx = e.Attribute("rx") is not null ? Num(e, "rx") : Num(e, "ry");
                var ry = e.Attribute("ry") is not null ? Num(e, "ry") : Num(e, "rx");
                rx = Math.Min(rx, w / 2);
                ry = Math.Min(ry, h / 2);
                if (rx <= 0 || ry <= 0) return F($"M{x} {y}H{x + w}V{y + h}H{x}Z");
                return F($"M{x + rx} {y}H{x + w - rx}A{rx} {ry} 0 0 1 {x + w} {y + ry}V{y + h - ry}A{rx} {ry} 0 0 1 {x + w - rx} {y + h}H{x + rx}A{rx} {ry} 0 0 1 {x} {y + h - ry}V{y + ry}A{rx} {ry} 0 0 1 {x + rx} {y}Z");
            }
            case "circle":
                return Ellipse(Num(e, "cx"), Num(e, "cy"), Num(e, "r"), Num(e, "r"));
            case "ellipse":
                return Ellipse(Num(e, "cx"), Num(e, "cy"), Num(e, "rx"), Num(e, "ry"));
            case "line":
                return F($"M{Num(e, "x1")} {Num(e, "y1")}L{Num(e, "x2")} {Num(e, "y2")}");
            case "polygon":
                return "M" + Attr(e, "points") + "Z";
            case "polyline":
                return "M" + Attr(e, "points");
            default:
                return null;
        }
    }

    static string? Ellipse(double cx, double cy, double rx, double ry) =>
        rx <= 0 || ry <= 0 ? null : F($"M{cx - rx} {cy}A{rx} {ry} 0 1 1 {cx + rx} {cy}A{rx} {ry} 0 1 1 {cx - rx} {cy}Z");

    static Paint ParsePaint(string value)
    {
        value = value.Trim().ToLowerInvariant();
        if (value is "none" or "transparent") return Paint.None;
        return Brightness(value) >= PaperBrightness ? Paint.Light : Paint.Dark;
    }

    /// <summary>From 0 to 1. Anything that can't be read, such as a gradient, counts as dark, so it still prints.</summary>
    static double Brightness(string colour)
    {
        if (colour == "white") return 1;
        double r, g, b;
        var rgb = Regex.Match(colour, @"^rgb\(\s*([\d.]+)%?[\s,]+([\d.]+)%?[\s,]+([\d.]+)%?");
        if (rgb.Success)
        {
            var max = colour.Contains('%') ? 100.0 : 255.0;
            r = double.Parse(rgb.Groups[1].Value, CultureInfo.InvariantCulture) / max;
            g = double.Parse(rgb.Groups[2].Value, CultureInfo.InvariantCulture) / max;
            b = double.Parse(rgb.Groups[3].Value, CultureInfo.InvariantCulture) / max;
        }
        else if (Regex.IsMatch(colour, "^#[0-9a-f]{3}$"))
        {
            (r, g, b) = (Hex(colour[1..2]) * 17 / 255, Hex(colour[2..3]) * 17 / 255, Hex(colour[3..4]) * 17 / 255);
        }
        else if (Regex.IsMatch(colour, "^#[0-9a-f]{6}$"))
        {
            (r, g, b) = (Hex(colour[1..3]) / 255, Hex(colour[3..5]) / 255, Hex(colour[5..7]) / 255);
        }
        else
        {
            return 0;
        }
        return 0.299 * r + 0.587 * g + 0.114 * b;
    }

    static double Hex(string text) => int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    static Matrix ParseTransform(string text)
    {
        if (text.Length > MaxTransformLength) throw new FormatException("Transform too long");
        var result = Matrix.Identity;
        foreach (Match m in Regex.Matches(text, @"([a-zA-Z]+)\s*\(([^)]*)\)"))
        {
            var a = Regex.Matches(m.Groups[2].Value, @"[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?")
                .Select(x => double.Parse(x.Value, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
            double Arg(int i, double fallback) => i < a.Length ? a[i] : fallback;
            var radians = Arg(0, 0) * Math.PI / 180;
            Matrix step;
            switch (m.Groups[1].Value)
            {
                case "matrix" when a.Length >= 6:
                    step = new Matrix(a[0], a[1], a[2], a[3], a[4], a[5]);
                    break;
                case "translate":
                    step = new Matrix(1, 0, 0, 1, Arg(0, 0), Arg(1, 0));
                    break;
                case "scale":
                    step = new Matrix(Arg(0, 1), 0, 0, Arg(1, Arg(0, 1)), 0, 0);
                    break;
                case "rotate":
                    var turn = new Matrix(Math.Cos(radians), Math.Sin(radians), -Math.Sin(radians), Math.Cos(radians), 0, 0);
                    step = a.Length >= 3 ? new Matrix(1, 0, 0, 1, a[1], a[2]).Then(turn).Then(new Matrix(1, 0, 0, 1, -a[1], -a[2])) : turn;
                    break;
                case "skewX":
                    step = new Matrix(1, 0, Math.Tan(radians), 1, 0, 0);
                    break;
                case "skewY":
                    step = new Matrix(1, Math.Tan(radians), 0, 1, 0, 0);
                    break;
                default:
                    continue;
            }
            result = result.Then(step);
        }
        return result;
    }

    /// <summary>Paints the shapes in file order, as a screen would, so a later white shape removes what is under it.</summary>
    static Paths64? Paint_(List<Shape> shapes)
    {
        var all = shapes.SelectMany(s => s.SubPaths).SelectMany(p => p.Points).ToList();
        if (all.Count == 0 || all.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) return null;

        double minX = all.Min(p => p.X), minY = all.Min(p => p.Y), maxX = all.Max(p => p.X), maxY = all.Max(p => p.Y);
        var span = Math.Max(maxX - minX, maxY - minY);
        if (!double.IsFinite(span)) return null;
        if (span <= 0) span = 1;
        var scale = OutputSize / span;
        Point64 Convert(Point p) => new((long)Math.Round((p.X - minX) * scale), (long)Math.Round((maxY - p.Y) * scale));

        var drawing = new Paths64();
        // Neighbouring shapes of one colour are joined together first: adding them one at a time gets slow with many shapes.
        var waiting = new Paths64();
        var waitingPaint = Paint.None;

        void Flush()
        {
            if (waiting.Count > 0)
                drawing = waitingPaint == Paint.Dark
                    ? Clipper.Union(drawing, waiting, FillRule.NonZero)
                    : Clipper.Difference(drawing, waiting, FillRule.NonZero);
            waiting = new Paths64();
        }

        void Add(Paths64 area, Paint paint)
        {
            if (paint != waitingPaint) Flush();
            waitingPaint = paint;
            waiting.AddRange(area);
        }

        foreach (var shape in shapes)
        {
            if (shape.Style.Fill != Paint.None)
            {
                var outlines = new Paths64(shape.SubPaths.Where(p => p.Points.Count >= 3).Select(p => new Path64(p.Points.Select(Convert))));
                var rule = shape.Style.EvenOdd ? FillRule.EvenOdd : FillRule.NonZero;
                Add(Clipper.Union(outlines, rule), shape.Style.Fill);
            }
            if (shape.Style.Stroke != Paint.None && shape.StrokeWidth > 0)
            {
                var half = Math.Min(shape.StrokeWidth / 2 * scale, OutputSize);
                var closed = new Paths64(shape.SubPaths.Where(p => p.Closed && p.Points.Count >= 2).Select(p => new Path64(p.Points.Select(Convert))));
                var open = new Paths64(shape.SubPaths.Where(p => !p.Closed && p.Points.Count >= 2).Select(p => new Path64(p.Points.Select(Convert))));
                var band = Clipper.Union(
                    Clipper.InflatePaths(closed, half, JoinType.Round, EndType.Joined),
                    Clipper.InflatePaths(open, half, JoinType.Round, EndType.Round),
                    FillRule.NonZero);
                Add(band, shape.Style.Stroke);
            }
        }
        Flush();
        return drawing.Count == 0 ? null : drawing;
    }

    /// <summary>Follows the commands of an SVG path. A mistake ends the path, keeping what came before it, as browsers do.</summary>
    static class PathReader
    {
        public static List<SubPath> Read(string d, PointBudget budget)
        {
            var result = new List<SubPath>();
            var scan = new Scanner(d);
            var points = new List<Point>();
            Point current = default, start = default, control = default;
            var command = '\0';
            var previous = '\0';

            void Finish(bool closed)
            {
                if (points.Count > 0) result.Add(new SubPath(points, closed));
                budget.Used += points.Count;
                points = new List<Point>();
            }

            void Begin()
            {
                if (points.Count == 0) points.Add(current);
            }

            void LineTo(Point p)
            {
                Begin();
                points.Add(p);
                current = p;
            }

            void CubicTo(Point c1, Point c2, Point end)
            {
                Begin();
                var from = current;
                for (var i = 1; i <= CurveSteps; i++)
                {
                    var t = (double)i / CurveSteps;
                    var u = 1 - t;
                    points.Add(new Point(
                        u * u * u * from.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * end.X,
                        u * u * u * from.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * end.Y));
                }
                current = end;
            }

            void QuadTo(Point c, Point end)
            {
                Begin();
                var from = current;
                for (var i = 1; i <= CurveSteps; i++)
                {
                    var t = (double)i / CurveSteps;
                    var u = 1 - t;
                    points.Add(new Point(u * u * from.X + 2 * u * t * c.X + t * t * end.X, u * u * from.Y + 2 * u * t * c.Y + t * t * end.Y));
                }
                current = end;
            }

            void ArcTo(double rx, double ry, double degrees, bool large, bool sweep, Point end)
            {
                var from = current;
                if (rx == 0 || ry == 0) { LineTo(end); return; }
                if (from == end) return;
                Begin();
                rx = Math.Abs(rx);
                ry = Math.Abs(ry);
                var phi = degrees * Math.PI / 180;
                double cos = Math.Cos(phi), sin = Math.Sin(phi);
                double dx = (from.X - end.X) / 2, dy = (from.Y - end.Y) / 2;
                double x1 = cos * dx + sin * dy, y1 = -sin * dx + cos * dy;
                var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
                if (lambda > 1)
                {
                    rx *= Math.Sqrt(lambda);
                    ry *= Math.Sqrt(lambda);
                }
                var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
                var numerator = rx * rx * ry * ry - denominator;
                var factor = (large == sweep ? -1 : 1) * Math.Sqrt(Math.Max(0, numerator / denominator));
                double cxp = factor * rx * y1 / ry, cyp = -factor * ry * x1 / rx;
                double cx = cos * cxp - sin * cyp + (from.X + end.X) / 2, cy = sin * cxp + cos * cyp + (from.Y + end.Y) / 2;
                var a1 = Math.Atan2((y1 - cyp) / ry, (x1 - cxp) / rx);
                var a2 = Math.Atan2((-y1 - cyp) / ry, (-x1 - cxp) / rx);
                var delta = a2 - a1;
                if (sweep && delta < 0) delta += 2 * Math.PI;
                if (!sweep && delta > 0) delta -= 2 * Math.PI;
                var steps = Math.Max(4, (int)Math.Ceiling(Math.Abs(delta) / ArcStep));
                for (var i = 1; i < steps; i++)
                {
                    var a = a1 + delta * i / steps;
                    points.Add(new Point(cos * rx * Math.Cos(a) - sin * ry * Math.Sin(a) + cx, sin * rx * Math.Cos(a) + cos * ry * Math.Sin(a) + cy));
                }
                points.Add(end);
                current = end;
            }

            try
            {
                while (scan.More)
                {
                    if (budget.Used + points.Count > MaxPoints) throw new InvalidOperationException("The picture is too complicated.");
                    if (scan.NextIsCommand) command = scan.Command();
                    else if (command == '\0') break;

                    var relative = char.IsLower(command);
                    double X() => scan.Number() + (relative ? current.X : 0);
                    double Y() => scan.Number() + (relative ? current.Y : 0);
                    Point Xy()
                    {
                        var x = X();
                        var y = Y();
                        return new Point(x, y);
                    }

                    var kind = char.ToUpperInvariant(command);
                    switch (kind)
                    {
                        case 'M':
                            Finish(false);
                            current = start = Xy();
                            Begin();
                            command = relative ? 'l' : 'L';
                            break;
                        case 'L':
                            LineTo(Xy());
                            break;
                        case 'H':
                            LineTo(new Point(X(), current.Y));
                            break;
                        case 'V':
                            LineTo(new Point(current.X, Y()));
                            break;
                        case 'C':
                        {
                            var c1 = Xy();
                            var c2 = Xy();
                            var end = Xy();
                            CubicTo(c1, c2, end);
                            control = c2;
                            break;
                        }
                        case 'S':
                        {
                            var c1 = previous is 'C' or 'S' ? new Point(2 * current.X - control.X, 2 * current.Y - control.Y) : current;
                            var c2 = Xy();
                            var end = Xy();
                            CubicTo(c1, c2, end);
                            control = c2;
                            break;
                        }
                        case 'Q':
                        {
                            var c = Xy();
                            var end = Xy();
                            QuadTo(c, end);
                            control = c;
                            break;
                        }
                        case 'T':
                        {
                            var c = previous is 'Q' or 'T' ? new Point(2 * current.X - control.X, 2 * current.Y - control.Y) : current;
                            QuadTo(c, Xy());
                            control = c;
                            break;
                        }
                        case 'A':
                        {
                            var rx = scan.Number();
                            var ry = scan.Number();
                            var angle = scan.Number();
                            var large = scan.Flag();
                            var sweep = scan.Flag();
                            ArcTo(rx, ry, angle, large, sweep, Xy());
                            break;
                        }
                        case 'Z':
                            Finish(true);
                            current = start;
                            command = '\0';
                            break;
                        default:
                            throw new FormatException($"Unknown path command {command}");
                    }
                    previous = kind;
                }
            }
            catch (FormatException)
            {
                // Keep what was drawn up to the mistake.
            }
            Finish(false);
            return result;
        }

        sealed class Scanner(string text)
        {
            int _at;

            void SkipSpace()
            {
                while (_at < text.Length && (char.IsWhiteSpace(text[_at]) || text[_at] == ',')) _at++;
            }

            public bool More
            {
                get
                {
                    SkipSpace();
                    return _at < text.Length;
                }
            }

            public bool NextIsCommand
            {
                get
                {
                    SkipSpace();
                    return _at < text.Length && char.IsLetter(text[_at]);
                }
            }

            public char Command() => text[_at++];

            public bool Flag()
            {
                SkipSpace();
                if (_at >= text.Length || text[_at] is not ('0' or '1')) throw new FormatException("Expected a flag");
                return text[_at++] == '1';
            }

            public double Number()
            {
                SkipSpace();
                var from = _at;
                if (_at < text.Length && text[_at] is '+' or '-') _at++;
                var digits = 0;
                while (_at < text.Length && char.IsAsciiDigit(text[_at])) { _at++; digits++; }
                if (_at < text.Length && text[_at] == '.')
                {
                    _at++;
                    while (_at < text.Length && char.IsAsciiDigit(text[_at])) { _at++; digits++; }
                }
                if (digits == 0) throw new FormatException("Expected a number");
                if (_at < text.Length && text[_at] is 'e' or 'E')
                {
                    var mark = _at++;
                    if (_at < text.Length && text[_at] is '+' or '-') _at++;
                    var exponent = _at;
                    while (_at < text.Length && char.IsAsciiDigit(text[_at])) _at++;
                    if (exponent == _at) _at = mark;
                }
                return double.Parse(text.AsSpan(from, _at - from), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
