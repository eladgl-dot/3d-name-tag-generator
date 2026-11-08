using System.IO;
using System.IO.Compression;
using Clipper2Lib;
using NameTagGenerator.Geometry;
using NameTagGenerator.Items;
using NameTagGenerator.Output;

namespace NameTagGenerator.Tests;

public class PreviewMeshTests
{
    // One triangle spanning x 0..10, at the given height, on a base area of the same width.
    static PlacedTag Tag(double x, double y, bool flipped = false)
    {
        var mesh = new Mesh();
        mesh.Triangle(mesh.Vertex(Poly.U(0), Poly.U(0), Poly.U(0)), mesh.Vertex(Poly.U(10), Poly.U(0), Poly.U(2)), mesh.Vertex(Poly.U(0), Poly.U(4), Poly.U(0)));
        var area = new Paths64 { new Path64 { new(Poly.U(0), Poly.U(0)), new(Poly.U(10), Poly.U(0)), new(Poly.U(10), Poly.U(4)), new(Poly.U(0), Poly.U(4)) } };
        return new PlacedTag("a", new ItemModel(mesh, new Mesh(), area, area, null, 1, flipped), x, y);
    }

    static float[] Floats(byte[] bytes)
    {
        var result = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    static int[] Ints(byte[] bytes)
    {
        var result = new int[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    [Fact]
    public void Positions_are_in_millimetres_and_moved_to_the_tag_place()
    {
        var part = PreviewMesh.Base(new[] { Tag(100, 50) });
        Assert.Equal(new[] { 100f, 50f, 0f, 110f, 50f, 2f, 100f, 54f, 0f }, Floats(part.Positions));
    }

    [Fact]
    public void Second_tag_indices_continue_after_the_first_tag()
    {
        var part = PreviewMesh.Base(new[] { Tag(0, 0), Tag(20, 0) });
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, Ints(part.Indices));
    }

    [Fact]
    public void Item_printed_name_down_is_turned_over_around_its_own_middle()
    {
        var part = PreviewMesh.Base(new[] { Tag(100, 0, flipped: true) });
        // x mirrors around the item's middle (5), z flips within its height (2), y stays. The place is added afterwards.
        Assert.Equal(new[] { 110f, 0f, 2f, 100f, 0f, 0f, 110f, 4f, 2f }, Floats(part.Positions));
    }

    [Fact]
    public void Letters_of_a_turned_item_use_the_height_of_its_base()
    {
        var tag = Tag(0, 0, flipped: true);
        var letters = new Mesh();
        letters.Triangle(letters.Vertex(0, 0, Poly.U(1)), letters.Vertex(Poly.U(1), 0, Poly.U(1)), letters.Vertex(0, Poly.U(1), Poly.U(1)));
        var withLetters = tag with { Model = tag.Model with { Letters = letters } };
        var z = Floats(PreviewMesh.Letters(new[] { withLetters }).Positions)[2];
        Assert.Equal(1f, z);
    }

    [Fact]
    public void Stream_export_writes_a_3mf_with_both_parts()
    {
        using var stream = new MemoryStream();
        Exporter.Write3mf(stream, "t", new[] { Tag(0, 0) });
        stream.Position = 0;
        using var zip = new ZipArchive(stream);
        Assert.Contains(zip.Entries, e => e.FullName.EndsWith(".model", StringComparison.Ordinal));
    }

    [Fact]
    public void Stream_export_writes_an_stl_with_every_triangle()
    {
        using var stream = new MemoryStream();
        Exporter.WriteStl(stream, new[] { Tag(0, 0), Tag(20, 0) });
        Assert.Equal(84 + 2 * 50, stream.Length);
    }
}
