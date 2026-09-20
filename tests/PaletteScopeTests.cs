using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureGrade.Bridge;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;
using TextureGrade.Models;
using TextureGrade.TextureIO;
using TextureGrade.WpfUI;

internal static partial class Program
{
    private static void PaletteScopeIntegration()
    {
        L.Set(Lang.ZhCn);
        var original = Pixels(Enumerable.Range(0, 96 * 64).Select(i => i % 96 < 48 ? 0x884422 : 0x228866).ToArray());
        string texture = Path.Combine(output, "shared-scope.png"); TextureLoader.SavePng(texture, original, 96, 64);
        var bridge = new ScopeBridge(texture); var panel = new MainPanel(bridge);
        var settings = (GradeSettings)Get(panel, "Settings");
        Call(panel, "ApplyParams", new Dictionary<string, double> { ["Exposure"] = 1 });
        var selection = (HashSet<int>)Get(panel, "_selectedTris"); selection.Add(0);
        Call(panel, "AfterSelectionChanged", "Selection fixture");
        PumpUntil(() => Get(panel, "_gradeCancel") == null, "Scope selected render");
        var local = (byte[])Call(panel, "ComputeGraded");
        Check(Rgb(local, 8 * 96 + 16) != Rgb(original, 8 * 96 + 16) && Rgb(local, 8 * 96 + 80) == Rgb(original, 8 * 96 + 80), "UV scope preserves pixels in another material region");
        ((RadioButton)Get(panel, "ScopeWhole")).IsChecked = true;
        PumpUntil(() => Get(panel, "_gradeCancel") == null && Get(panel, "_sharedUvCancel") == null, "Whole texture and shared UV ready");
        var whole = (byte[])Call(panel, "ComputeGraded");
        Check(Rgb(whole, 8 * 96 + 80) != Rgb(original, 8 * 96 + 80) && selection.Count == 1, "Whole scope includes other regions and retains selected faces");
        var otherUv = ((System.Windows.Shapes.Path)Get(panel, "SharedTexturePath")).Data.Bounds;
        Check(Math.Abs(otherUv.Left - 48) < 1e-6 && Math.Abs(otherUv.Width - 48) < 1e-6, "Whole texture displays other shared material UVs");
        Check(((TextBlock)Get(panel, "TxtScopeNotice")).Text.Contains("2") && ((Border)Get(panel, "ScopeNotice")).Visibility == Visibility.Visible, "Whole texture warning names shared usage");
        panel.Undo(); Check(settings["Scope.WholeTexture"] == 0, "Scope undo");
        panel.Redo(); Check(settings["Scope.WholeTexture"] == 1, "Scope redo");
        var saved = settings.Capture();
        Call(panel, "ApplyParams", new Dictionary<string, double> { ["Exposure"] = 1 });
        Check(settings["Scope.WholeTexture"] == 0, "Legacy preset uses selection-first scope");
        Call(panel, "ApplyParams", saved); Check(settings["Scope.WholeTexture"] == 1, "Scope preset restore");
        var materials = (ListBox)Get(panel, "_materialList"); materials.SelectedIndex = 1;
        Check(settings["Scope.WholeTexture"] == 0, "Scope separate per material");
        materials.SelectedIndex = 0; Check(settings["Scope.WholeTexture"] == 1, "Scope material memory");
        panel.SaveNew();
        Check(bridge.SavedMaterial == 0 && TextureLoader.Load(bridge.SavedPath).rgba.SequenceEqual((byte[])Call(panel, "ComputeGraded")), "Whole texture saves exact result and updates current material only");

        selection.Add(0); Call(panel, "AfterSelectionChanged", "Selection restored");
        for (int i = 0; i < 6; i++) Call(panel, "SetWholeTextureScope", i % 2 == 0);
        PumpUntil(() => Get(panel, "_gradeCancel") == null, "Final scope wins asynchronous race");
        Check(settings["Scope.WholeTexture"] == 0 && ((bool[])Get(panel, "_maskCache"))[8 * 96 + 80] == false, "Outdated whole-texture mask discarded");

        // Simulate an imported image through the same session data used by the file decoder.
        var sessionType = typeof(MainPanel).GetNestedType("ReferenceSession", BindingFlags.NonPublic);
        var session = Activator.CreateInstance(sessionType, true);
        var reference = Pixels(0x3555dd, 0x3555dd);
        sessionType.GetField("Rgba").SetValue(session, reference);
        sessionType.GetField("Width").SetValue(session, 2); sessionType.GetField("Height").SetValue(session, 1);
        sessionType.GetField("Name").SetValue(session, "参考风格.png");
        var thumb = BitmapSource.Create(2, 1, 96, 96, PixelFormats.Rgb24, null, new byte[] { 0x35, 0x55, 0xdd, 0x35, 0x55, 0xdd }, 6); thumb.Freeze();
        sessionType.GetField("Thumbnail").SetValue(session, thumb);
        var references = (IDictionary)Get(panel, "_references"); references[0] = session;
        Call(panel, "SetWholeTextureScope", true);
        var state = new RecolorSettings { DetectCount = 2, ReferenceCount = 1, ReferenceLock = true };
        state.Entries.Add(PaletteEntry.Identity(0, true)); state.Write(settings);
        Call(panel, "AnalyzeReference", true);
        PumpUntil(() => Get(panel, "_analysisCancel") == null, "Generate target-style palette");
        Check(!(bool)Get(panel, "_referenceExpanded"), "Generating a reference palette reveals the mapping editor");
        state = RecolorSettings.Read(settings);
        Check(state.Mode == RecolorMode.Palette && state.Entries.Count(e => !e.Manual) == 2 && state.Entries.Any(e => e.Manual), "Reference creates editable palette and keeps manual entries");
        Check(state.Entries.Where(e => !e.Manual).All(e => e.TransferChroma), "UI generation enables reference chroma interpolation");
        Check(state.Entries.Where(e => !e.Manual).All(e => e.LockLightness && Math.Abs(e.EffectiveTarget.L - e.Source.L) < 1e-6), "Reference palette generation locks source lightness");
        state.ReferenceLock = false; state.Write(settings);
        Call(panel, "AnalyzeReference", true);
        PumpUntil(() => Get(panel, "_analysisCancel") == null, "Generate unlocked target-style palette");
        state = RecolorSettings.Read(settings);
        Check(state.Entries.Where(e => !e.Manual).All(e => !e.LockLightness && OklabColor.ToRgb(e.EffectiveTarget) == 0x3555dd), "Unlocked palette generation retains reference lightness");
        var generated = settings.Capture(); var expected = (byte[])Call(panel, "ComputeGraded");
        references.Clear(); Call(panel, "ApplyParams", generated);
        Check(((byte[])Call(panel, "ComputeGraded")).SequenceEqual(expected), "Generated palette preset works without reference image");
        references[0] = session; Call(panel, "RefreshRecolorUi");
        var root = (StackPanel)Get(panel, "_recolorRoot"); ((Expander)root.Parent).IsExpanded = true;
        var transfer = root.Children.OfType<Expander>().Single(e => (string)e.Header == L.T("Rc.Reference")); transfer.IsExpanded = true;
        Check(root.Children.OfType<WrapPanel>().Single().Children.Count == 2, "Palette only exposes off/on at top level");
        panel.Width = 1400; panel.Height = 1150; Capture(panel, "ui-palette-scope.png", 1400, 1150);
        var editor = root.Children.OfType<StackPanel>().Single(p => (string)p.Tag == "PaletteEditor");
        var actions = editor.Children.OfType<WrapPanel>().Single();
        var mappings = editor.Children.OfType<StackPanel>().Single(p => (string)p.Tag == "PaletteMappings");
        Check(editor.Children.IndexOf(mappings) == editor.Children.IndexOf(actions) + 1, "Palette actions stay immediately beside mappings even with reference expanded");
        var countInput = ((DockPanel)editor.Children[0]).Children.OfType<TextBox>().Single();
        var detectButton = actions.Children.OfType<Button>().First();
        countInput.Text = "4";
        countInput.RaiseEvent(new System.Windows.Input.KeyboardFocusChangedEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, 0, countInput, detectButton)
            { RoutedEvent = System.Windows.Input.Keyboard.LostKeyboardFocusEvent });
        Check(RecolorSettings.Read(settings).DetectCount == 4 && ReferenceEquals(editor.Parent, root) && ReferenceEquals(detectButton.Parent, actions),
            "Committing a count keeps action buttons alive for the pending click");
        transfer.IsExpanded = false;
        Call(panel, "RunGradeAsync"); PumpUntil(() => Get(panel, "_gradeCancel") == null, "Palette layout screenshot uses the current graded image");
        Capture(panel, "ui-palette-controls.png", 1400, 1150);
        byte[] priorPixels = (byte[])Call(panel, "ComputeGraded"); int priorSelection = panel.SelectedTriangleCount;
        var toggleUv = (Button)Get(panel, "BtnToggleUV"); int changes = 0; panel.ShowUVChanged += () => changes++;
        toggleUv.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(!panel.ShowUV && ((Canvas)Get(panel, "UvCanvas")).Visibility == Visibility.Collapsed &&
            ((Canvas)Get(panel, "TextureContextCanvas")).Visibility == Visibility.Collapsed, "Left toolbar hides both material and shared-texture UV overlays");
        Check(panel.SelectedTriangleCount == priorSelection && ((byte[])Call(panel, "ComputeGraded")).SequenceEqual(priorPixels), "Hiding UV retains selection and graded pixels");
        Capture(panel, "ui-hidden-uv.png", 1400, 1150);
        panel.SetShowUV(false); Check(changes == 1 && (string)toggleUv.Content == L.T("Btn.ShowUV"), "UV menu synchronization does not recurse and offers Show UV");
        toggleUv.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(panel.ShowUV && changes == 2 && ((Canvas)Get(panel, "UvCanvas")).Visibility == Visibility.Visible &&
            (string)toggleUv.Content == L.T("Btn.HideUV"), "Left toolbar restores UV overlays and button action");
        editor = ((StackPanel)Get(panel, "_recolorRoot")).Children.OfType<StackPanel>().Single(p => (string)p.Tag == "PaletteEditor");
        editor.Children.OfType<CheckBox>().Single(c => (string)c.Tag == "AutoCount").IsChecked = true;
        root = (StackPanel)Get(panel, "_recolorRoot");
        transfer = root.Children.OfType<Expander>().Single();
        ((StackPanel)transfer.Content).Children.OfType<CheckBox>().Single(c => (string)c.Tag == "AutoCount").IsChecked = true;
        editor = ((StackPanel)Get(panel, "_recolorRoot")).Children.OfType<StackPanel>().Single(p => (string)p.Tag == "PaletteEditor");
        Check(!((DockPanel)editor.Children[0]).Children.OfType<TextBox>().Single().IsEnabled, "Automatic source count disables manual entry");
        Call(panel, "AnalyzeReference", true);
        PumpUntil(() => Get(panel, "_analysisCancel") == null && Get(panel, "_gradeCancel") == null, "Automatic reference generation");
        state = RecolorSettings.Read(settings);
        Check(state.AutoDetectCount && state.AutoReferenceCount && state.DetectCount == 2 && state.ReferenceCount == 1,
            "UI automatic generation records independently estimated counts");
        var automaticPixels = (byte[])Call(panel, "ComputeGraded"); var automaticPreset = settings.Capture();
        panel.Undo(); state = RecolorSettings.Read(settings);
        Check(state.AutoDetectCount && state.AutoReferenceCount && state.DetectCount == 4, "Automatic generation undoes result and computed count together");
        panel.Redo();
        Check(RecolorSettings.Read(settings).DetectCount == 2 && ((byte[])Call(panel, "ComputeGraded")).SequenceEqual(automaticPixels), "Automatic generation redo reproduces exact render");
        materials.SelectedIndex = 1; materials.SelectedIndex = 0;
        Check(RecolorSettings.Read(settings).AutoDetectCount && RecolorSettings.Read(settings).AutoReferenceCount, "Automatic choices persist per material");
        references.Clear(); Call(panel, "ApplyParams", automaticPreset);
        Check(((byte[])Call(panel, "ComputeGraded")).SequenceEqual(automaticPixels), "Automatic reference preset reproduces pixels after reference removal");
        references[0] = session; Call(panel, "RefreshRecolorUi");
        Call(panel, "RunGradeAsync"); PumpUntil(() => Get(panel, "_gradeCancel") == null, "Automatic palette screenshot preview");
        root = (StackPanel)Get(panel, "_recolorRoot"); root.Children.OfType<Expander>().Single().IsExpanded = true;
        Capture(panel, "ui-automatic-palette.png", 1400, 1150);
        foreach (Lang language in Enum.GetValues(typeof(Lang)))
        {
            L.Set(language); panel.ApplyLanguage();
            Check(!((TextBlock)Get(panel, "TxtScopeNotice")).Text.StartsWith("Scope."), "Localized scope warning " + language);
            Check((string)toggleUv.Content == L.T("Btn.HideUV") && !((string)toggleUv.Content).StartsWith("Btn."), "Localized UV visibility control " + language);
            Check(!L.T("Rc.AutoCount").StartsWith("Rc.") && !L.F("Rc.MatchedCount", 10, 5).StartsWith("Rc."), "Localized automatic count and result " + language);
        }
        L.Set(Lang.ZhCn); Call(panel, "CancelRecolorWork");
    }

    private sealed class ScopeBridge : IPMDBridge
    {
        private readonly string texture;
        public int SavedMaterial = -1; public string SavedPath;
        public ScopeBridge(string path) { texture = path; }
        public string PmxDirectory => Path.GetDirectoryName(texture);
        public ModelSnapshot LoadCurrent() => new ModelSnapshot("scope.pmx", PmxDirectory,
            new[] { new MaterialInfo(0, "左侧材质", texture, texture, true), new MaterialInfo(1, "右侧材质", texture, texture, true) });
        public void ApplyPreview(int i, string path) { }
        public void ApplySaved(int i, string path) { SavedMaterial = i; SavedPath = path; }
        public void Revert(int i) { }
        public void Cleanup() { }
        public int[] GetPmxSelectedVertices() => new int[0];
        public void SetPmxSelectedVertices(int[] vertices) { }
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3)> GetUVTriangles(int i)
            => GetUVTrianglesWithIndices(i).Select(t => (t.u1, t.v1, t.u2, t.v2, t.u3, t.v3)).ToArray();
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3, int i1, int i2, int i3)> GetUVTrianglesWithIndices(int i)
        {
            float left = i * .5f, right = left + .5f;
            return new[] { (left, 0f, right, 0f, right, 1f, i * 4, i * 4 + 1, i * 4 + 2), (left, 0f, right, 1f, left, 1f, i * 4, i * 4 + 2, i * 4 + 3) };
        }
    }
}
