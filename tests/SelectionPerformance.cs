using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TextureGrade.Bridge;
using TextureGrade.Models;
using TextureGrade.TextureIO;
using TextureGrade.WpfUI;

internal static partial class Program
{
    private static void SelectionPerformance()
    {
        if (Application.Current == null) new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
        string texture = Path.Combine(output, "selection-source.png"); TextureLoader.SavePng(texture, Gradient(256, 256), 256, 256);
        var bridge = new DenseBridge(texture, 128);
        var panel = new MainPanel(bridge);
        panel.Measure(new Size(1200, 950)); panel.Arrange(new Rect(0, 0, 1200, 950)); panel.UpdateLayout(); panel.FitView();
        var watch = Stopwatch.StartNew();
        panel.SelectAllUv(); panel.UpdateLayout();
        long selectMs = watch.ElapsedMilliseconds;
        var canvas = (Canvas)Get(panel, "UvCanvas");
        var selectedGeometry = ((System.Windows.Shapes.Path)canvas.Children[1]).Data;
        watch.Restart();
        for (int i = 0; i < 8; i++) { panel.ZoomIn(); panel.ZoomOut(); panel.UpdateLayout(); }
        long zoomMs = watch.ElapsedMilliseconds;
        Check(canvas.Children.Count == 4, "Dense UV keeps constant drawing layer count");
        Check(ReferenceEquals(selectedGeometry, ((System.Windows.Shapes.Path)canvas.Children[1]).Data), "Zoom reuses selected face geometry");
        Check(((Point[])Get(panel, "_markedDotPositions")).Length == bridge.VertexCount, "Shared corner display coordinates deduplicated exactly");
        Console.WriteLine($"Selection benchmark: {panel.SelectedTriangleCount} faces / {bridge.VertexCount} vertices; select+layout {selectMs} ms; 16 zooms+layout {zoomMs} ms; UV elements {canvas.Children.Count}");
        panel.SendSelectedVertices(); Check(bridge.Sent.Length == bridge.VertexCount, "Dense selection keeps every vertex");
        watch.Restart(); Capture(panel, "ui-dense-selection.png", 1200, 950);
        Console.WriteLine("Dense selection software screenshot: " + watch.ElapsedMilliseconds + " ms");

        var settings = (GradeSettings)Get(panel, "Settings"); settings["Exposure"] = 1;
        panel.SelectAllUv(); panel.ClearUVSelection();
        var selected = (HashSet<int>)Get(panel, "_selectedTris");
        for (int y = 0; y < 128; y++) for (int x = 0; x < 64; x++) { int i = 2 * (y * 128 + x); selected.Add(i); selected.Add(i + 1); }
        Call(panel, "AfterSelectionChanged", "Partial selection fixture");
        PumpUntil(() => Get(panel, "_gradeCancel") == null, "Dense async selection completes");
        var mask = (bool[])Get(panel, "_maskCache");
        Check(mask.Length == 256 * 256 && mask.Where((value, i) => value != (i % 256 < 128)).Count() == 0, "Stale masks discarded; current selection exact");
        var preview = (System.Windows.Media.Imaging.WriteableBitmap)Get(panel, "_wb"); var actual = new byte[256 * 256 * 4]; preview.CopyPixels(actual, 256 * 4, 0);
        var expected = (byte[])Call(panel, "ComputeGraded");
        for (int i = 0; i < expected.Length; i += 4) { byte t = expected[i]; expected[i] = expected[i + 2]; expected[i + 2] = t; }
        Check(actual.SequenceEqual(expected), "Async selection preview matches final renderer");
        panel.ClearUVSelection(); panel.ReceiveSelectedVertices(); panel.SendSelectedVertices();
        Check(bridge.Sent.Length == bridge.VertexCount && ((Point[])Get(panel, "_markedDotPositions")).Length == bridge.VertexCount, "Received vertices invalidate only marker display");
        panel.ClearUVSelection(); Check(((Point[])Get(panel, "_markedDotPositions")).Length == 0, "Clear selection removes cached markers");

        ((Button)Get(panel, "BtnMode")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(panel.PanMode && (string)((Button)Get(panel, "BtnMode")).Tag == "on", "Pan button shows active mode");
        ((Button)Get(panel, "BtnSelectMode")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!panel.PanMode && (string)((Button)Get(panel, "BtnSelectMode")).Tag == "on", "Select button shows active mode");
        panel.Width = 850; panel.Height = 1100;
        foreach (TextureGrade.Localization.Lang lang in Enum.GetValues(typeof(TextureGrade.Localization.Lang)))
        {
            TextureGrade.Localization.L.Set(lang); panel.ApplyLanguage();
            Capture(panel, "ui-tools-" + lang + ".png", 850, 1100);
        }
        TextureGrade.Localization.L.Set(TextureGrade.Localization.Lang.ZhCn); panel.ApplyLanguage();
        Call(panel, "CancelRecolorWork");
        Console.WriteLine("PASS selection, cache, background mask and toolbar checks: " + assertions);
    }
    private sealed class DenseBridge : IPMDBridge
    {
        private readonly string texture;
        private readonly List<(float u1, float v1, float u2, float v2, float u3, float v3, int i1, int i2, int i3)> triangles = new List<(float, float, float, float, float, float, int, int, int)>();
        public int VertexCount { get; }
        public int[] Sent = new int[0];
        public DenseBridge(string path, int side)
        {
            texture = path; VertexCount = (side + 1) * (side + 1);
            for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
            {
                float u = (float)x / side, v = (float)y / side, u2 = (float)(x + 1) / side, v2 = (float)(y + 1) / side;
                int a = y * (side + 1) + x, b = a + 1, c = a + side + 1, d = c + 1;
                triangles.Add((u, v, u2, v, u2, v2, a, b, d)); triangles.Add((u, v, u2, v2, u, v2, a, d, c));
            }
        }
        public string PmxDirectory => Path.GetDirectoryName(texture);
        public ModelSnapshot LoadCurrent() => new ModelSnapshot("dense.pmx", PmxDirectory, new[] { new MaterialInfo(0, "密集 UV 测试", texture, texture, true) });
        public void ApplyPreview(int i, string path) { }
        public void ApplySaved(int i, string path) { }
        public void Revert(int i) { }
        public void Cleanup() { }
        public int[] GetPmxSelectedVertices() => Enumerable.Range(0, VertexCount).ToArray();
        public void SetPmxSelectedVertices(int[] vertices) { Sent = vertices; }
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3)> GetUVTriangles(int i)
            => triangles.Select(t => (t.u1, t.v1, t.u2, t.v2, t.u3, t.v3)).ToArray();
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3, int i1, int i2, int i3)> GetUVTrianglesWithIndices(int i) => triangles;
    }
}
