using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TextureGrade.Bridge;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;
using TextureGrade.Models;
using TextureGrade.TextureIO;
using TextureGrade.WpfUI;

internal static partial class Program
{
    private static int assertions;
    private static string output;
    private static void Check(bool valid, string name) { assertions++; if (!valid) throw new Exception(name); }
    private static bool Near(Oklab a, Oklab b, double tolerance = 1e-6)
        => Math.Abs(a.L - b.L) < tolerance && Math.Abs(a.A - b.A) < tolerance && Math.Abs(a.B - b.B) < tolerance;
    private static GradeSettings Settings(RecolorSettings r) { var s = new GradeSettings(); r.Write(s); return s; }
    private static byte[] Pixels(params int[] colors)
    {
        var bytes = new byte[colors.Length * 4];
        for (int i = 0; i < colors.Length; i++) { bytes[4 * i] = (byte)(colors[i] >> 16); bytes[4 * i + 1] = (byte)(colors[i] >> 8); bytes[4 * i + 2] = (byte)colors[i]; bytes[4 * i + 3] = 255; }
        return bytes;
    }
    private static int Rgb(byte[] p, int index) => (p[index * 4] << 16) | (p[index * 4 + 1] << 8) | p[index * 4 + 2];
    [STAThread]
    private static int Main(string[] args)
    {
        output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts"); Directory.CreateDirectory(output);
        try
        {
            if (args.Length > 1 && args[1] == "--selection-bench") { SelectionPerformance(); return 0; }
            if (args.Length > 1 && args[1] == "--reference-diagnostic") { ReferenceDiagnostic(args[2], args[3], args[4]); return 0; }
            if (args.Length > 1 && args[1] == "--palette-exchange")
            {
                if (Application.Current == null) new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                PaletteExchangeIntegration(); Console.WriteLine("PASS: " + assertions + " palette exchange assertions"); return 0;
            }
            Colors(); Extraction(); AutomaticPalette(); LargeReferencePalette(); Mapping(); GlobalPaletteCoverage(); ReferenceChromaTransfer(); RemovedTransferCompatibility(); PersistenceAndRendering(); TexturePathRoundTrips(); ThresholdBehavior(); Performance(); UiIntegration(); PaletteScopeIntegration(); UpstreamFeatures(); PaletteExchangeIntegration();
            Console.WriteLine("PASS: " + assertions + " assertions; artifacts: " + output); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Colors()
    {
        var random = new Random(4);
        foreach (int rgb in new[] { 0, 0xffffff, 0xff0000, 0x00ff00, 0x0000ff, 0x010101, 0x010000 })
            Check(OklabColor.ToRgb(OklabColor.FromRgb(rgb)) == rgb, "RGB round trip " + rgb);
        Check(Near(OklabColor.FromRgb(0xff0000), new Oklab(.62795536, .22486306, .12584630)), "Ottosson red reference");
        double worst = 0;
        for (int i = 0; i < 3000; i++)
        {
            var source = OklabColor.FromRgb(random.Next(0x1000000));
            var fit = OklabColor.FitGamut(OklabColor.FromLch(source.L, random.NextDouble() * .5, random.NextDouble() * 360));
            Check(Math.Abs(source.L - fit.L) < 1e-6, "Gamut keeps L");
            Check(OklabColor.InGamut(fit), "Gamut bounds");
            worst = Math.Max(worst, Math.Abs(source.L - OklabColor.FromRgb(OklabColor.ToRgb(fit)).L));
        }
        Check(worst < .02, "Quantization L error"); Console.WriteLine("Max measured 8-bit L error: " + worst.ToString("F6"));
    }
    private static void Extraction()
    {
        var pixels = Pixels(0xff0000, 0xff0000, 0x00ff00, 0x0000ff, 0xffffff); pixels[19] = 0; pixels[11] = 128;
        var samples = PaletteAnalysis.Sample(pixels); Check(samples.Count == 3, "Transparent excluded");
        var palette = PaletteAnalysis.Extract(samples, 32); Check(palette.Count == 3, "Fewer available colors");
        Check(palette[0].Rgb == 0xff0000, "Weighted occupancy sort");
        var mask = new[] { false, false, true, false, false };
        Check(PaletteAnalysis.Extract(PaletteAnalysis.Sample(pixels, mask), 8).Single().Rgb == 0x00ff00, "One pixel selection");
        Check(PaletteAnalysis.Sample(pixels, new bool[5]).Count == 0, "Empty mask");
        var gradient = Gradient(256, 256);
        var a = PaletteAnalysis.Extract(PaletteAnalysis.Sample(gradient), 8);
        var b = PaletteAnalysis.Extract(PaletteAnalysis.Sample(gradient), 8);
        Check(a.Count == 8 && a.Select(x => x.Rgb).SequenceEqual(b.Select(x => x.Rgb)), "Deterministic k-means");
        Check(PaletteAnalysis.Extract(PaletteAnalysis.Sample(gradient), 1).Count == 1, "One cluster");
    }
    private static RecolorSettings Palette()
    {
        var r = new RecolorSettings { Mode = RecolorMode.Palette };
        r.Entries.Add(new PaletteEntry { SourceRgb = 0xbc4646, Target = OklabColor.FromRgb(0x4466dd) });
        r.Entries.Add(PaletteEntry.Identity(0x61aa59));
        return r;
    }
    private static void Mapping()
    {
        var r = Palette(); var transform = new PaletteRecolor(r); var entry = r.Entries[0];
        Check(Near(transform.Transform(entry.Source), entry.EffectiveTarget), "Exact palette anchor");
        Check(Near(transform.Transform(r.Entries[1].Source), r.Entries[1].Source), "Identity anchor protected");
        var reverse = Palette(); reverse.Entries.Reverse(); var other = new PaletteRecolor(reverse);
        var random = new Random(19);
        for (int i = 0; i < 300; i++)
        {
            var c = OklabColor.FromRgb(random.Next(0x1000000)); var result = transform.Transform(c);
            Check(Math.Abs(c.L - result.L) < 1e-6, "Pixel L preservation");
            Check(Near(result, other.Transform(c)), "Order independence");
        }
        r.Entries.ForEach(e => e.Enabled = false);
        Check(Near(new PaletteRecolor(r).Transform(entry.Source), entry.Source), "Disabled rows");
        r = Palette();
        var manual = PaletteEntry.Identity(r.Entries[0].SourceRgb, true);
        r.Entries.Add(manual); r.ReplaceAutomatic(new[] { PaletteEntry.Identity(manual.SourceRgb), PaletteEntry.Identity(123) });
        Check(r.Entries.Count == 2 && r.Entries.First(e => e.SourceRgb == manual.SourceRgb).Manual, "Manual wins regeneration duplicates");
        var near = new RecolorSettings { Mode = RecolorMode.Palette };
        for (int i = 0; i < 30; i++) near.Entries.Add(new PaletteEntry { SourceRgb = 0x808080 + i, Target = OklabColor.FromRgb(0xff5050) });
        var fallback = new PaletteRecolor(near); Check(fallback.UsesFallback, "Ill-conditioned RBF fallback");
        Check(Near(fallback.Transform(near.Entries[3].Source), near.Entries[3].EffectiveTarget), "Fallback exact anchor");
        var match = PaletteAnalysis.Match(new[] { new PaletteColor { Rgb = 0xaaaaaa, Weight = 1 } },
            new[] { new PaletteColor { Rgb = 0x0000ff, Weight = 2 }, new PaletteColor { Rgb = 0xff0000, Weight = 1 } });
        Check(Math.Abs(match[0].Target.Hue - OklabColor.FromRgb(0x0000ff).Hue) < 1e-6, "Single source chooses dominant reference");
        match = PaletteAnalysis.Match(new[] { new PaletteColor { Rgb = 0xdddddd }, new PaletteColor { Rgb = 0x555555 }, new PaletteColor { Rgb = 0x999999 } },
            new[] { new PaletteColor { Rgb = 0x123456 }, new PaletteColor { Rgb = 0xabcdef } });
        Check(match.Count == 3 && match[0].SourceRgb == 0x555555, "Unequal count luminance pairing");
    }
    private static void PersistenceAndRendering()
    {
        var pixels = Pixels(0xbc4646, 0x61aa59, 0xbc4646, 0xbc4646); pixels[15] = 0; pixels[11] = 128;
        var mask = new[] { true, false, true, true }; var settings = Settings(Palette());
        var result = GradeRenderer.Render(pixels, 2, 2, settings, mask).Rgba;
        Check(Rgb(result, 0) != Rgb(pixels, 0), "Selected recolored");
        Check(Rgb(result, 1) == Rgb(pixels, 1) && Rgb(result, 3) == Rgb(pixels, 3), "Mask and transparent untouched");
        Check(result[11] == 128 && result[15] == 0, "Alpha exact");
        var encoded = MiniJson.WriteObject(settings.Capture()); var restored = new GradeSettings(); restored.Replace(MiniJson.ReadObject(encoded));
        Check(GradeRenderer.Render(pixels, 2, 2, restored, mask).Rgba.SequenceEqual(result), "Preset round trip exact");
        var copy = settings.Copy(); settings.Reset(); Check(RecolorSettings.Read(copy).Entries.Count == 2, "Snapshot isolation");
        var legacy = new GradeSettings(); legacy.Replace(MiniJson.ReadObject("{\"Exposure\":0}"));
        Check(RecolorSettings.Read(legacy).Mode == RecolorMode.Off, "Old preset off");
        string png = Path.Combine(output, "rendered.png"); TextureLoader.SavePng(png, result, 2, 2);
        Check(TextureLoader.Load(png).rgba.SequenceEqual(result), "Saved PNG matches render");
        var cancel = new CancellationTokenSource(); cancel.Cancel();
        bool canceled = false; try { GradeRenderer.Render(pixels, 2, 2, copy, null, 0, cancel.Token); } catch (OperationCanceledException) { canceled = true; }
        Check(canceled, "Cancellation");
        var state = Palette(); var mixed = Settings(state); mixed["Exposure"] = 1;
        var once = GradeRenderer.Render(pixels, 2, 2, Settings(state)).Rgba;
        var exposure = new GradeSettings(); exposure["Exposure"] = 1;
        Check(GradeRenderer.Render(pixels, 2, 2, mixed).Rgba.SequenceEqual(GradeRenderer.Render(once, 2, 2, exposure).Rgba), "Recolor before exposure");
    }
    private static void ThresholdBehavior()
    {
        var ramp = Pixels(0, 0x404040, 0x7f7f7f, 0x808080, 0xc0c0c0, 0xffffff);
        var s = new GradeSettings(); s["Threshold"] = 1;
        Check(GradeRenderer.Render(ramp, 6, 1, s).Rgba.SequenceEqual(Pixels(0, 0, 0, 0xffffff, 0xffffff, 0xffffff)), "Legacy threshold keeps midpoint");
        s["ThresholdLevel"] = 64;
        Check(GradeRenderer.Render(ramp, 6, 1, s).Rgba.SequenceEqual(Pixels(0, 0xffffff, 0xffffff, 0xffffff, 0xffffff, 0xffffff)), "Threshold includes exact boundary as white");
        s["ThresholdLevel"] = 192;
        Check(GradeRenderer.Render(ramp, 6, 1, s).Rgba.SequenceEqual(Pixels(0, 0, 0, 0, 0xffffff, 0xffffff)), "Raising threshold expands black");
        s["ThresholdLevel"] = 0;
        Check(GradeRenderer.Render(ramp, 6, 1, s).Rgba.SequenceEqual(Pixels(0xffffff, 0xffffff, 0xffffff, 0xffffff, 0xffffff, 0xffffff)), "Threshold zero includes black as white");
        s["ThresholdLevel"] = 255;
        Check(GradeRenderer.Render(ramp, 6, 1, s).Rgba.SequenceEqual(Pixels(0, 0, 0, 0, 0, 0xffffff)), "Threshold upper boundary exact");
        s["ThresholdLevel"] = 127.5;
        Check(GradeRenderer.Render(Pixels(0xff0000, 0x00ff00, 0x0000ff), 3, 1, s).Rgba.SequenceEqual(Pixels(0, 0xffffff, 0)), "Threshold uses weighted RGB brightness");
        s["Threshold"] = .5;
        Check(GradeRenderer.Render(Pixels(0x404040), 1, 1, s).Rgba.SequenceEqual(Pixels(0x202020)), "Threshold mix remains separate from cutoff");
        s["Threshold"] = 0; s["ThresholdLevel"] = 240;
        Check(GradeRenderer.Render(ramp, 6, 1, s).Rgba.SequenceEqual(ramp), "Cutoff alone does not enable threshold");
        s["Threshold"] = 1;
        var alpha = Pixels(0x404040, 0x808080, 0xc0c0c0); alpha[3] = 128; alpha[11] = 0;
        var expected = Pixels(0, 0x808080, 0xc0c0c0); expected[3] = 128; expected[11] = 0;
        Check(GradeRenderer.Render(alpha, 3, 1, s, new[] { true, false, true }).Rgba.SequenceEqual(expected), "Threshold preserves selection and alpha");
        var encoded = MiniJson.WriteObject(s.Capture()); var restored = new GradeSettings(); restored.Replace(MiniJson.ReadObject(encoded));
        Check(restored["ThresholdLevel"] == 240 && GradeRenderer.Render(ramp, 6, 1, restored).Rgba.SequenceEqual(GradeRenderer.Render(ramp, 6, 1, s).Rgba), "Threshold preset round trip");
        s.Reset(); Check(s["ThresholdLevel"] == 127.5 && s["Threshold"] == 0, "Reset threshold defaults");
        s["LabAmount"] = 100; s["LabTargetA"] = 70; s["LabTargetB"] = -40;
        Check(GradeRenderer.Render(ramp, 6, 1, s).Rgba.SequenceEqual(ramp), "Removed Lab effect cannot apply invisibly");
        var grayscale = new GradeSettings(); grayscale["Grayscale"] = 100;
        var colors = Pixels(0, 0xff0000, 0x4488aa, 0xffffff);
        Check(GradeRenderer.Render(colors, 4, 1, grayscale).Rgba.SequenceEqual(Pixels(0, 0x363636, 0x7c7c7c, 0xffffff)), "Grayscale preserves continuous weighted tones");
    }
    private static byte[] Gradient(int w, int h)
    {
        var data = new byte[w * h * 4];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            int o = (y * w + x) * 4; data[o] = (byte)(30 + x * 200 / w); data[o + 1] = (byte)(30 + y * 200 / h); data[o + 2] = (byte)(50 + (x + y) * 100 / (w + h)); data[o + 3] = 255;
        }
        return data;
    }
    private static void Performance()
    {
        var pixels = Gradient(4096, 4096); var state = Palette(); var sw = Stopwatch.StartNew();
        var preview = GradeRenderer.Render(pixels, 4096, 4096, Settings(state), null, 1024);
        Console.WriteLine("4K / 1024 preview: " + sw.ElapsedMilliseconds + " ms"); Check(preview.Width == 1024, "Preview size");
        sw.Restart(); var full = GradeRenderer.Render(pixels, 4096, 4096, Settings(state));
        Console.WriteLine("4K full render: " + sw.ElapsedMilliseconds + " ms"); Check(full.Width == 4096, "Full render size");
        TextureLoader.SavePng(Path.Combine(output, "palette-preview.png"), preview.Rgba, preview.Width, preview.Height);
    }
    private static object Get(object o, string field) => o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    private static object Call(object o, string name, params object[] values) => o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, values);
    private static void PumpUntil(Func<bool> ready, string label)
    {
        var watch = Stopwatch.StartNew();
        while (!ready() && watch.ElapsedMilliseconds < 15000)
        {
            var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame); Thread.Sleep(5);
        }
        Check(ready(), label);
    }
    private static void Capture(FrameworkElement element, string name, double width, double height)
    {
        element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var backing = new DrawingVisual(); using (var dc = backing.RenderOpen()) dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
        bmp.Render(backing);
        var content = new DrawingVisual();
        using (var dc = content.RenderOpen()) dc.DrawRectangle(new VisualBrush(element) { AutoLayoutContent = false }, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        bmp.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
        using (var stream = File.Create(Path.Combine(output, name))) encoder.Save(stream);
    }
    private static void UiIntegration()
    {
        if (Application.Current == null) new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        L.Set(Lang.ZhCn);
        string texture = Path.Combine(output, "source.png"); var original = Gradient(128, 128); TextureLoader.SavePng(texture, original, 128, 128);
        var panel = new MainPanel(new FakeBridge(texture));
        var settings = (GradeSettings)Get(panel, "Settings");
        Call(panel, "ChangeRecolor", new Action<RecolorSettings>(r => { var p = Palette(); r.Mode = p.Mode; r.Entries = p.Entries; }), true);
        PumpUntil(() => Get(panel, "_gradeCancel") == null, "UI render completion");
        Check(RecolorSettings.Read(settings).Entries.Count == 2, "UI state update");
        panel.Undo(); Check(RecolorSettings.Read(settings).Entries.Count == 0, "Undo palette");
        panel.Redo(); Check(RecolorSettings.Read(settings).Entries.Count == 2, "Redo palette");
        Call(panel, "BeginHistorySession");
        for (int i = 0; i < 6; i++) Call(panel, "ChangeRecolor", new Action<RecolorSettings>(r => r.Entries[0].Target = OklabColor.FromRgb(0x224466 + i * 0x100)), false);
        Call(panel, "CommitHistorySession"); panel.Undo(); Check(Near(RecolorSettings.Read(settings).Entries[0].Target, Palette().Entries[0].Target), "Color edit one undo step"); panel.Redo();
        var list = (ListBox)Get(panel, "_materialList"); list.SelectedIndex = 1;
        Check(RecolorSettings.Read(settings).Mode == RecolorMode.Off, "Independent material default");
        list.SelectedIndex = 0; Check(Near(RecolorSettings.Read(settings).Entries[0].Target, OklabColor.FromRgb(0x224966)), "Material restoration");
        var preset = settings.Capture(); Call(panel, "ApplyParams", new Dictionary<string, double> { ["Exposure"] = 0 });
        Check(RecolorSettings.Read(settings).Mode == RecolorMode.Off, "Old preset clears new state");
        Call(panel, "ApplyParams", preset); Check(RecolorSettings.Read(settings).Entries.Count == 2, "New preset restores dynamic rows");
        // Cancel an actual color-edit transaction through the dialog's dispatcher.
        var before = MiniJson.WriteObject(settings.Capture());
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<ColorPickerWindow>().Single();
            dialog.SelectMode(ColorPickerMode.Rgb);
            Call(dialog, "Set", OklabColor.FromRgb(0xeeee00), true); dialog.DialogResult = false;
        }));
        Call(panel, "EditPaletteColor", 0, false);
        Check(before == MiniJson.WriteObject(settings.Capture()), "Cancel color picker restores all state");
        // Rapid updates must end with the current state, never the preceding async render.
        for (int i = 0; i < 6; i++) Call(panel, "ChangeRecolor", new Action<RecolorSettings>(r => r.Entries[0].Target = OklabColor.FromRgb(0x665544 + i * 0x1000)), false);
        Call(panel, "RunGradeAsync"); PumpUntil(() => Get(panel, "_gradeCancel") == null, "Latest async render completed");
        var wb = (WriteableBitmap)Get(panel, "_wb"); var actual = new byte[128 * 128 * 4]; wb.CopyPixels(actual, 128 * 4, 0);
        var expected = (byte[])Call(panel, "ComputeGraded");
        for (int i = 0; i < expected.Length; i += 4) { byte t = expected[i]; expected[i] = expected[i + 2]; expected[i + 2] = t; }
        Check(actual.SequenceEqual(expected), "Async preview equals final renderer");
        var root = (StackPanel)Get(panel, "_recolorRoot"); var parent = (Expander)root.Parent; parent.IsExpanded = true;
        Capture(panel, "ui-main.png", 1200, 950);
        Capture(root, "ui-recolor.png", 510, 1200);
        Check(!root.Children.OfType<StackPanel>().SelectMany(p => Descendants<Slider>(p)).Any(), "Palette controls and mapping rows have no strength or influence sliders");
        foreach (Lang lang in Enum.GetValues(typeof(Lang)))
        {
            L.Set(lang); panel.ApplyLanguage(); Check(!L.T("Rc.Title").StartsWith("Rc."), "Localized title " + lang);
            Check(DefaultManual.Get(lang).Contains("OKLCH"), "Localized manual " + lang);
        }
        L.Set(Lang.ZhCn);
        RemovedTransferUi(panel);
        PickerModes();
        ThresholdUi(panel);
        Call(panel, "CancelRecolorWork");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found) yield return found;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void ThresholdUi(MainPanel panel)
    {
        panel.ApplyLanguage();
        var settings = (GradeSettings)Get(panel, "Settings");
        Check(!(bool)Call(panel, "HasSlider", "LabAmount"), "No Lab settings in UI or presets");
        Call(panel, "ApplyParams", new Dictionary<string, double> { ["LabAmount"] = 100, ["LabTargetL"] = 40 });
        Check(!settings.Capture().ContainsKey("LabAmount") && !(bool)Call(panel, "HasNonDefaultParams"), "Legacy Lab preset is ignored");
        var fx = Descendants<Expander>(panel).Single(e => (string)e.Header == L.T("Grp.Fx")); fx.IsExpanded = true;
        Capture(panel, "ui-main-effects.png", 1200, 950);
        var input = Descendants<TextBox>(fx).Single(t => t.Tag as string == "ThresholdLevel");
        input.SetCurrentValue(TextBox.TextProperty, "192");
        input.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, panel) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
        Check(settings["ThresholdLevel"] == 192 && !(bool)Call(panel, "HasNonDefaultParams"), "Typed cutoff stays inactive until strength enabled");
        panel.Undo(); Check(settings["ThresholdLevel"] == 127.5, "Cutoff input undo");
        panel.Redo(); Check(settings["ThresholdLevel"] == 192, "Cutoff input redo");
        input.SetCurrentValue(TextBox.TextProperty, "999");
        input.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, panel) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
        Check(settings["ThresholdLevel"] == 192 && input.Text == "192", "Invalid cutoff restores field");
        panel.ResetParams(); Check(settings["ThresholdLevel"] == 127.5, "Reset cutoff even when effect is off");
        var sliders = Descendants<Slider>(fx).ToArray();
        sliders.Single(s => s.Tag as string == "Threshold").Value = 1;
        sliders.Single(s => s.Tag as string == "ThresholdLevel").Value = 192;
        var list = (ListBox)Get(panel, "_materialList"); list.SelectedIndex = 1; list.SelectedIndex = 0;
        Check(settings["Threshold"] == 1 && settings["ThresholdLevel"] == 192, "Threshold material memory");
        Capture((FrameworkElement)fx.Content, "ui-effects.png", 510, 250);
        foreach (Lang lang in Enum.GetValues(typeof(Lang)))
        {
            L.Set(lang);
            Check(!L.T("ThresholdLevel").StartsWith("Threshold") || lang == Lang.En, "Localized threshold label " + lang);
            Check(DefaultManual.Get(lang).Contains("127.5") && !DefaultManual.Get(lang).Contains("Lab "), "Updated effect manual " + lang);
        }
        L.Set(Lang.ZhCn);
    }
    private static void PickerModes()
    {
        var random = new Random(491);
        foreach (int rgb in new[] { 0, 0xffffff, 0xff0000, 0x00ff00, 0x0000ff, 0xff00ff, 0xffff00, 0x00ffff }.Concat(Enumerable.Range(0, 1000).Select(_ => random.Next(0x1000000))))
            Check(HsvColor.FromRgb(rgb).ToRgb() == rgb, "HSV round trip");

        var picker = new ColorPickerWindow(OklabColor.FromLch(.543212345678, .12, 28.231245), .6, true);
        Check(picker.Mode == ColorPickerMode.Hsv && picker.Locked, "HSV default with independent lightness lock");
        var original = picker.Selected; int changes = 0;
        picker.ColorChanged += (c, locked) => changes++;
        foreach (var mode in new[] { ColorPickerMode.Rgb, ColorPickerMode.Oklch, ColorPickerMode.Hsv })
        {
            ((RadioButton[])Get(picker, "modeButtons"))[(int)mode].IsChecked = true;
            Check(picker.Mode == mode && picker.Selected.Equals(original) && picker.Locked && changes == 0, "Switch picker mode without changing color or lock");
            var number = ((TextBox[])Get(picker, "numbers"))[1];
            number.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, number, picker) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            Check(picker.Selected.Equals(original) && changes == 0, "Unedited rounded field does not quantize selection");
            Capture((FrameworkElement)picker.Content, "ui-picker-" + mode.ToString().ToLowerInvariant() + ".png", 354, mode == ColorPickerMode.Rgb ? 365 : 650);
        }
        foreach (ColorPickerMode mode in Enum.GetValues(typeof(ColorPickerMode)))
        {
            picker.SelectMode(mode);
            Call(picker, "ChangeComponent", mode == ColorPickerMode.Oklch ? 1 : 0, mode == ColorPickerMode.Oklch ? .4 : 220.0);
            Check(Math.Abs(picker.Selected.L - .6) < 1e-6 && OklabColor.InGamut(picker.Selected), "Independent lightness lock in " + mode);
        }
        ((CheckBox)Get(picker, "lockBox")).IsChecked = false;
        Call(picker, "Set", OklabColor.FromRgb(0xff0000), false);
        picker.SelectMode(ColorPickerMode.Hsv); Call(picker, "ChangeComponent", 0, 120.0);
        Check(OklabColor.ToRgb(picker.Selected) == 0x00ff00, "HSV hue edit");
        picker.SelectMode(ColorPickerMode.Rgb); Call(picker, "ChangeComponent", 2, 255.0);
        Check(OklabColor.ToRgb(picker.Selected) == 0x00ffff, "RGB blue-channel edit");
        picker.SelectMode(ColorPickerMode.Oklch); Call(picker, "ChangeComponent", 0, .4);
        Check(Math.Abs(picker.Selected.L - .4) < 1e-6, "Unlocked OKLCH L edit");
        picker.SelectMode(ColorPickerMode.Hsv); Call(picker, "Set", OklabColor.FromRgb(0), false);
        Call(picker, "ChangeComponent", 0, 240.0); Call(picker, "ChangeComponent", 1, 100.0); Call(picker, "ChangeComponent", 2, 100.0);
        Check(OklabColor.ToRgb(picker.Selected) == 0x0000ff, "HSV hue and saturation survive black");
        picker.Close();
        var sourcePicker = new ColorPickerWindow(OklabColor.FromRgb(0x556677), null, true);
        Check(!sourcePicker.Locked, "Source picker is freely editable"); sourcePicker.Close();
        foreach (Lang lang in Enum.GetValues(typeof(Lang)))
        {
            L.Set(lang); Check(!L.T("Rc.PickerMode").StartsWith("Rc."), "Localized picker mode " + lang);
            Check(DefaultManual.Get(lang).Contains("HSV"), "Manual describes picker choices " + lang);
        }
        L.Set(Lang.ZhCn);
    }
    private sealed class FakeBridge : IPMDBridge
    {
        private readonly string texture;
        public FakeBridge(string path) { texture = path; }
        public string PmxDirectory => Path.GetDirectoryName(texture);
        public ModelSnapshot LoadCurrent() => new ModelSnapshot("fixture.pmx", PmxDirectory,
            new[] { new MaterialInfo(0, "测试材质", texture, texture, true), new MaterialInfo(1, "第二材质", texture, texture, true) });
        public void ApplyPreview(int i, string path) { }
        public void ApplySaved(int i, string path) { }
        public void Revert(int i) { }
        public void Cleanup() { }
        public int[] GetPmxSelectedVertices() => new int[0];
        public void SetPmxSelectedVertices(int[] vertices) { }
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3)> GetUVTriangles(int i)
            => new[] { (0f, 0f, 1f, 0f, 1f, 1f), (0f, 0f, 1f, 1f, 0f, 1f) };
        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3, int i1, int i2, int i3)> GetUVTrianglesWithIndices(int i)
            => new[] { (0f, 0f, 1f, 0f, 1f, 1f, 0, 1, 2), (0f, 0f, 1f, 1f, 0f, 1f, 0, 2, 3) };
    }
}
