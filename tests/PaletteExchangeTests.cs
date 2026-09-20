using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;
using TextureGrade.Models;
using TextureGrade.WpfUI;
using TextureGrade.TextureIO;

internal static partial class Program
{
    private static void PaletteExchangeIntegration()
    {
        L.Set(Lang.ZhCn);
        var state = new RecolorSettings { Mode = RecolorMode.Palette, AutoDetectCount = true };
        state.Entries.Add(new PaletteEntry { SourceRgb = 0x202040, Target = OklabColor.FromRgb(0xff2050), Manual = true, TransferChroma = true });
        state.Entries.Add(new PaletteEntry { SourceRgb = 0xc0d0b0, Target = OklabColor.FromRgb(0x20ff60), Enabled = false, LockLightness = false });
        var first = state.Entries[0].Copy(); var second = state.Entries[1].Copy();
        var original = MiniJson.WriteObject(Settings(state).Capture());
        Check(!state.SwapColors(-1, 0, true) && !state.SwapColors(0, 2, false) && !state.SwapColors(1, 1, false), "Invalid/self exchange is a no-op");
        Check(original == MiniJson.WriteObject(Settings(state).Capture()), "Invalid exchange preserves complete settings");
        Check(state.SwapColors(0, 1, false), "Target column exchange accepted");
        Check(state.Entries[0].SourceRgb == first.SourceRgb && state.Entries[1].SourceRgb == second.SourceRgb &&
            Near(state.Entries[0].Target, second.Target, double.Epsilon) && Near(state.Entries[1].Target, first.Target, double.Epsilon), "Target exchange swaps raw unquantized colors only");
        Check(Math.Abs(state.Entries[0].EffectiveTarget.L - first.Source.L) < 1e-6 &&
            Math.Abs(state.Entries[1].EffectiveTarget.L - first.Target.L) < 1e-6, "Destination row controls lightness lock, including unlocked reference L");
        Check(state.Entries[0].Manual && state.Entries[0].TransferChroma && state.Entries[0].Enabled &&
            !state.Entries[1].Manual && !state.Entries[1].TransferChroma && !state.Entries[1].Enabled, "Exchange preserves per-row metadata");
        state.SwapColors(1, 0, false);
        Check(original == MiniJson.WriteObject(Settings(state).Capture()), "Two target exchanges restore exact state without gamut/quantization drift");
        state.SwapColors(0, 1, true);
        Check(state.Entries[0].SourceRgb == second.SourceRgb && state.Entries[1].SourceRgb == first.SourceRgb &&
            Near(state.Entries[0].Target, first.Target, double.Epsilon) && Near(state.Entries[1].Target, second.Target, double.Epsilon), "Source exchange leaves target column in place");
        Check(RecolorSettings.Read(Settings(state)).Entries.Count == 2 && OklabColor.InGamut(state.Entries[0].EffectiveTarget), "Source permutation neither merges rows nor leaves gamut");
        state.SwapColors(0, 1, true);
        Check(original == MiniJson.WriteObject(Settings(state).Capture()), "Two source exchanges restore exact state");

        // A long palette exercises scrolling and incremental visual updates using real controls.
        state.Entries.Clear();
        for (int i = 0; i < 128; i++) state.Entries.Add(new PaletteEntry { SourceRgb = (i + 40) * 0x010101,
            Target = OklabColor.FromLch(.65, .13, i * 360.0 / 128), TransferChroma = true, Manual = i == 0 });
        string texture = Path.Combine(output, "exchange-source.png");
        TextureLoader.SavePng(texture, Pixels(state.Entries[0].SourceRgb, state.Entries[127].SourceRgb), 2, 1);
        var panel = new MainPanel(new FakeBridge(texture));
        // A hidden native presentation source makes WPF visibility/hit testing meaningful
        // without showing a test window or driving the user's PMXEditor session.
        using var host = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("PaletteExchangeTests")
            { Width = 1250, Height = 900, WindowStyle = unchecked((int)0x80000000), PositionX = -32000, PositionY = -32000 });
        host.RootVisual = panel;
        Call(panel, "ChangeRecolor", new Action<RecolorSettings>(r => { r.Mode = state.Mode; r.Entries = state.Entries; }), true);
        var settings = (GradeSettings)Get(panel, "Settings");
        Call(panel, "CancelRecolorWork"); Call(panel, "ResetHistory");
        var root = (StackPanel)Get(panel, "_recolorRoot"); ((Expander)root.Parent).IsExpanded = true;
        panel.Width = 1250; panel.Height = 900;
        panel.Measure(new Size(1250, 900)); panel.Arrange(new Rect(0, 0, 1250, 900)); panel.UpdateLayout();
        var scroll = (ScrollViewer)Get(panel, "AdjustScroll");
        var buttons = Descendants<Button>(root).Where(b => (string)b.Tag == "PaletteTarget").ToArray();
        Check(buttons.Length == 128 && scroll.ScrollableHeight > 1000, "128 mapping rows in a scrollable viewport");
        int changes = 0; settings.PropertyChanged += (s, e) => changes++;
        int version = (int)Get(panel, "_gradeVersion");
        string before = MiniJson.WriteObject(settings.Capture());
        var rowsBefore = _PaletteRows(panel).Children.Cast<UIElement>().ToArray();
        var clickMode = (CheckBox)Get(panel, "_paletteExchangeCheck"); clickMode.IsChecked = true;
        buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Get(panel, "_paletteExchange") != null && ((TextBlock)Get(panel, "PaletteSwapStatus")).Visibility == Visibility.Visible,
            "First click arms exchange and pins a cancelable status above the scroller");
        scroll.ScrollToBottom(); panel.UpdateLayout(); double offset = scroll.VerticalOffset;
        Check(Get(panel, "_paletteExchange") != null && changes == 0 && version == (int)Get(panel, "_gradeVersion"),
            "Long-distance scrolling keeps the origin without notifying settings or queuing renders");
        var color0 = state.Entries[0].Target; var color127 = state.Entries[127].Target;
        buttons[127].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var after = RecolorSettings.Read(settings);
        Check(Near(after.Entries[0].Target, color127, double.Epsilon) && Near(after.Entries[127].Target, color0, double.Epsilon), "Far-apart target buttons exchange on second click");
        Check(changes == 1 && ((IList)Get(panel, "_undo")).Count == 1, "Exchange is one settings notification and one history entry");
        Check(rowsBefore.SequenceEqual(_PaletteRows(panel).Children.Cast<UIElement>()) && scroll.VerticalOffset == offset,
            "Exchange keeps all row controls and scroll offset instead of rebuilding the panel");
        var targetContent = (StackPanel)buttons[127].Content;
        Check(((TextBlock)targetContent.Children[1]).Text == "#" + OklabColor.ToRgb(after.Entries[127].EffectiveTarget).ToString("X6"),
            "Target swatch shows actual destination gamut/lightness result");
        string exchanged = MiniJson.WriteObject(settings.Capture());
        byte[] graded = (byte[])Call(panel, "ComputeGraded");
        panel.Undo(); Check(MiniJson.WriteObject(settings.Capture()) == before, "One Undo restores complete palette");
        panel.Redo(); Check(MiniJson.WriteObject(settings.Capture()) == exchanged, "One Redo restores exchange");
        Check(graded.SequenceEqual((byte[])Call(panel, "ComputeGraded")), "Redo reproduces renderer output");
        var restored = new GradeSettings(); restored.Replace(MiniJson.ReadObject(MiniJson.WriteObject(settings.Capture())));
        Check(GradeRenderer.Render(Pixels(state.Entries[0].SourceRgb, state.Entries[127].SourceRgb), 2, 1, restored).Rgba.SequenceEqual(graded),
            "Exchanged palette survives numeric JSON preset round-trip");
        Call(panel, "CancelRecolorWork"); changes = 0;
        int history = ((IList)Get(panel, "_undo")).Count;
        Check((bool)Call(panel, "BeginPaletteExchange", 0, false), "Arm exchange for cancel scenarios");
        Check(!(bool)Call(panel, "CompletePaletteExchange", 1, true) && Get(panel, "_paletteExchange") != null,
            "Cross-column click is rejected without losing the selected color");
        ((Button)Get(panel, "PaletteSwapCancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Get(panel, "_paletteExchange") == null && !(bool)Get(panel, "_paletteClickExchange") && changes == 0 &&
            ((IList)Get(panel, "_undo")).Count == history, "Cancel button leaves no settings/history changes and returns to color editing");
        Call(panel, "BeginPaletteExchange", 0, true); Call(panel, "CompletePaletteExchange", 0, true);
        Check(changes == 0 && Get(panel, "_paletteExchange") == null, "Dropping on itself cancels without work");
        Call(panel, "BeginPaletteExchange", 0, false);
        settings["Exposure"] = .2;
        Check(Get(panel, "_paletteExchange") == null && !(bool)Call(panel, "CompletePaletteExchange", 2, false), "Settings change invalidates a pending exchange");
        Call(panel, "BeginPaletteExchange", 0, false); Call(panel, "RefreshRecolorUi");
        Check(Get(panel, "_paletteExchange") == null, "Palette rebuild cancels stale indices");

        // Hit testing and edge-scroll math use WPF DIPs, independent of image/monitor resolution.
        scroll.ScrollToTop(); panel.UpdateLayout();
        var firstButton = Descendants<Button>(_PaletteRows(panel)).First(b => (string)b.Tag == "PaletteSource");
        firstButton.BringIntoView(); panel.UpdateLayout();
        var point = firstButton.TranslatePoint(new Point(firstButton.ActualWidth / 2, firstButton.ActualHeight / 2), scroll);
        Check(Call(panel, "PaletteCellAt", point) != null && Call(panel, "PaletteCellAt", new Point(-1, 20)) == null,
            "Drag hit testing recognizes the full color button and rejects outside drops");
        var speed = typeof(MainPanel).GetMethod("PaletteScrollSpeed", BindingFlags.NonPublic | BindingFlags.Static);
        double Speed(double x, double y, double w = 400, double h = 400) => (double)speed.Invoke(null, new object[] { new Point(x, y), w, h });
        Check(Speed(200, 200) == 0 && Speed(-1, 399) == 0 && Speed(200, 401) == 0, "Drag stays still in middle or outside viewport");
        Check(Speed(200, 1) < Speed(200, 20) && Speed(200, 20) < 0 && Speed(200, 399) > Speed(200, 380) && Speed(200, 380) > 0,
            "Edge speed accelerates smoothly toward either edge");
        Check(Speed(50, 40, 100, 80) == 0, "Small viewport retains a neutral middle");
        Call(panel, "CancelRecolorWork");
        Call(panel, "BeginPaletteExchange", 0, true);
        typeof(MainPanel).GetField("_paletteDragging", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, true);
        changes = 0; version = (int)Get(panel, "_gradeVersion");
        var update = typeof(MainPanel).GetMethod("UpdatePaletteDrag", BindingFlags.Instance | BindingFlags.NonPublic);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++) update.Invoke(panel, new object[] { point, 0.0 });
        clock.Stop();
        Check(changes == 0 && version == (int)Get(panel, "_gradeVersion"), "2000 drag updates never mutate settings or queue texture rendering");
        Console.WriteLine("128-row palette / 2000 drag hover updates: " + clock.ElapsedMilliseconds + " ms");
        double oldOffset = scroll.VerticalOffset;
        Call(panel, "UpdatePaletteDrag", new Point(100, scroll.ViewportHeight - 1), .1); panel.UpdateLayout();
        Check(scroll.VerticalOffset > oldOffset, "Edge drag advances real scroll viewport");
        // Escape is a routed event and must stop the drag timer and any captured interaction.
        panel.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(), 0, Key.Escape)
            { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Check(Get(panel, "_paletteExchange") == null && !(bool)Get(panel, "_paletteDragging") &&
            !((DispatcherTimer)Get(panel, "_paletteScrollTimer")).IsEnabled && changes == 0, "Esc cancels drag without committing or leaving a timer running");
        Call(panel, "BeginPaletteExchange", 0, false);
        typeof(MainPanel).GetField("_paletteDragging", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, true);
        int outsideHistory = ((IList)Get(panel, "_undo")).Count;
        Call(panel, "FinishPaletteDrag", new Point(-100, -100));
        Check(Get(panel, "_paletteExchange") == null && changes == 0 && ((IList)Get(panel, "_undo")).Count == outsideHistory,
            "Drop outside the palette cancels without opening a picker or recording an edit");

        // The ordinary click still reaches the color picker, rather than arming an exchange.
        bool pickerOpened = false;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var picker = Application.Current.Windows.OfType<ColorPickerWindow>().Single();
            pickerOpened = true; picker.DialogResult = false;
        }));
        firstButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(pickerOpened && Get(panel, "_paletteExchange") == null, "Normal click opens the existing picker after canceling exchange mode");
        Call(panel, "BeginPaletteExchange", 0, true);
        var materials = (ListBox)Get(panel, "_materialList"); materials.SelectedIndex = 1;
        Check(Get(panel, "_paletteExchange") == null && !(bool)Call(panel, "CompletePaletteExchange", 1, true), "Material switch invalidates an exchange");
        materials.SelectedIndex = 0;
        Check(Near(RecolorSettings.Read(settings).Entries[127].Target, color0, double.Epsilon), "Committed exchange survives material switch");

        var visibleTargets = Descendants<Button>(_PaletteRows(panel)).Where(b => (string)b.Tag == "PaletteTarget").ToArray();
        visibleTargets[1].BringIntoView(); panel.UpdateLayout();
        Point dropPoint = visibleTargets[1].TranslatePoint(new Point(visibleTargets[1].ActualWidth / 2, visibleTargets[1].ActualHeight / 2), scroll);
        Call(panel, "CancelRecolorWork"); changes = 0;
        bool realDrag = (bool)Call(panel, "StartPaletteDrag", 0, false);
        Check(realDrag ? Mouse.Captured == visibleTargets[0] : Get(panel, "_paletteExchange") == null && changes == 0,
            "Drag captures origin or safely cancels when the hidden test host denies capture");
        Call(panel, "CancelPaletteExchange", false);
        // Exercise the same feedback/drop lifecycle without requiring foreground mouse input.
        Call(panel, "BeginPaletteExchange", 0, false); Call(panel, "ShowPaletteDragFeedback");
        Call(panel, "UpdatePaletteDrag", dropPoint, 0.0);
        Check(Get(panel, "_paletteHover") != null && Get(panel, "_paletteGhost") != null && changes == 0,
            "Drag feedback displays a valid target and ghost without editing settings");
        Capture(panel, "ui-palette-exchange-drag.png", 1250, 900);
        Call(panel, "FinishPaletteDrag", dropPoint);
        Check(changes == 1 && Mouse.Captured == null && Get(panel, "_paletteGhost") == null &&
            !((DispatcherTimer)Get(panel, "_paletteScrollTimer")).IsEnabled, "Drop commits once and removes capture, ghost and timer");
        Call(panel, "BeginPaletteExchange", 0, false); Call(panel, "ShowPaletteDragFeedback");
        visibleTargets[0].RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        Check(Get(panel, "_paletteExchange") == null && Get(panel, "_paletteGhost") == null && changes == 1,
            "Mouse capture-loss event cancels the drag without an extra edit");

        foreach (Lang language in Enum.GetValues(typeof(Lang)))
        {
            L.Set(language); panel.ApplyLanguage();
            Check(!L.T("Rc.SwapClickMode").StartsWith("Rc.") && !L.F("Rc.SwapPending", 128, L.T("Rc.TargetColumn")).StartsWith("Rc."),
                "Exchange controls and status localized " + language);
        }
        L.Set(Lang.ZhCn); panel.ApplyLanguage();
        scroll.ScrollToTop(); panel.UpdateLayout();
        Capture(panel, "ui-palette-exchange.png", 1250, 900);
        Call(panel, "BeginPaletteExchange", 0, false);
        Descendants<Button>(_PaletteRows(panel)).Last(b => (string)b.Tag == "PaletteTarget").BringIntoView(); panel.UpdateLayout();
        Capture(panel, "ui-palette-exchange-distant.png", 1250, 900);
        Call(panel, "CancelRecolorWork");
    }

    private static StackPanel _PaletteRows(MainPanel panel)
        => Descendants<StackPanel>((StackPanel)Get(panel, "_recolorRoot")).Single(p => (string)p.Tag == "PaletteMappings");
    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; }
        public override bool IsDisposed => false;
        protected override CompositionTarget GetCompositionTargetCore() => null;
    }
}
