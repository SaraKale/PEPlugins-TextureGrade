using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using TextureGrade.Bridge;
using TextureGrade.Localization;
using TextureGrade.Models;
using TextureGrade.TextureIO;
using TextureGrade.WpfUI;

internal static partial class Program
{
    private static void UpstreamFeatures()
    {
        ExportFormats(); PresetManagement(); SelectionStateIntegration();
    }
    private static void ExportFormats()
    {
        var pixels = Gradient(7, 5);
        for (int p = 0; p < 35; p++) pixels[p * 4 + 3] = (byte)(p * 255 / 34);
        foreach (TextureFormat format in Enum.GetValues(typeof(TextureFormat)))
        {
            string path = Path.Combine(output, "format-" + format + TextureWriter.Extension(format));
            TextureWriter.Save(path, pixels, 7, 5, format);
            var decoded = TextureLoader.Load(path);
            Check(decoded.width == 7 && decoded.height == 5, "Format dimensions and decode " + format);
            if (format == TextureFormat.Png || format == TextureFormat.Tga || format == TextureFormat.DdsRaw)
                Check(decoded.rgba.SequenceEqual(pixels), "Lossless format preserves RGB and alpha " + format);
            for (int p = 0; p < 35; p++)
            {
                int alpha = decoded.rgba[p * 4 + 3], original = pixels[p * 4 + 3];
                if (format == TextureFormat.DdsDxt1) Check(alpha == (original < 128 ? 0 : 255), "DXT1 binary alpha");
                else if (format == TextureFormat.DdsDxt3) Check(Math.Abs(alpha - original) <= 8, "DXT3 rounded 4-bit alpha");
                else if (format == TextureFormat.DdsDxt5) Check(Math.Abs(alpha - original) <= 19, "DXT5 interpolated alpha");
                else if (!TextureWriter.KeepsAlpha(format)) Check(alpha == 255, "Flattened format opaque " + format);
            }
        }
        var opaque = Pixels(0x123456, 0xabcdef); opaque[3] = 0;
        var resized = TextureWriter.Resample(opaque, 2, 1, 1, 1);
        Check(resized[3] == 128 && Rgb(resized, 0) == 0xabcdef, "Resize does not bleed hidden transparent RGB");
        Check(ReferenceEquals(TextureWriter.Resample(pixels, 7, 5, 7, 5), pixels), "Original size is pixel-exact without resampling");
        var odd = Pixels(Enumerable.Range(0, 25).Select(i => i % 5 == 4 ? 0xffffff : 0).ToArray());
        Check(Rgb(TextureWriter.Resample(odd, 5, 5, 2, 2), 1) > 0, "Odd-size downsample includes last source column");
        string protectedFile = Path.Combine(output, "save-failure.png"); File.WriteAllText(protectedFile, "unchanged");
        bool rejected = false;
        try { TextureWriter.Save(protectedFile, new byte[4], int.MaxValue, 9, TextureFormat.Png); } catch (ArgumentException) { rejected = true; }
        Check(rejected && File.ReadAllText(protectedFile) == "unchanged", "Invalid export size does not destroy existing output");
        TextureWriter.Save(protectedFile, pixels, 7, 5, TextureFormat.Png);
        Check(TextureLoader.Load(protectedFile).rgba.SequenceEqual(pixels), "Atomic replacement writes complete texture");
        Check(!Directory.GetFiles(output, ".texturegrade-*.tmp").Any(), "No export scratch files remain");

        string alphaPath = Path.Combine(output, "selection-alpha.png");
        var mask = Enumerable.Range(0, 35).Select(i => i % 3 == 0).ToArray();
        var before = (byte[])pixels.Clone(); MaskWriter.SaveAlphaPng(alphaPath, mask, 7, 5, pixels);
        var alphaPixels = TextureLoader.Load(alphaPath).rgba;
        for (int i = 0; i < 35; i++)
            Check(Rgb(alphaPixels, i) == Rgb(pixels, i) && alphaPixels[4 * i + 3] == (mask[i] ? 255 : 0), "Selection replaces alpha and preserves hidden RGB");
        Check(pixels.SequenceEqual(before), "Alpha export never mutates source pixels");
        MaskWriter.SaveAlphaPng(alphaPath, mask, 7, 5);
        Check(TextureLoader.Load(alphaPath).rgba.Where((v, i) => i % 4 != 3).All(v => v == 255), "White RGB alpha mask option");

        foreach (Lang lang in Enum.GetValues(typeof(Lang)))
        {
            L.Set(lang);
            using (var dialog = new SaveTextureDialog(Path.Combine(output, "format-dialog.png"), 512, 256))
            {
                var formats = (System.Windows.Forms.ComboBox)Get(dialog, "_cboFormat");
                Check(formats.Items.Count == 10 && !((System.Windows.Forms.NumericUpDown)Get(dialog, "_numW")).Enabled, "Format dialog choices and original size " + lang);
                formats.SelectedIndex = (int)TextureFormat.DdsDxt5;
                Check(((System.Windows.Forms.TextBox)Get(dialog, "_txtPath")).Text.EndsWith(".dds"), "Format switch updates extension " + lang);
                ((System.Windows.Forms.ComboBox)Get(dialog, "_cboSize")).SelectedIndex = 1;
                Check(((System.Windows.Forms.NumericUpDown)Get(dialog, "_numW")).Value == 256 && ((System.Windows.Forms.NumericUpDown)Get(dialog, "_numH")).Value == 128, "Half-size export choice " + lang);
                Check(!L.T("SaveDlg.Title").StartsWith("SaveDlg.") && !L.T("Preset.Rename").StartsWith("Preset."), "Upstream feature localization " + lang);
                dialog.PerformLayout();
                var footer = dialog.Controls.OfType<System.Windows.Forms.Panel>().Single();
                Check(((System.Windows.Forms.Label)Get(dialog, "_lblInfo")).Bottom <= footer.Top, "Format dialog keeps details above footer " + lang);
            }
        }
        L.Set(Lang.ZhCn);
    }
    private static void PresetManagement()
    {
        var field = typeof(PresetStore).GetField("_folder", BindingFlags.NonPublic | BindingFlags.Static);
        var previous = field.GetValue(null);
        string folder = Path.Combine(output, "presets-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); field.SetValue(null, folder);
        try
        {
            var settings = Settings(Palette()); var json = settings.Capture();
            foreach (string name in new[] { "C", "A", "B" }) PresetStore.Save(name, json);
            Check(PresetStore.List().SequenceEqual(new[] { "A", "B", "C" }), "Presets sort by name");
            File.SetLastWriteTimeUtc(Path.Combine(folder, "A.json"), new DateTime(2020, 1, 1));
            File.SetLastWriteTimeUtc(Path.Combine(folder, "B.json"), new DateTime(2021, 1, 1));
            File.SetLastWriteTimeUtc(Path.Combine(folder, "C.json"), new DateTime(2022, 1, 1));
            Check(PresetStore.List(PresetStore.PresetSort.Time).SequenceEqual(new[] { "C", "B", "A" }), "Presets sort by modification time");
            PresetStore.SaveOrder(new[] { "C", "c", "missing", "A" });
            Check(PresetStore.List(PresetStore.PresetSort.Custom).SequenceEqual(new[] { "C", "A", "B" }), "Manual order deduplicates case and appends unlisted presets");
            byte[] content = File.ReadAllBytes(Path.Combine(folder, "A.json"));
            Check(PresetStore.Rename("A", "配色 #1") && File.ReadAllBytes(Path.Combine(folder, "配色 #1.json")).SequenceEqual(content), "Rename preserves preset bytes including palette data");
            Check(PresetStore.List(PresetStore.PresetSort.Custom).SequenceEqual(new[] { "C", "配色 #1", "B" }), "Rename preserves custom position");
            Check(!PresetStore.Rename("B", "C") && !PresetStore.Rename("B", "../outside") && !PresetStore.Rename("B", "NUL") && !PresetStore.Rename("B", ""), "Rename rejects collisions, paths, devices and empty names");
            PresetStore.SaveSort(PresetStore.PresetSort.Custom); Check(PresetStore.LoadSort() == PresetStore.PresetSort.Custom, "Sort preference survives reopening");
            var panel = new MainPanel(new FakeBridge(Path.Combine(output, "source.png")));
            var list = (ListBox)Get(panel, "_presetList"); list.SelectedItem = "B"; Call(panel, "MoveSelectedPreset", -1);
            Check(PresetStore.List(PresetStore.PresetSort.Custom).SequenceEqual(new[] { "C", "B", "配色 #1" }) && (string)list.SelectedItem == "B", "Preset move UI preserves selection and saves order");
            PresetStore.Delete("C"); Check(PresetStore.List(PresetStore.PresetSort.Custom).SequenceEqual(new[] { "B", "配色 #1" }), "Removed preset does not leave a visible ghost row");
            Call(panel, "CancelRecolorWork");
        }
        finally { field.SetValue(null, previous); }
    }
    private static void SelectionStateIntegration()
    {
        string path = Path.Combine(output, "selection-state-source.png"); var bytes = Pixels(Enumerable.Repeat(0x804020, 64 * 64).ToArray());
        TextureLoader.SavePng(path, bytes, 64, 64);
        var originalFile = File.ReadAllBytes(path);
        var bridge = new MutableSelectionBridge(path); var panel = new MainPanel(bridge);
        var list = (ListBox)Get(panel, "_materialList"); var settings = (GradeSettings)Get(panel, "Settings");
        var selected = (HashSet<int>)Get(panel, "_selectedTris"); var vertices = (HashSet<int>)Get(panel, "_recvVerts");
        selected.Add(0); vertices.Add(1); Call(panel, "AfterSelectionChanged", "test selection");
        Call(panel, "ApplyParams", new Dictionary<string, double> { ["Exposure"] = 1 });
        var before = (byte[])Call(panel, "ComputeGraded");
        Check(Rgb(before, 8 * 64 + 48) != Rgb(bytes, 8 * 64 + 48) && Rgb(before, 48 * 64 + 8) == Rgb(bytes, 48 * 64 + 8), "Initial grading affects selected triangle only");
        list.SelectedIndex = 1; Check(selected.Count == 0 && settings["Exposure"] == 0, "Material switch isolates selection and parameters");
        list.SelectedIndex = 0;
        Check(selected.SetEquals(new[] { 0 }) && vertices.Contains(1) && ((byte[])Call(panel, "ComputeGraded")).SequenceEqual(before), "Returning to material restores exact partial grade");
        panel.ReRead(); Check(selected.SetEquals(new[] { 0 }) && ((byte[])Call(panel, "ComputeGraded")).SequenceEqual(before), "Re-read retains selection and graded scope");
        panel.InvertUVSelection(); Check(selected.SetEquals(new[] { 1 }), "Invert selects complement");
        var inverted = (byte[])Call(panel, "ComputeGraded");
        Check(Rgb(inverted, 8 * 64 + 48) == Rgb(bytes, 8 * 64 + 48) && Rgb(inverted, 48 * 64 + 8) != Rgb(bytes, 48 * 64 + 8), "Invert invalidates actual grading mask");
        panel.InvertUVSelection(); Check(((byte[])Call(panel, "ComputeGraded")).SequenceEqual(before), "Double inversion restores pixels");
        panel.SelectAllUv(); panel.InvertUVSelection(); Check(selected.Count == 0 && vertices.Contains(1), "Inverting all retains received vertex markers");
        list.SelectedIndex = 1; list.SelectedIndex = 0; Check(selected.Count == 0 && vertices.Contains(1), "Vertex-only selection state is retained");
        selected.Add(0); Call(panel, "AfterSelectionChanged", "selection restored");
        list.SelectedIndex = 1; bridge.ChangedUv = true; list.SelectedIndex = 0;
        Check(selected.Count == 0 && vertices.Count == 0, "Equal face counts with changed UVs reject stale selection");
        bridge.ChangedUv = false; panel.ReRead(); selected.Add(0); Call(panel, "AfterSelectionChanged", "selection restored");
        Call(panel, "SetWholeTextureScope", true);
        var allGraded = (byte[])Call(panel, "ComputeGraded"); string alpha = Path.Combine(output, "whole-scope-selection-alpha.png");
        Call(panel, "SaveSelectionAlpha", alpha, 64, 64, 2);
        var exported = TextureLoader.Load(alpha).rgba;
        Check(exported[(8 * 64 + 48) * 4 + 3] == 255 && exported[(48 * 64 + 8) * 4 + 3] == 0, "Alpha export uses explicit selection even in whole-texture grading mode");
        Check(Rgb(exported, 48 * 64 + 8) == Rgb(allGraded, 48 * 64 + 8), "Alpha export retains RGB under transparent pixels");
        Check(File.ReadAllBytes(path).SequenceEqual(originalFile), "Selection export preserves original file");
        string saved = Path.Combine(output, "selection-state-save.tga");
        Call(panel, "SaveGradedTexture", saved, TextureFormat.Tga, 64, 64, 92);
        Check(bridge.Saved == saved && TextureLoader.Load(saved).rgba.SequenceEqual(allGraded), "Format save uses final renderer and updates material only after writing");
        bool protectedSource = false;
        try { Call(panel, "SaveGradedTexture", path, TextureFormat.Png, 64, 64, 92); }
        catch (TargetInvocationException ex) { protectedSource = ex.InnerException is InvalidOperationException; }
        Check(protectedSource && File.ReadAllBytes(path).SequenceEqual(originalFile) && bridge.Saved == saved, "Saving over original is rejected before writing or updating material");
        protectedSource = false;
        try { Call(panel, "SaveSelectionAlpha", path, 64, 64, 0); }
        catch (TargetInvocationException ex) { protectedSource = ex.InnerException is InvalidOperationException; }
        Check(protectedSource && File.ReadAllBytes(path).SequenceEqual(originalFile), "Alpha export also protects the original texture");
        list.SelectedIndex = 1; list.SelectedIndex = 0;
        Call(panel, "ApplyParams", new Dictionary<string, double>());
        ((HashSet<int>)Get(panel, "_pushedMats")).Clear(); Call(panel, "MarkCurrentModified");
        var row = ((IEnumerable)Get(panel, "_rows")).Cast<object>().First();
        Check(!(bool)row.GetType().GetProperty("Modified").GetValue(row), "Stale saved parameters cannot keep a reset material modified");
        Call(panel, "CancelRecolorWork");
    }
    private sealed class MutableSelectionBridge : IPMDBridge
    {
        private readonly string texture;
        public bool ChangedUv; public string Saved;
        public MutableSelectionBridge(string path) { texture = path; }
        public string PmxDirectory => Path.GetDirectoryName(texture);
        public ModelSnapshot LoadCurrent() => new ModelSnapshot("state.pmx", PmxDirectory, new[] { new MaterialInfo(0, "A", texture, texture, true), new MaterialInfo(1, "B", texture, texture, true) });
        public void ApplyPreview(int i, string path) { }
        public void ApplySaved(int i, string path) { Saved = path; }
        public void Revert(int i) { }
        public void Cleanup() { }
        public int[] GetPmxSelectedVertices() => new int[0];
        public void SetPmxSelectedVertices(int[] indices) { }
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3)> GetUVTriangles(int i)
            => GetUVTrianglesWithIndices(i).Select(t => (t.u1, t.v1, t.u2, t.v2, t.u3, t.v3)).ToArray();
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3, int i1, int i2, int i3)> GetUVTrianglesWithIndices(int i)
            => new[] { (ChangedUv ? .1f : 0f, 0f, 1f, 0f, 1f, 1f, 0, 1, 2), (0f, 0f, 1f, 1f, 0f, 1f, 0, 2, 3) };
    }
}
