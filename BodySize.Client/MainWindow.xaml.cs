
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BodySize.Client.Rendering;
using System.Windows.Media.Media3D;

namespace BodySize.Client
{
    public partial class MainWindow : Window
    {
        private string? _front, _back, _left, _right;

        public ICommand ResetCommand { get; }
        public ICommand AnalyzeCommand { get; }

        // 3D fields
        private Model3DGroup _sceneRoot = new Model3DGroup();
        private PerspectiveCamera _cam = new PerspectiveCamera();
        private double _yaw = 0, _pitch = -8 * Math.PI / 180.0, _distance = 320;
        private GeometryModel3D _mannequinModel;
        private GeometryModel3D _shortsModel;
        private MeshGeometry3D _shortsBaseMesh;
        private MeshGeometry3D _mannequinBaseMesh;
        private Model3DGroup _ringsGroup = new Model3DGroup();
        private Point _lastPos;
        private bool _isDragging;

        public MainWindow()
        {
            ResetCommand = new RoutedUICommand("Reset", nameof(ResetCommand), typeof(MainWindow));
            AnalyzeCommand = new RoutedUICommand("Analyze", nameof(AnalyzeCommand), typeof(MainWindow));

            InitializeComponent();

            this.Loaded += MainWindow_Loaded;

            CommandBindings.Add(new CommandBinding(ResetCommand, (_, __) => ResetAll()));
            CommandBindings.Add(new CommandBinding(AnalyzeCommand, (_, __) => BtnAnalyze_Click(this, new RoutedEventArgs())));
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // keyboard shortcuts
            this.InputBindings.Add(new KeyBinding(ResetCommand, new KeyGesture(Key.R, ModifierKeys.Control)));
            this.InputBindings.Add(new KeyBinding(AnalyzeCommand, new KeyGesture(Key.Enter)));

            // set up 3D camera + lights
            SetupCameraAndLights();
            EnsureFloor();
            try
            {
                var assetPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "mannequin.obj");
                var obj = ObjImporter.Load(assetPath);
                _mannequinBaseMesh = obj.Mesh;
                _mannequinModel = new GeometryModel3D(_mannequinBaseMesh,
                new MaterialGroup {
                    Children = {
                        new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(226,229,234))),
                        new SpecularMaterial(new SolidColorBrush(Color.FromRgb(240,240,240)), 60)
                    }
                });
                _sceneRoot.Children.Add(_mannequinModel);
                _sceneRoot.Children.Add(_ringsGroup);
                // load shorts
                var shortsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "shorts.obj");
                if (File.Exists(shortsPath))
                {
                    var shorts = ObjImporter.Load(shortsPath);
                    _shortsBaseMesh = shorts.Mesh;
                    _shortsModel = new GeometryModel3D(_shortsBaseMesh,
                        new MaterialGroup { Children = { new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(150,152,158))), new SpecularMaterial(new SolidColorBrush(Color.FromRgb(210,210,210)), 40) } });
                    _sceneRoot.Children.Add(_shortsModel);
                }
            }
            catch { /* asset missing – ignore for now */ }
        }

        // === Image picking and previews ===
        private void BtnFront_Click(object sender, RoutedEventArgs e) => SetImage(ref _front, ImgFront, LblFront);
        private void BtnBack_Click(object sender, RoutedEventArgs e) => SetImage(ref _back, ImgBack, LblBack);
        private void BtnLeft_Click(object sender, RoutedEventArgs e) => SetImage(ref _left, ImgLeft, LblLeft);
        private void BtnRight_Click(object sender, RoutedEventArgs e) => SetImage(ref _right, ImgRight, LblRight);

        private void Front_Drop(object sender, DragEventArgs e) { if (TryGetDropPath(e, out var p)) LoadPreview(ref _front, p, ImgFront, LblFront); }
        private void Back_Drop(object sender, DragEventArgs e) { if (TryGetDropPath(e, out var p)) LoadPreview(ref _back, p, ImgBack, LblBack); }
        private void Left_Drop(object sender, DragEventArgs e) { if (TryGetDropPath(e, out var p)) LoadPreview(ref _left, p, ImgLeft, LblLeft); }
        private void Right_Drop(object sender, DragEventArgs e) { if (TryGetDropPath(e, out var p)) LoadPreview(ref _right, p, ImgRight, LblRight); }

        private void ClearFront_Click(object sender, RoutedEventArgs e) => ClearOne(ref _front, ImgFront, LblFront);
        private void ClearBack_Click(object sender, RoutedEventArgs e) => ClearOne(ref _back, ImgBack, LblBack);
        private void ClearLeft_Click(object sender, RoutedEventArgs e) => ClearOne(ref _left, ImgLeft, LblLeft);
        private void ClearRight_Click(object sender, RoutedEventArgs e) => ClearOne(ref _right, ImgRight, LblRight);

        private bool TryGetDropPath(DragEventArgs e, out string path)
        {
            path = "";
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && File.Exists(files[0]))
                {
                    var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    {
                        path = files[0];
                        return true;
                    }
                }
            }
            return false;
        }

        private void SetImage(ref string? field, Image img, TextBlock label)
        {
            var dlg = new OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png" };
            if (dlg.ShowDialog() == true) LoadPreview(ref field, dlg.FileName, img, label);
        }

        private void LoadPreview(ref string? field, string path, Image img, TextBlock label)
        {
            field = path;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            img.Source = bmp;
            label.Visibility = Visibility.Collapsed;
        }

        private void ClearOne(ref string? field, Image img, TextBlock label)
        {
            field = null;
            img.Source = null;
            label.Visibility = Visibility.Visible;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) => ResetAll();

        private void ResetAll()
        {
            ClearOne(ref _front, ImgFront, LblFront);
            ClearOne(ref _back, ImgBack, LblBack);
            ClearOne(ref _left, ImgLeft, LblLeft);
            ClearOne(ref _right, ImgRight, LblRight);
            ListResults.Items.Clear();
            ListResults3D.Items.Clear();
            TxtHeight.Text = "175";
            CmbGender.SelectedIndex = 0;
        }

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TxtHeight.Text, out var height))
            {
                MessageBox.Show("Height must be a number in cm.");
                return;
            }
            var gender = (CmbGender.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "male";
            try
            {
                var json = await SendToApi(height, gender);
                ShowResults(json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> SendToApi(int height, string gender)
        {
            using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5189/") };
            using var form = new MultipartFormDataContent();

            if (_front != null) form.Add(new StreamContent(File.OpenRead(_front)) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "Front", System.IO.Path.GetFileName(_front));
            if (_back != null) form.Add(new StreamContent(File.OpenRead(_back)) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "Back", System.IO.Path.GetFileName(_back));
            if (_left != null) form.Add(new StreamContent(File.OpenRead(_left)) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "Left", System.IO.Path.GetFileName(_left));
            if (_right != null) form.Add(new StreamContent(File.OpenRead(_right)) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "Right", System.IO.Path.GetFileName(_right));

            form.Add(new StringContent(height.ToString()), "HeightCm");
            form.Add(new StringContent(gender), "Gender");

            var res = await client.PostAsync("api/analyze/analyze", form);
            var content = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new Exception(content);
            return content;
        }

        private void ShowResults(string json)
        {
            var o = JObject.Parse(json);
            ListResults.Items.Clear();
            ListResults3D.Items.Clear();

            void add(string label, string path, string suffix = " cm")
            {
                var val = o.SelectToken(path)?.ToString();
                if (val != null) { var t = new TextBlock { Text = $"{label}: {val}{suffix}" }; ListResults.Items.Add(t); ListResults3D.Items.Add(new TextBlock { Text = t.Text }); }
            }

            add("Chest", "chestCircumferenceCm");
            add("Waist", "waistCircumferenceCm");
            add("Hips", "hipCircumferenceCm");
            add("Shoulder width", "shoulderWidthCm");
            add("Torso length (est.)", "estimatedTorsoLengthCm");
            ListResults.Items.Add(new Separator());
            ListResults.Items.Add(new TextBlock { Text = $"Top size (EU): {o["topSizeEU"]}" });
            ListResults.Items.Add(new TextBlock { Text = $"Bottom size (EU): {o["bottomSizeEU"]}" });

            // Update 3D
            double height = double.Parse(TxtHeight.Text);
            double chest = double.Parse(o["chestCircumferenceCm"].ToString());
            double waist = double.Parse(o["waistCircumferenceCm"].ToString());
            double hips = double.Parse(o["hipCircumferenceCm"].ToString());
            double shoulder = double.Parse(o["shoulderWidthCm"].ToString());
            Update3DPreview(height, chest, waist, hips, shoulder);
            // Tabs.SelectedIndex = 1;  // no auto-switch; use Show 3D button
        }

        // ===== 3D =====
        private void SetupCameraAndLights()
        {
            _cam.FieldOfView = 45;
            View3D.Camera = _cam;
            UpdateCamera();

            // Lights
            var lights = new Model3DGroup();
            lights.Children.Add(new AmbientLight(Color.FromRgb(180, 180, 180)));
            lights.Children.Add(new DirectionalLight(Color.FromRgb(255, 255, 255), new Vector3D(-0.2, -1, -0.3)));
            lights.Children.Add(new DirectionalLight(Color.FromRgb(200, 200, 200), new Vector3D(0.5, -0.3, 0.7)));

            View3D.Children.Clear();
            View3D.Children.Add(new ModelVisual3D { Content = lights });
            View3D.Children.Add(new ModelVisual3D { Content = _sceneRoot });
        }

        private void UpdateCamera()
        {
            double x = _distance * Math.Cos(_pitch) * Math.Sin(_yaw);
            double y = _distance * Math.Sin(_pitch);
            double z = _distance * Math.Cos(_pitch) * Math.Cos(_yaw);
            _cam.Position = new Point3D(x, y + 80, z + 100);
            _cam.LookDirection = new Vector3D(-x, -y - 80, -z - 100);
            _cam.UpDirection = new Vector3D(0, 1, 0);
            _cam.NearPlaneDistance = 0.5;
            _cam.FarPlaneDistance = 5000;
        }

        

private void Update3DPreview(double heightCm, double chestC, double waistC, double hipC, double shoulderW)
{
    if (_mannequinBaseMesh == null || _mannequinModel == null) return;

    var morphed = MannequinMorpher.MorphToMeasurements(_mannequinBaseMesh, heightCm, chestC, waistC, hipC);
    _mannequinModel.Geometry = morphed;

    // Scale height to match
    double baseH = _mannequinBaseMesh.Bounds.SizeY;
    if (baseH > 1)
    {
        double s = heightCm / baseH;
        _mannequinModel.Transform = new ScaleTransform3D(s, s, s);
    }

        // Also morph shorts to waist/hip bands if present
    if (_shortsBaseMesh != null && _shortsModel != null)
    {
        var shortsM = MannequinMorpher.MorphToMeasurements(_shortsBaseMesh, heightCm, chestC, waistC, hipC);
        _shortsModel.Geometry = shortsM;
        if (baseH > 1)
        {
            double s2 = heightCm / baseH;
            _shortsModel.Transform = new ScaleTransform3D(s2, s2, s2);
        }
    }

    // Rebuild measurement rings
    _ringsGroup.Children.Clear();
            if (_heightArrow == null)
            {
                _heightArrow = new GeometryModel3D(BuildArrow(0, heightCm), new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(120,120,120))));
                _sceneRoot.Children.Add(_heightArrow);
            }
            else { _heightArrow.Geometry = BuildArrow(0, heightCm); }
    var blue = new DiffuseMaterial(new SolidColorBrush(Color.FromRgb(36, 171, 84)));

    double chestR = chestC / (2 * Math.PI);
    double waistR = waistC / (2 * Math.PI);
    double hipR   = hipC   / (2 * Math.PI);
    double yChest = heightCm * 0.72;
    double yWaist = heightCm * 0.60;
    double yHip   = heightCm * 0.50;

    _ringsGroup.Children.Add(new GeometryModel3D(BuildTorus(yChest, chestR, 0.8, 128), blue));
    _ringsGroup.Children.Add(new GeometryModel3D(BuildTorus(yWaist, waistR, 0.8, 128), blue));
    _ringsGroup.Children.Add(new GeometryModel3D(BuildTorus(yHip,   hipR,   0.8, 128), blue));
            double yNeck = heightCm * 0.78; double neckR = chestR * 0.33; _ringsGroup.Children.Add(new GeometryModel3D(BuildTorus(yNeck, neckR, 0.6, 128), blue));
}

        
// === 3D helpers ===
private GeometryModel3D Capsule(Point3D p1, Point3D p2, double r, Brush brush)
{
    var mat = new DiffuseMaterial(brush);
    var group = new Model3DGroup();
    group.Children.Add(new GeometryModel3D(BuildCylinder(p1, p2, r), mat));
    group.Children.Add(new GeometryModel3D(BuildSphere(p1, r), mat));
    group.Children.Add(new GeometryModel3D(BuildSphere(p2, r), mat));
    return new GeometryModel3D { Geometry = ((GeometryModel3D)group.Children[0]).Geometry, Material = mat, BackMaterial = mat };
}

        // Geometry helpers
        private MeshGeometry3D BuildCylinder(Point3D p1, Point3D p2, double r, int seg = 32)
        {
            var m = new MeshGeometry3D();
            var dir = p2 - p1;
            var len = dir.Length;
            dir.Normalize();
            Vector3D up = new Vector3D(0, 1, 0);
            Vector3D n = Vector3D.CrossProduct(dir, up);
            if (n.Length < 1e-6) { up = new Vector3D(1, 0, 0); n = Vector3D.CrossProduct(dir, up); }
            n.Normalize();
            Vector3D b = Vector3D.CrossProduct(dir, n);

            for (int i = 0; i <= seg; i++)
            {
                double ang = i * 2 * Math.PI / seg;
                var offset = (n * Math.Cos(ang) + b * Math.Sin(ang)) * r;
                m.Positions.Add(p1 + offset);
                m.Positions.Add(p2 + offset);
            }
            for (int i = 0; i < seg; i++)
            {
                int i0 = i * 2; int i1 = i * 2 + 1; int i2 = (i + 1) * 2; int i3 = (i + 1) * 2 + 1;
                m.TriangleIndices.Add(i0); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i1);
                m.TriangleIndices.Add(i1); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i3);
            }
            return m;
        }

        private MeshGeometry3D BuildSphere(Point3D center, double r, int segU = 24, int segV = 16)
        {
            var m = new MeshGeometry3D();
            for (int v = 0; v <= segV; v++)
            {
                double phi = v * Math.PI / segV;
                for (int u = 0; u <= segU; u++)
                {
                    double theta = u * 2 * Math.PI / segU;
                    double x = r * Math.Sin(phi) * Math.Cos(theta);
                    double y = r * Math.Cos(phi);
                    double z = r * Math.Sin(phi) * Math.Sin(theta);
                    m.Positions.Add(new Point3D(center.X + x, center.Y + y, center.Z + z));
                }
            }
            int cols = segU + 1;
            for (int v = 0; v < segV; v++)
            {
                for (int u = 0; u < segU; u++)
                {
                    int i0 = v * cols + u;
                    int i1 = i0 + 1;
                    int i2 = i0 + cols;
                    int i3 = i2 + 1;
                    m.TriangleIndices.Add(i0); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i1);
                    m.TriangleIndices.Add(i1); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i3);
                }
            }
            return m;
        }

        private MeshGeometry3D BuildFrustum(double rBottomX, double rBottomZ, double rTopX, double rTopZ, double y0, double y1, int seg = 48)
        {
            var m = new MeshGeometry3D();
            for (int i = 0; i <= seg; i++)
            {
                double ang = i * 2 * Math.PI / seg;
                double cos = Math.Cos(ang), sin = Math.Sin(ang);
                var pB = new Point3D(rBottomX * cos, y0, rBottomZ * sin);
                var pT = new Point3D(rTopX * cos, y1, rTopZ * sin);
                m.Positions.Add(pB);
                m.Positions.Add(pT);
            }
            for (int i = 0; i < seg; i++)
            {
                int i0 = i * 2; int i1 = i * 2 + 1; int i2 = (i + 1) * 2; int i3 = (i + 1) * 2 + 1;
                m.TriangleIndices.Add(i0); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i1);
                m.TriangleIndices.Add(i1); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i3);
            }
            return m;
        }

        private MeshGeometry3D BuildTorus(double y, double radius, double tube, int seg = 96, int tubeSeg = 12)
        {
            var m = new MeshGeometry3D();
            for (int i = 0; i <= seg; i++)
            {
                double a = i * 2 * Math.PI / seg;
                double cx = Math.Cos(a), cz = Math.Sin(a);
                for (int j = 0; j <= tubeSeg; j++)
                {
                    double b = j * 2 * Math.PI / tubeSeg;
                    double tx = Math.Cos(b), tz = Math.Sin(b);
                    double x = (radius + tube * tx) * cx;
                    double z = (radius + tube * tx) * cz;
                    double yy = y + tube * tz;
                    m.Positions.Add(new Point3D(x, yy, z));
                }
            }
            int cols = tubeSeg + 1;
            for (int i = 0; i < seg; i++)
            {
                for (int j = 0; j < tubeSeg; j++)
                {
                    int i0 = i * cols + j;
                    int i1 = i0 + 1;
                    int i2 = (i + 1) * cols + j;
                    int i3 = i2 + 1;
                    m.TriangleIndices.Add(i0); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i1);
                    m.TriangleIndices.Add(i1); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i3);
                }
            }
            return m;
        }

        private void BtnRecenter_Click(object sender, RoutedEventArgs e)
        {
            _yaw = 0; _pitch = -15 * Math.PI / 180.0; _distance = 300;
            UpdateCamera();
        }

        private void BtnExportPng_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "PNG Image|*.png", FileName = "preview.png" };
            if (dlg.ShowDialog() == true)
            {
                var rtb = new RenderTargetBitmap((int)View3D.ActualWidth, (int)View3D.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(View3D);
                using var fs = new FileStream(dlg.FileName, FileMode.Create);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                encoder.Save(fs);
            }
        }

        // Simple camera controls
        
