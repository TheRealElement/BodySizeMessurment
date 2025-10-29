using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace BodySize.Client.Rendering
{
    public static class ObjImporter
    {
        public sealed class ObjModel
        {
            public MeshGeometry3D Mesh { get; set; } = new MeshGeometry3D();
        }

        public static ObjModel Load(string path)
        {
            var verts = new List<Point3D>();
            var positions = new List<Point3D>();
            var indices = new List<int>();
            var inv = CultureInfo.InvariantCulture;

            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                switch (p[0])
                {
                    case "v":
                        verts.Add(new Point3D(
                            double.Parse(p[1], inv),
                            double.Parse(p[2], inv),
                            double.Parse(p[3], inv)));
                        break;
                    case "f":
                        var ids = new List<int>();
                        for (int i = 1; i < p.Length; i++)
                        {
                            var parts = p[i].Split('/'); // v/vt/vn
                            int vi = int.Parse(parts[0], inv);
                            if (vi < 0) vi = verts.Count + 1 + vi;
                            ids.Add(vi - 1);
                        }
                        if (ids.Count == 3)
                        {
                            int a = AddVertex(verts[ids[0]], positions);
                            int b = AddVertex(verts[ids[1]], positions);
                            int c = AddVertex(verts[ids[2]], positions);
                            indices.Add(a); indices.Add(b); indices.Add(c);
                        }
                        else if (ids.Count == 4)
                        {
                            int a = AddVertex(verts[ids[0]], positions);
                            int b = AddVertex(verts[ids[1]], positions);
                            int c = AddVertex(verts[ids[2]], positions);
                            int d = AddVertex(verts[ids[3]], positions);
                            indices.Add(a); indices.Add(b); indices.Add(c);
                            indices.Add(a); indices.Add(c); indices.Add(d);
                        }
                        break;
                }
            }

            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection(positions),
                TriangleIndices = new Int32Collection(indices)
            };
            mesh.Normals = ComputeNormals(mesh);
            return new ObjModel { Mesh = mesh };
        }

        private static int AddVertex(Point3D v, List<Point3D> positions)
        {
            positions.Add(v);
            return positions.Count - 1;
        }

        private static Vector3DCollection ComputeNormals(MeshGeometry3D m)
        {
            var normals = new Vector3D[m.Positions.Count];
            for (int i = 0; i < m.TriangleIndices.Count; i += 3)
            {
                int i0 = m.TriangleIndices[i];
                int i1 = m.TriangleIndices[i + 1];
                int i2 = m.TriangleIndices[i + 2];
                var p0 = m.Positions[i0];
                var p1 = m.Positions[i1];
                var p2 = m.Positions[i2];
                var u = p1 - p0;
                var v = p2 - p0;
                var n = Vector3D.CrossProduct(u, v);
                normals[i0] += n;
                normals[i1] += n;
                normals[i2] += n;
            }
            var col = new Vector3DCollection(normals.Length);
            for (int i = 0; i < normals.Length; i++)
            {
                var n = normals[i];
                n.Normalize();
                col.Add(n);
            }
            return col;
        }
    }
}
