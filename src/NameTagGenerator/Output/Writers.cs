using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using NameTagGenerator.Geometry;

namespace NameTagGenerator.Output;

public static class StlWriter
{
    /// <summary>Writes a binary STL holding every mesh, each moved by its offset in millimetres.</summary>
    public static void Write(Stream output, IEnumerable<(Mesh Mesh, double X, double Y)> parts)
    {
        var list = parts.ToList();
        using var w = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        var header = new byte[80];
        Encoding.ASCII.GetBytes("3D name tag generator").CopyTo(header, 0);
        w.Write(header);
        w.Write((uint)list.Sum(p => p.Mesh.TriangleCount));

        foreach (var (mesh, dx, dy) in list)
        {
            Vector3 Point(int i)
            {
                var v = mesh.Vertices[i];
                return new Vector3((float)(Poly.Mm(v.X) + dx), (float)(Poly.Mm(v.Y) + dy), (float)Poly.Mm(v.Z));
            }

            for (var i = 0; i < mesh.Indices.Count; i += 3)
            {
                Vector3 a = Point(mesh.Indices[i]), b = Point(mesh.Indices[i + 1]), c = Point(mesh.Indices[i + 2]);
                var n = Vector3.Cross(b - a, c - a);
                n = n.LengthSquared() > 0 ? Vector3.Normalize(n) : Vector3.Zero;
                foreach (var v in new[] { n, a, b, c })
                {
                    w.Write(v.X);
                    w.Write(v.Y);
                    w.Write(v.Z);
                }
                w.Write((ushort)0);
            }
        }
    }
}

/// <summary>
/// 3MF holding one object made of two parts, base and letters, with each part
/// assigned its own filament so Bambu Studio can print them in two colours.
/// </summary>
public static class ThreeMfWriter
{
    const int BaseId = 1, LettersId = 2, ObjectId = 3;

    public static void Write(Stream output, string name, Mesh baseMesh, Mesh letters)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        Add(zip, "[Content_Types].xml",
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
             <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
             <Default Extension="model" ContentType="application/vnd.ms-package.3dmanufacturing-3dmodel+xml"/>
             <Default Extension="config" ContentType="text/xml"/>
            </Types>
            """);
        Add(zip, "_rels/.rels",
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
             <Relationship Target="/3D/3dmodel.model" Id="rel0" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"/>
            </Relationships>
            """);

        var safe = System.Security.SecurityElement.Escape(name);
        var model = new StringBuilder();
        model.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        model.Append("<model unit=\"millimeter\" xml:lang=\"en-US\" xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\">\n");
        model.Append($" <metadata name=\"Title\">{safe}</metadata>\n <resources>\n");
        AppendObject(model, BaseId, "Base", baseMesh);
        AppendObject(model, LettersId, "Letters", letters);
        model.Append($"  <object id=\"{ObjectId}\" type=\"model\" name=\"{safe}\">\n   <components>\n");
        model.Append($"    <component objectid=\"{BaseId}\"/>\n    <component objectid=\"{LettersId}\"/>\n");
        model.Append("   </components>\n  </object>\n </resources>\n");
        model.Append($" <build>\n  <item objectid=\"{ObjectId}\"/>\n </build>\n</model>\n");
        Add(zip, "3D/3dmodel.model", model.ToString());

        Add(zip, "Metadata/model_settings.config",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <config>
             <object id="{ObjectId}">
              <metadata key="name" value="{safe}"/>
              <metadata key="extruder" value="1"/>
              <part id="{BaseId}" subtype="normal_part">
               <metadata key="name" value="Base"/>
               <metadata key="extruder" value="1"/>
              </part>
              <part id="{LettersId}" subtype="normal_part">
               <metadata key="name" value="Letters"/>
               <metadata key="extruder" value="2"/>
              </part>
             </object>
            </config>
            """);
    }

    static void AppendObject(StringBuilder sb, int id, string name, Mesh mesh)
    {
        var c = CultureInfo.InvariantCulture;
        sb.Append($"  <object id=\"{id}\" type=\"model\" name=\"{name}\">\n   <mesh>\n    <vertices>\n");
        foreach (var v in mesh.Vertices)
            sb.Append(c, $"     <vertex x=\"{Poly.Mm(v.X):0.####}\" y=\"{Poly.Mm(v.Y):0.####}\" z=\"{Poly.Mm(v.Z):0.####}\"/>\n");
        sb.Append("    </vertices>\n    <triangles>\n");
        for (var i = 0; i < mesh.Indices.Count; i += 3)
            sb.Append($"     <triangle v1=\"{mesh.Indices[i]}\" v2=\"{mesh.Indices[i + 1]}\" v3=\"{mesh.Indices[i + 2]}\"/>\n");
        sb.Append("    </triangles>\n   </mesh>\n  </object>\n");
    }

    static void Add(ZipArchive zip, string path, string content)
    {
        using var stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        stream.Write(bytes);
    }
}