private void View3D_MouseDown(object sender, MouseButtonEventArgs e)
{
    _lastPos = e.GetPosition(this);
    _isDragging = true;
    Mouse.Capture(View3D);
}

        
private void View3D_MouseMove(object sender, MouseEventArgs e)
{
    if (_isDragging && Mouse.Captured == View3D && e.LeftButton == MouseButtonState.Pressed)
    {
        var p = e.GetPosition(this);
        var dx = (p.X - _lastPos.X) * 0.01;
        var dy = (p.Y - _lastPos.Y) * 0.01;
        _yaw += dx;
        _pitch = Math.Clamp(_pitch + dy, -Math.PI / 2 + 0.1, Math.PI / 2 - 0.1);
        _lastPos = p;
        UpdateCamera();
    }
}

        private void View3D_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            _distance = Math.Clamp(_distance * (e.Delta > 0 ? 0.9 : 1.1), 80, 800);
            UpdateCamera();
        }

        private void BtnGo3D_Click(object sender, RoutedEventArgs e)
        {
            Tabs.SelectedIndex = 1;
        }

private void View3D_MouseUp(object sender, MouseButtonEventArgs e)
{
    _isDragging = false;
    Mouse.Capture(null);
}

private void BtnBackToPhotos_Click(object sender, RoutedEventArgs e)
{
    Tabs.SelectedIndex = 0;
}

