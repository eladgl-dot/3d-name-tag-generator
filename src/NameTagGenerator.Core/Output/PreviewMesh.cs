using NameTagGenerator.Geometry;
using NameTagGenerator.Items;

namespace NameTagGenerator.Output;

/// <summary>A mesh as raw bytes for a browser: 32-bit float positions in millimetres, 32-bit triangle indices.</summary>
public sealed record PreviewPart(byte[] Positions, byte[] Indices);

/// <summary>Flattens placed tags into one mesh per colour for the website's 3D view. Files are not affected.</summary>
public static class PreviewMesh
{
    public static PreviewPart Base(IReadOnlyCollection<PlacedTag> tags) => Flatten(tags, m => m.Base);

    public static PreviewPart Letters(IReadOnlyCollection<PlacedTag> tags) => Flatten(tags, m => m.Letters);

    static PreviewPart Flatten(IReadOnlyCollection<PlacedTag> tags, Func<ItemModel, Mesh> pick)
    {
        var positions = new float[tags.Sum(t => pick(t.Model).Vertices.Count) * 3];
        var indices = new int[tags.Sum(t => pick(t.Model).Indices.Count)];
        int p = 0, n = 0;

        foreach (var tag in tags)
        {
            var mesh = pick(tag.Model);
            var firstVertex = p / 3;
            var turn = TurnFor(tag.Model);
            foreach (var v in mesh.Vertices)
            {
                var (x, z) = turn(Poly.Mm(v.X), Poly.Mm(v.Z));
                positions[p++] = (float)(x + tag.X);
                positions[p++] = (float)(Poly.Mm(v.Y) + tag.Y);
                positions[p++] = (float)z;
            }
            foreach (var i in mesh.Indices) indices[n++] = i + firstVertex;
        }
        return new PreviewPart(ToBytes(positions), ToBytes(indices));
    }

    // Half a turn around the item's own middle, so an item that prints name side down still shows its name.
    static Func<double, double, (double X, double Z)> TurnFor(ItemModel model)
    {
        if (!model.PreviewFlipped) return (x, z) => (x, z);
        var box = Poly.Bounds(model.BaseArea);
        var doubleMiddleX = Poly.Mm(box.MinX + box.MaxX);
        var top = Poly.Mm(model.Base.Vertices.Max(v => v.Z));
        return (x, z) => (doubleMiddleX - x, top - z);
    }

    static byte[] ToBytes(Array values)
    {
        var bytes = new byte[Buffer.ByteLength(values)];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
