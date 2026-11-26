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

/// <summary>One part of a 3MF object, printed with the given filament (1 = first).</summary>
public sealed record ThreeMfPart(string Name, Mesh Mesh, int Filament);

/// <summary>An object placed at X, Y (mm, Bambu's bed coordinates) on a plate (0 = first). Its parts' coordinates are relative to X, Y.</summary>
public sealed record ThreeMfObject(string Name, IReadOnlyList<ThreeMfPart> Parts, double X, double Y, int Plate = 0);

/// <summary>
/// 3MF for Bambu Studio: objects made of parts, each part with its own filament so it prints in its own colour.
/// With plates, Metadata/model_settings.config lists which object goes on which Bambu plate.
/// </summary>
public static class ThreeMfWriter
{
    /// <summary>One object of two parts, base and letters, at the origin.</summary>
    public static void Write(Stream output, string name, Mesh baseMesh, Mesh letters) =>
        Write(output, name, new[] { new ThreeMfObject(name, new[] { new ThreeMfPart("Base", baseMesh, 1), new ThreeMfPart("Letters", letters, 2) }, 0, 0) });

    public static void Write(Stream output, string title, IReadOnlyList<ThreeMfObject> objects, bool plates = false)
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

        var c = CultureInfo.InvariantCulture;
        var model = new StringBuilder();
        var config = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<config>\n");
        model.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        model.Append("<model unit=\"millimeter\" xml:lang=\"en-US\" xmlns=\"http://schemas.microsoft.com/3dmanufacturing/core/2015/02\">\n");
        model.Append($" <metadata name=\"Title\">{Escape(title)}</metadata>\n <resources>\n");

        // Ids: each part, then the object holding them.
        var nextId = 1;
        var objectIds = new List<int>();
        foreach (var obj in objects)
        {
            var partIds = new List<int>();
            foreach (var part in obj.Parts)
            {
                partIds.Add(nextId);
                AppendMesh(model, nextId++, part.Name, part.Mesh);
            }
            var id = nextId++;
            objectIds.Add(id);
            model.Append($"  <object id=\"{id}\" type=\"model\" name=\"{Escape(obj.Name)}\">\n   <components>\n");
            foreach (var p in partIds) model.Append($"    <component objectid=\"{p}\"/>\n");
            model.Append("   </components>\n  </object>\n");

            config.Append($" <object id=\"{id}\">\n  <metadata key=\"name\" value=\"{Escape(obj.Name)}\"/>\n  <metadata key=\"extruder\" value=\"1\"/>\n");
            for (var i = 0; i < obj.Parts.Count; i++)
                config.Append($"  <part id=\"{partIds[i]}\" subtype=\"normal_part\">\n   <metadata key=\"name\" value=\"{Escape(obj.Parts[i].Name)}\"/>\n   <metadata key=\"extruder\" value=\"{obj.Parts[i].Filament}\"/>\n  </part>\n");
            config.Append(" </object>\n");
        }
        model.Append(" </resources>\n <build>\n");
        for (var i = 0; i < objects.Count; i++)
            model.Append(c, $"  <item objectid=\"{objectIds[i]}\" transform=\"1 0 0 0 1 0 0 0 1 {objects[i].X:0.####} {objects[i].Y:0.####} 0\"/>\n");
        model.Append(" </build>\n</model>\n");

        if (plates)
            foreach (var plate in objects.Select(o => o.Plate).Distinct().Order())
            {
                config.Append($" <plate>\n  <metadata key=\"plater_id\" value=\"{plate + 1}\"/>\n");
                for (var i = 0; i < objects.Count; i++)
                    if (objects[i].Plate == plate)
                        config.Append($"  <model_instance>\n   <metadata key=\"object_id\" value=\"{objectIds[i]}\"/>\n   <metadata key=\"instance_id\" value=\"0\"/>\n  </model_instance>\n");
                config.Append(" </plate>\n");
            }
        config.Append("</config>\n");

        Add(zip, "3D/3dmodel.model", model.ToString());
        Add(zip, "Metadata/model_settings.config", config.ToString());
    }

    // XML cannot hold control characters at all, so they are left out rather than escaped.
    static string Escape(string s) =>
        System.Security.SecurityElement.Escape(new string(s.Where(ch => ch >= ' ' || ch is '\t' or '\n' or '\r').ToArray()))!;

    static void AppendMesh(StringBuilder sb, int id, string name, Mesh mesh)
    {
        var c = CultureInfo.InvariantCulture;
        sb.Append($"  <object id=\"{id}\" type=\"model\" name=\"{Escape(name)}\">\n   <mesh>\n    <vertices>\n");
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