private GeometryModel3D _floorDisc;

private void EnsureFloor()
{
    if (_floorDisc != null) return;
    var mesh = BuildDisc(0, 60, 96);
    var mat = new DiffuseMaterial(new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)));
    _floorDisc = new GeometryModel3D(mesh, mat);
    _floorDisc.Transform = new TranslateTransform3D(0, 0.1, 0);
    _sceneRoot.Children.Add(_floorDisc);
}

private MeshGeometry3D BuildDisc(double y, double radius, int seg = 96)
{
    var m = new MeshGeometry3D();
    m.Positions.Add(new Point3D(0, y, 0));
    for (int i = 0; i <= seg; i++)
    {
        double a = i * 2 * Math.PI / seg;
        m.Positions.Add(new Point3D(radius * Math.Cos(a), y, radius * Math.Sin(a)));
    }
    for (int i = 1; i <= seg - 1; i++)
    {
        m.TriangleIndices.Add(0);
        m.TriangleIndices.Add(i);
        m.TriangleIndices.Add(i + 1);
    }
    return m;
}

private GeometryModel3D _heightArrow;

private MeshGeometry3D BuildArrow(double yBottom, double yTop)
{
    // shaft
    var m = new MeshGeometry3D();
    double r = 0.6;
    var shaft = BuildCylinder(new Point3D(0, yBottom, -35), new Point3D(0, yTop, -35), r, 32);
    // arrowheads
    var head1 = BuildCone(new Point3D(0, yTop, -35), new Vector3D(0,1,0), 3.0, 6.0, 32);
    var head2 = BuildCone(new Point3D(0, yBottom, -35), new Vector3D(0,-1,0), 3.0, 6.0, 32);
    // merge
    m.Positions = shaft.Positions;
    m.TriangleIndices = shaft.TriangleIndices;
    int off = m.Positions.Count;
    foreach (var p in head1.Positions) m.Positions.Add(p);
    foreach (var t in head1.TriangleIndices) m.TriangleIndices.Add(off + t);
    off = m.Positions.Count;
    foreach (var p in head2.Positions) m.Positions.Add(p);
    foreach (var t in head2.TriangleIndices) m.TriangleIndices.Add(off + t);
    return m;
}

private MeshGeometry3D BuildCone(Point3D tip, Vector3D dir, double radius, double height, int seg=32)
{
    dir.Normalize();
    var baseCenter = tip - dir * height;
    // pick orthonormal basis
    Vector3D up = new Vector3D(0,1,0);
    Vector3D n = Vector3D.CrossProduct(dir, up);
    if (n.Length < 1e-6) { up = new Vector3D(1,0,0); n = Vector3D.CrossProduct(dir, up); }
    n.Normalize();
    Vector3D b = Vector3D.CrossProduct(dir, n);

    var m = new MeshGeometry3D();
    // base ring
    for (int i=0;i<=seg;i++)
    {
        double a = i * 2 * Math.PI / seg;
        var off = (n * Math.Cos(a) + b * Math.Sin(a)) * radius;
        m.Positions.Add(baseCenter + off);
        m.Positions.Add(tip);
    }
    for (int i=0;i<seg;i++)
    {
        int i0 = i*2, i1 = i*2+1, i2 = (i+1)*2, i3 = (i+1)*2+1;
        m.TriangleIndices.Add(i0); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i1);
        m.TriangleIndices.Add(i1); m.TriangleIndices.Add(i2); m.TriangleIndices.Add(i3);
    }
    return m;
}

    }
}
