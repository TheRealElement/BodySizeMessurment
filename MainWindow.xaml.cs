
using HX  = HelixToolkit.Wpf.SharpDX;
using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BodySize.ClientSharpDX
{
    public partial class MainWindow : Window
    {
        private HX.MeshGeometryModel3D _mannequin;
        private HX.MeshGeometryModel3D _shorts;
        private HX.MeshGeometryModel3D _chestRing, _waistRing, _hipsRing, _neckRing;
        private HX.MeshGeometryModel3D _floor;
        private HX.PhongMaterial _skin;
        private HX.PhongMaterial _shortsMat;
        private HX.PhongMaterial _ringMat;
        private HX.EffectsManager _fx = new HX.DefaultEffectsManager();

        public MainWindow()
        {
            InitializeComponent();
            View.EffectsManager = _fx;
            BuildScene();
        }

        private void BuildScene()
        {
            _skin = new HX.PhongMaterial { DiffuseColor = new Color4(226/255f,229/255f,234/255f,1f), SpecularColor = new Color4(1f,1f,1f,1f), SpecularShininess = 80f };
            _shortsMat = new HX.PhongMaterial { DiffuseColor = new Color4(0.63f,0.63f,0.65f,1f) };
            _ringMat = new HX.PhongMaterial { DiffuseColor = new Color4(0.14f,0.67f,0.33f,1f) };

            _floor = new HX.MeshGeometryModel3D { Geometry = BuildDisc(0, 120, 180), Material = new HX.PhongMaterial { DiffuseColor = new Color4(0,0,0,0.06f) } };
            SceneRoot.Children.Add(_floor);

            string asset = FindAsset("mannequin.obj");
            if (!string.IsNullOrEmpty(asset))
            {
                var mesh = LoadObjAsMesh(asset);
                _mannequin = new HX.MeshGeometryModel3D { Geometry = mesh, Material = _skin };
                SceneRoot.Children.Add(_mannequin);
            }
            else { if (Hint != null) Hint.Visibility = System.Windows.Visibility.Visible; }

            string shorts = FindAsset("shorts.obj");
            if (!string.IsNullOrEmpty(shorts))
            {
                var smesh = LoadObjAsMesh(shorts);
                _shorts = new HX.MeshGeometryModel3D { Geometry = smesh, Material = _shortsMat };
                SceneRoot.Children.Add(_shorts);
            }

            _chestRing = new HX.MeshGeometryModel3D { Geometry = BuildTorus(126, 18, 0.8f, 180, 24), Material = _ringMat };
            _waistRing = new HX.MeshGeometryModel3D { Geometry = BuildTorus(106, 14, 0.8f, 180, 24), Material = _ringMat };
            _hipsRing  = new HX.MeshGeometryModel3D { Geometry = BuildTorus(92,  19, 0.8f, 180, 24), Material = _ringMat };
            _neckRing  = new HX.MeshGeometryModel3D { Geometry = BuildTorus(135,  6, 0.6f, 180, 24), Material = _ringMat };
            SceneRoot.Children.Add(_chestRing);
            SceneRoot.Children.Add(_waistRing);
            SceneRoot.Children.Add(_hipsRing);
            SceneRoot.Children.Add(_neckRing);
        }

        private HX.MeshGeometry3D BuildDisc(float y, float radius, int seg)
        {
            var positions = new List<Vector3>();
            var indices = new List<int>();
            positions.Add(new Vector3(0, y, 0));
            for (int i = 0; i < seg; i++)
            {
                float a = (float)(i * 2.0 * Math.PI / seg);
                positions.Add(new Vector3(radius * (float)Math.Cos(a), y, radius * (float)Math.Sin(a)));
            }
            for (int i = 1; i <= seg; i++)
            {
                int a = 0;
                int b = i;
                int c = i == seg ? 1 : i + 1;
                indices.Add(a); indices.Add(b); indices.Add(c);
            }
            var mesh = new HX.MeshGeometry3D
            {
                Positions = new HX.Vector3Collection(positions),
                Indices = new HX.IntCollection(indices)
            };
            mesh.Normals = ComputeFlatNormals(mesh);
            return mesh;
        }

        private HX.MeshGeometry3D BuildTorus(float y, float R, float r, int seg, int tubeSeg)
        {
            var pos = new List<Vector3>();
            var idx = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float u = (float)(i * 2.0 * Math.PI / seg);
                var cu = (float)Math.Cos(u);
                var su = (float)Math.Sin(u);
                for (int j = 0; j < tubeSeg; j++)
                {
                    float v = (float)(j * 2.0 * Math.PI / tubeSeg);
                    var cv = (float)Math.Cos(v);
                    var sv = (float)Math.Sin(v);
                    float x = (R + r * cv) * cu;
                    float z = (R + r * cv) * su;
                    float yy = y + r * sv;
                    pos.Add(new Vector3(x, yy, z));
                }
            }
            for (int i = 0; i < seg; i++)
            {
                int inext = (i + 1) % seg;
                for (int j = 0; j < tubeSeg; j++)
                {
                    int jnext = (j + 1) % tubeSeg;
                    int a = i * tubeSeg + j;
                    int b = inext * tubeSeg + j;
                    int c = i * tubeSeg + jnext;
                    int d = inext * tubeSeg + jnext;
                    idx.Add(a); idx.Add(b); idx.Add(c);
                    idx.Add(b); idx.Add(d); idx.Add(c);
                }
            }
            var mesh = new HX.MeshGeometry3D
            {
                Positions = new HX.Vector3Collection(pos),
                Indices = new HX.IntCollection(idx)
            };
            mesh.Normals = ComputeSmoothNormals(mesh);
            return mesh;
        }

        private HX.Vector3Collection ComputeFlatNormals(HX.MeshGeometry3D m)
        {
            var normals = new Vector3[m.Positions.Count];
            for (int i = 0; i < m.Indices.Count; i += 3)
            {
                int i0 = m.Indices[i];
                int i1 = m.Indices[i + 1];
                int i2 = m.Indices[i + 2];
                var p0 = m.Positions[i0];
                var p1 = m.Positions[i1];
                var p2 = m.Positions[i2];
                var n = Vector3.Cross(p1 - p0, p2 - p0);
                n.Normalize();
                normals[i0] += n;
                normals[i1] += n;
                normals[i2] += n;
            }
            for (int i = 0; i < normals.Length; i++) { var n = normals[i]; n.Normalize(); normals[i] = n; }
            return new HX.Vector3Collection(normals);
        }

        private HX.Vector3Collection ComputeSmoothNormals(HX.MeshGeometry3D m)
        {
            return ComputeFlatNormals(m);
        }

        private HX.MeshGeometry3D LoadObjAsMesh(string path)
        {
            var positions = new List<Vector3>();
            var indices = new List<int>();
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                var p = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p[0] == "v")
                {
                    positions.Add(new Vector3(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3])));
                }
                else if (p[0] == "f")
                {
                    var ids = new List<int>();
                    for (int i = 1; i < p.Length; i++)
                    {
                        var parts = p[i].Split('/');
                        int vi = int.Parse(parts[0]);
                        if (vi < 0) vi = positions.Count + 1 + vi;
                        ids.Add(vi - 1);
                    }
                    if (ids.Count == 3) { indices.AddRange(new[] { ids[0], ids[1], ids[2] }); }
                    else if (ids.Count == 4) { indices.AddRange(new[] { ids[0], ids[1], ids[2], ids[0], ids[2], ids[3] }); }
                }
            }
            var mesh = new HX.MeshGeometry3D
            {
                Positions = new HX.Vector3Collection(positions),
                Indices = new HX.IntCollection(indices)
            };
            mesh.Normals = ComputeSmoothNormals(mesh);
            return mesh;
        }

        private void BtnRecenter_Click(object sender, RoutedEventArgs e)
        {
            Cam.Position = new System.Windows.Media.Media3D.Point3D(0, 180, 380);
            Cam.LookDirection = new System.Windows.Media.Media3D.Vector3D(0, -60, -260);
            Cam.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PNG Image|*.png", FileName = "preview_dx.png" };
            if (dlg.ShowDialog() == true)
            {
                var rtb = new RenderTargetBitmap((int)View.ActualWidth, (int)View.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(View);
                using var fs = new FileStream(dlg.FileName, FileMode.Create);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                enc.Save(fs);
            }
        }

        private string FindAsset(string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new[]
            {
                System.IO.Path.Combine(baseDir, "Assets", fileName),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "Assets", fileName)),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "BodySize.Client", "Assets", fileName)),
                System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, fileName))
            };
            foreach (var p in candidates)
                if (System.IO.File.Exists(p)) return p;
            return null;
        }

        private void LoadMannequin(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
            {
                if (Hint != null) Hint.Visibility = System.Windows.Visibility.Visible;
                return;
            }
            if (Hint != null) Hint.Visibility = System.Windows.Visibility.Collapsed;
            var mesh = LoadObjAsMesh(path);
            if (_mannequin == null)
            {
                _mannequin = new HX.MeshGeometryModel3D { Material = _skin };
                SceneRoot.Children.Add(_mannequin);
            }
            _mannequin.Geometry = mesh;
        }

        private void LoadShorts(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;
            var mesh = LoadObjAsMesh(path);
            if (_shorts == null)
            {
                _shorts = new HX.MeshGeometryModel3D { Material = _shortsMat };
                SceneRoot.Children.Add(_shorts);
            }
            _shorts.Geometry = mesh;
        }

        private void BtnLoadModel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Wavefront OBJ (*.obj)|*.obj" };
            if (dlg.ShowDialog() == true) LoadMannequin(dlg.FileName);
        }

        private void BtnLoadShorts_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Wavefront OBJ (*.obj)|*.obj" };
            if (dlg.ShowDialog() == true) LoadShorts(dlg.FileName);
        }
    }
}
