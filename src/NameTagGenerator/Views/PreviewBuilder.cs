using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Output;

namespace NameTagGenerator.Views;

/// <summary>Turns the same meshes that get exported into something the 3D view can draw.</summary>
public static class PreviewBuilder
{
    public const string DefaultBaseColour = "#1E90FF", DefaultLetterColour = "#F4F6F8";

    static readonly Material PlateMaterial = Make(Color.FromRgb(0x4A, 0x4F, 0x58));

    static Material Make(Color color)
    {
        var material = new DiffuseMaterial(new SolidColorBrush(color));
        material.Freeze();
        return material;
    }

    /// <summary>Reads "#RRGGBB". Anything else, such as a damaged settings file, gives the fallback.</summary>
    public static Color ParseColour(string? text, Color fallback)
    {
        if (text is not { Length: 7 } || text[0] != '#' || !int.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
            return fallback;
        return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    public static Model3D Build(IEnumerable<PlacedTag> tags, (double Width, double Depth)? showPlate, string? baseColour = null, string? letterColour = null)
    {
        var baseMaterial = Make(ParseColour(baseColour, ParseColour(DefaultBaseColour, Colors.Blue)));
        var letterMaterial = Make(ParseColour(letterColour, ParseColour(DefaultLetterColour, Colors.White)));
        var group = new Model3DGroup();
        foreach (var tag in tags)
        {
            var turn = TurnFor(tag.Model);
            group.Children.Add(ToModel(tag.Model.Base, tag.X, tag.Y, baseMaterial, turn));
            if (!tag.Model.Letters.IsEmpty) group.Children.Add(ToModel(tag.Model.Letters, tag.X, tag.Y, letterMaterial, turn));
        }
        if (showPlate is { } plate) group.Children.Add(Plate(plate.Width, plate.Depth));
        group.Freeze();
        return group;
    }

    // Half a turn around the item's own middle, so an item that prints name side down still shows its name. Files are not affected.
    static Func<Point3D, Point3D> TurnFor(ItemModel model)
    {
        if (!model.PreviewFlipped) return p => p;
        var box = Poly.Bounds(model.BaseArea);
        var doubleMiddleX = Poly.Mm(box.MinX + box.MaxX);
        var top = Poly.Mm(model.Base.Vertices.Max(v => v.Z));
        return p => new Point3D(doubleMiddleX - p.X, p.Y, top - p.Z);
    }

    static GeometryModel3D ToModel(Mesh mesh, double dx, double dy, Material material, Func<Point3D, Point3D> turn)
    {
        // Each triangle gets its own corners: that gives crisp, flat-shaded faces.
        var positions = new Point3DCollection(mesh.Indices.Count);
        var indices = new Int32Collection(mesh.Indices.Count);
        foreach (var i in mesh.Indices)
        {
            var v = mesh.Vertices[i];
            indices.Add(positions.Count);
            var p = turn(new Point3D(Poly.Mm(v.X), Poly.Mm(v.Y), Poly.Mm(v.Z)));
            positions.Add(new Point3D(p.X + dx, p.Y + dy, p.Z));
        }
        positions.Freeze();
        indices.Freeze();
        var geometry = new MeshGeometry3D { Positions = positions, TriangleIndices = indices };
        geometry.Freeze();
        return new GeometryModel3D(geometry, material);
    }

    static GeometryModel3D Plate(double width, double depth)
    {
        const double z = -0.2;
        var geometry = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(-width / 2, -depth / 2, z), new(width / 2, -depth / 2, z), new(width / 2, depth / 2, z), new(-width / 2, depth / 2, z),
            },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 },
        };
        geometry.Freeze();
        return new GeometryModel3D(geometry, PlateMaterial);
    }
}
