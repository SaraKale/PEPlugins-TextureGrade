using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;
using TextureGrade.Models;
using TextureGrade.TextureIO;

namespace TextureGrade.WpfUI
{
    public partial class MainPanel
    {
        private StackPanel _recolorRoot;
        private StackPanel _paletteRows;
        private bool _buildingRecolor, _colorDialogOpen;
        private bool _referenceExpanded;
        private int _pickPaletteIndex = -1, _gradeVersion, _analysisVersion;
        private CancellationTokenSource _gradeCancel, _analysisCancel;
        private readonly Dictionary<int, ReferenceSession> _references = new Dictionary<int, ReferenceSession>();
        private sealed class ReferenceSession
        {
            public byte[] Rgba;
            public int Width, Height;
            public string Name;
            public BitmapSource Thumbnail;
            public Int32Rect? Crop;
            public bool[] Mask()
            {
                if (!Crop.HasValue) return null;
                var c = Crop.Value; var mask = new bool[Width * Height];
                for (int y = c.Y; y < c.Y + c.Height; y++)
                    for (int x = c.X; x < c.X + c.Width; x++) mask[y * Width + x] = true;
                return mask;
            }
        }
        private UIElement BuildRecolorGroup()
        {
            _recolorRoot = new StackPanel();
            var exp = new Expander { Content = _recolorRoot, HeaderTemplate = (DataTemplate)FindResource("GroupHeader") };
            LocHeader(exp, "Rc.Title");
            InitializePaletteExchange();
            exp.Collapsed += (s, e) => { if (ReferenceEquals(e.OriginalSource, exp)) CancelPaletteExchange(true); };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape && _pickPaletteIndex >= 0) { StopPalettePick(); e.Handled = true; } };
            Unloaded += (s, e) => CancelRecolorWork();
            RefreshRecolorUi();
            return exp;
        }
        private static TextBlock RcText(string key) => new TextBlock { Text = L.T(key), TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(2, 5, 2, 5), Foreground = Brushes.DimGray };
        private static Button RcButton(string key, Action action)
        {
            var b = new Button { Content = L.T(key), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(2), MinHeight = 28 };
            b.Click += (s, e) => action(); return b;
        }
        private void RefreshRecolorUi()
        {
            if (_recolorRoot == null || _buildingRecolor || _colorDialogOpen) return;
            CancelPaletteExchange();
            _paletteCells.Clear();
            _buildingRecolor = true;
            try
            {
                _recolorRoot.Children.Clear(); var state = RecolorSettings.Read(Settings);
                var modes = new WrapPanel();
                string[] keys = { "Rc.Off", "Rc.EnablePalette" };
                for (int i = 0; i < 2; i++)
                {
                    int mode = i;
                    var b = new RadioButton { Content = L.T(keys[i]), IsChecked = (int)state.Mode == i, GroupName = "RecolorMode", Margin = new Thickness(3, 5, 10, 5) };
                    b.Checked += (s, e) => { if (!_buildingRecolor) ChangeRecolor(r => r.Mode = (RecolorMode)mode); };
                    modes.Children.Add(b);
                }
                _recolorRoot.Children.Add(modes);
                _recolorRoot.Children.Add(RcText("Rc.ModeNote"));
                var transfer = new StackPanel();
                transfer.Children.Add(RcText("Rc.ReferenceNote"));
                var imports = new WrapPanel();
                imports.Children.Add(RcButton("Rc.Import", ImportReference));
                imports.Children.Add(RcButton("Rc.FullImage", () =>
                {
                    if (_references.TryGetValue(_currentMatIndex, out var image)) { CancelAnalysis(); image.Crop = null; RefreshRecolorUi(); }
                }));
                transfer.Children.Add(imports);
                if (_references.TryGetValue(_currentMatIndex, out var reference))
                {
                    transfer.Children.Add(new TextBlock { Text = reference.Name, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 4, 2, 4) });
                    var preview = new ReferencePreview { Height = 190, Image = reference.Thumbnail, PixelWidth = reference.Width,
                        PixelHeight = reference.Height, Crop = reference.Crop, Cursor = Cursors.Cross, ToolTip = L.T("Rc.CropNote") };
                    preview.CropChanged += crop => { reference.Crop = crop; CancelAnalysis(); Status(L.T("Rc.Candidate")); };
                    transfer.Children.Add(preview); transfer.Children.Add(RcText("Rc.CropNote"));
                }
                else transfer.Children.Add(RcText("Rc.NoReference"));
                transfer.Children.Add(CountEditor("Rc.ReferenceCount", state.ReferenceCount, 0, RecolorSettings.MaxPaletteColors, n => ChangeRecolor(r => r.ReferenceCount = n, false), state.AutoReferenceCount));
                transfer.Children.Add(AutomaticCountEditor(state.AutoReferenceCount, value => ChangeRecolor(r => r.AutoReferenceCount = value)));
                var locked = new CheckBox { Content = L.T("Rc.Lock"), IsChecked = state.ReferenceLock, Margin = new Thickness(3, 6, 3, 6) };
                locked.Checked += (s, e) => { if (!_buildingRecolor) ChangeRecolor(r => r.ReferenceLock = true); };
                locked.Unchecked += (s, e) => { if (!_buildingRecolor) ChangeRecolor(r => r.ReferenceLock = false); };
                transfer.Children.Add(locked);
                transfer.Children.Add(RcButton("Rc.Generate", () => AnalyzeReference(true)));
                var referenceExp = new Expander { Header = L.T("Rc.Reference"), Content = transfer, IsExpanded = _referenceExpanded, Margin = new Thickness(8, 6, 0, 6) };
                referenceExp.Expanded += (s, e) => { if (ReferenceEquals(e.OriginalSource, referenceExp)) _referenceExpanded = true; };
                referenceExp.Collapsed += (s, e) => { if (ReferenceEquals(e.OriginalSource, referenceExp)) _referenceExpanded = false; };
                _recolorRoot.Children.Add(referenceExp);
                // Keep palette controls next to their mappings, even while the reference section is expanded.
                var palette = new StackPanel { Tag = "PaletteEditor" };
                palette.Children.Add(CountEditor("Rc.Count", state.DetectCount, 1, RecolorSettings.MaxPaletteColors, n => ChangeRecolor(r => r.DetectCount = n, false), state.AutoDetectCount));
                palette.Children.Add(AutomaticCountEditor(state.AutoDetectCount, value => ChangeRecolor(r => r.AutoDetectCount = value)));
                palette.Children.Add(BuildPaletteExchangeTools());
                var actions = new WrapPanel();
                var detect = RcButton("Rc.Detect", () => AnalyzeReference(false)); detect.ToolTip = L.T("Rc.DetectNote");
                actions.Children.Add(detect);
                actions.Children.Add(RcButton("Rc.Add", () => EditPaletteColor(-1, true)));
                actions.Children.Add(RcButton("Rc.Reset", () => ChangeRecolor(r => { r.Entries.Clear(); r.Mode = RecolorMode.Off; })));
                palette.Children.Add(actions);
                var rows = _paletteRows = new StackPanel { Tag = "PaletteMappings" };
                for (int i = 0; i < state.Entries.Count; i++) rows.Children.Add(BuildPaletteRow(i, state.Entries[i]));
                palette.Children.Add(rows);
                palette.Children.Add(RcText("Rc.AutomaticWeights"));
                _recolorRoot.Children.Add(palette); _recolorRoot.Children.Add(RcText("Rc.GamutNote"));
            }
            finally { _buildingRecolor = false; }
        }
        private UIElement AutomaticCountEditor(bool value, Action<bool> change)
        {
            var check = new CheckBox { Content = L.T("Rc.AutoCount"), IsChecked = value, Tag = "AutoCount",
                ToolTip = L.T("Rc.AutoCountNote"), Margin = new Thickness(3, 4, 3, 4) };
            check.Checked += (s, e) => { if (!_buildingRecolor) change(true); };
            check.Unchecked += (s, e) => { if (!_buildingRecolor) change(false); };
            return check;
        }
        private UIElement CountEditor(string key, int value, int min, int max, Action<int> change, bool automatic = false)
        {
            var row = new DockPanel { Margin = new Thickness(2, 4, 2, 4) };
            var input = new TextBox { Text = value.ToString(), Width = 52, Padding = new Thickness(4), IsEnabled = !automatic,
                ToolTip = automatic ? L.T("Rc.AutoCountNote") : null };
            DockPanel.SetDock(input, Dock.Right); row.Children.Add(input); row.Children.Add(RcText(automatic ? key + "Auto" : key));
            Action commit = () =>
            {
                if (automatic) return;
                if (!int.TryParse(input.Text, out int n) || n < min || n > max) { input.Text = value.ToString(); Status(L.F("Rc.NumberRange", min, max)); return; }
                if (n != value) { value = n; change(n); }
            };
            input.LostKeyboardFocus += (s, e) => commit();
            input.KeyDown += (s, e) => { if (e.Key == Key.Enter) { commit(); e.Handled = true; } };
            return row;
        }
        private UIElement BuildPaletteRow(int index, PaletteEntry entry)
        {
            var panel = new StackPanel();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(27) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = (index + 1).ToString(),
                Foreground = Brushes.DimGray, VerticalAlignment = VerticalAlignment.Center });
            var source = PaletteColorButton(index, true, entry.SourceRgb);
            Grid.SetColumn(source, 1); row.Children.Add(source);
            var arrow = new TextBlock { Text = " → ", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
            var target = PaletteColorButton(index, false, OklabColor.ToRgb(entry.EffectiveTarget));
            Grid.SetColumn(target, 3); row.Children.Add(target);
            panel.Children.Add(row);
            var options = new WrapPanel();
            var enabled = new CheckBox { IsChecked = entry.Enabled, Content = L.T(entry.Manual ? "Rc.Manual" : "Rc.Auto"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 5, 0) };
            enabled.Checked += (s, e) => { if (!_buildingRecolor) ChangeRecolor(r => r.Entries[index].Enabled = true); };
            enabled.Unchecked += (s, e) => { if (!_buildingRecolor) ChangeRecolor(r => r.Entries[index].Enabled = false); };
            options.Children.Add(enabled);
            options.Children.Add(RcButton("Rc.Pick", () => { CancelPaletteExchange(true); _pickPaletteIndex = index; ViewRoot.Cursor = Cursors.Cross; ViewRoot.Focus(); Status(L.T("Rc.PickHint")); }));
            options.Children.Add(RcButton("Rc.Delete", () => ChangeRecolor(r => r.Entries.RemoveAt(index))));
            var locked = new CheckBox { Content = L.T("Rc.Lock"), IsChecked = entry.LockLightness, Margin = new Thickness(2, 2, 2, 2), VerticalAlignment = VerticalAlignment.Center };
            locked.Checked += (s, e) => { if (!_buildingRecolor) ChangeRecolor(r => r.Entries[index].LockLightness = true); };
            locked.Unchecked += (s, e) => { if (!_buildingRecolor) ChangeRecolor(r => r.Entries[index].LockLightness = false); };
            options.Children.Add(locked); panel.Children.Add(options);
            return new Border { Child = panel, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Padding = new Thickness(5), Margin = new Thickness(0, 5, 0, 0) };
        }
        private Button ColorButton(int rgb, string key, Action action)
        {
            var button = RcButton(key, action);
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(new Border { Background = new SolidColorBrush(OklchWheel.ToColor(rgb)), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Width = 24, Height = 22, Margin = new Thickness(0, 0, 4, 0) });
            content.Children.Add(new TextBlock { Text = "#" + rgb.ToString("X6"), VerticalAlignment = VerticalAlignment.Center });
            button.Content = content; button.ToolTip = L.T(key); return button;
        }
        private void ChangeRecolor(Action<RecolorSettings> edit, bool refresh = true)
        {
            if (_buildingRecolor) return;
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }
            var before = Snapshot(); var state = RecolorSettings.Read(Settings);
            edit(state); state.Write(Settings);
            CommitRecolor(before);
            if (refresh) RefreshRecolorUi();
        }
        private void CommitRecolor(Dictionary<string, double> before)
        {
            if (_sessionActive || _suppressHistory) return;
            var after = Snapshot();
            if (!SameSnapshot(before, after)) { PushHistory(before); _prevSnapshot = after; UpdateUndoButtons(); }
        }
        private void EditPaletteColor(int index, bool source)
        {
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }
            CancelPaletteExchange();
            StopPalettePick(); var before = Snapshot(); var original = RecolorSettings.Read(Settings);
            var entry = index < 0 ? PaletteEntry.Identity(0x808080, true) : original.Entries[index].Copy();
            bool identity = OklabColor.DistanceSquared(entry.Source, entry.EffectiveTarget) < 1e-15;
            var dialog = new ColorPickerWindow(source ? entry.Source : entry.EffectiveTarget, source ? (double?)null : entry.Source.L, entry.LockLightness);
            var form = System.Windows.Forms.Form.ActiveForm;
            if (form != null) new System.Windows.Interop.WindowInteropHelper(dialog).Owner = form.Handle;
            _colorDialogOpen = true;
            Action<Oklab, bool> apply = (color, locked) =>
            {
                var scratch = new GradeSettings(); scratch.Replace(before); var state = RecolorSettings.Read(scratch);
                var changed = entry.Copy();
                if (source)
                {
                    changed.SourceRgb = OklabColor.ToRgb(color); changed.Manual = true;
                    if (identity) changed.Target = changed.Source;
                }
                else { changed.Target = color; changed.LockLightness = locked; }
                if (index < 0) state.Entries.Add(changed); else state.Entries[index] = changed;
                state.Entries = RecolorSettings.Unique(state.Entries); state.Mode = RecolorMode.Palette; state.Write(Settings);
            };
            dialog.ColorChanged += apply;
            bool accepted = false;
            try
            {
                accepted = dialog.ShowDialog() == true;
                if (accepted) apply(dialog.Selected, dialog.Locked); else Settings.Replace(before);
            }
            finally { _colorDialogOpen = false; }
            if (accepted) CommitRecolor(before);
            RefreshRecolorUi(); RunGradeAsync();
        }
        private bool TryPickPaletteSource(Point viewPoint)
        {
            if (_pickPaletteIndex < 0) return false;
            if (!TryScenePoint(viewPoint, out var point) || point.X < 0 || point.Y < 0 || point.X >= _w || point.Y >= _h) return true;
            int o = ((int)point.Y * _w + (int)point.X) * 4;
            if (_originalBytes[o + 3] == 0) { Status(L.T("Rc.NoPixels")); return true; }
            int rgb = (_originalBytes[o] << 16) | (_originalBytes[o + 1] << 8) | _originalBytes[o + 2];
            int index = _pickPaletteIndex; StopPalettePick();
            ChangeRecolor(r =>
            {
                if (index >= r.Entries.Count) return;
                var entry = r.Entries[index]; bool identity = OklabColor.DistanceSquared(entry.Source, entry.EffectiveTarget) < 1e-15;
                entry.SourceRgb = rgb; entry.Manual = true; if (identity) entry.Target = entry.Source;
                r.Entries = RecolorSettings.Unique(r.Entries); r.Mode = RecolorMode.Palette;
            });
            return true;
        }
        private void StopPalettePick() { _pickPaletteIndex = -1; ViewRoot.Cursor = null; }
        private bool[] CurrentRecolorMask() => !WholeTextureScope && _selectedTris.Count > 0 ? (_maskCache ?? (_maskCache = BuildMask())) : null;
        private void CancelAnalysis() { _analysisVersion++; _analysisCancel?.Cancel(); }
        private void CancelRecolorWork() { CancelPaletteExchange(); _debounce.Stop(); InvalidateGrade(); CancelAnalysis(); CancelSharedTextureContext(); StopPalettePick(); }
        private void InvalidateGrade() { _gradeVersion++; _gradeCancel?.Cancel(); }

        private async void ImportReference()
        {
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = L.T("Rc.Import"), Filter = L.T("Rc.ImageFilter") };
            if (dialog.ShowDialog() != true) return;
            CancelAnalysis(); int version = _analysisVersion, material = _currentMatIndex;
            Status(L.T("Rc.Working"));
            try
            {
                var loaded = await Task.Run(() =>
                {
                    var data = TextureLoader.Load(dialog.FileName);
                    return new ReferenceSession { Rgba = data.rgba, Width = data.width, Height = data.height, Name = System.IO.Path.GetFileName(dialog.FileName) };
                });
                if (version != _analysisVersion || material != _currentMatIndex) return;
                var thumb = GradeRenderer.Render(loaded.Rgba, loaded.Width, loaded.Height, new GradeSettings(), null, 512);
                var image = BitmapSource.Create(thumb.Width, thumb.Height, 96, 96, PixelFormats.Bgra32, null,
                    RgbaToBgra(thumb.Rgba, thumb.Width, thumb.Height), thumb.Width * 4); image.Freeze(); loaded.Thumbnail = image;
                _references[material] = loaded; RefreshRecolorUi(); Status(L.T("Rc.Candidate"));
            }
            catch (Exception ex) { if (version == _analysisVersion) Status(L.F("Rc.Failed", ex.Message)); }
        }
        private async void AnalyzeReference(bool useReference)
        {
            if (_originalBytes == null) { Status(L.T("St.NeedTexture")); return; }
            if (useReference && !_references.ContainsKey(_currentMatIndex)) { Status(L.T("Rc.NoReference")); return; }
            CancelAnalysis(); var cancel = new CancellationTokenSource(); _analysisCancel = cancel;
            int version = _analysisVersion, material = _currentMatIndex;
            int selectionVersion = _selectionVersion, w = _w, h = _h;
            byte[] original = _originalBytes; bool[] mask = _maskCache; var triangles = mask == null ? CaptureMaskTriangles() : null; var before = Snapshot();
            var state = RecolorSettings.Read(Settings);
            ReferenceSession reference = useReference ? _references[material] : null;
            bool[] refMask = reference?.Mask();
            int sourceCount = 0, referenceCount = 0;
            Status(L.T("Rc.Working"));
            try
            {
                var next = await Task.Run(() =>
                {
                    if (mask == null) mask = BuildMaskSnapshot(w, h, triangles, cancel.Token);
                    var samples = PaletteAnalysis.Sample(original, mask, cancel.Token);
                    if (samples.Count == 0) throw new InvalidOperationException(L.T("Rc.NoPixels"));
                    var source = state.AutoDetectCount ? PaletteAnalysis.ExtractAutomatic(samples, cancel.Token) :
                        PaletteAnalysis.Extract(samples, state.DetectCount, cancel.Token);
                    sourceCount = source.Count;
                    if (state.AutoDetectCount) state.DetectCount = sourceCount;
                    if (useReference)
                    {
                        var referenceSamples = PaletteAnalysis.Sample(reference.Rgba, refMask, cancel.Token);
                        var colors = state.AutoReferenceCount ? PaletteAnalysis.ExtractAutomatic(referenceSamples, cancel.Token) :
                            PaletteAnalysis.Extract(referenceSamples, state.ReferenceCount == 0 ? state.DetectCount : state.ReferenceCount, cancel.Token);
                        if (colors.Count == 0) throw new InvalidOperationException(L.T("Rc.NoPixels"));
                        referenceCount = colors.Count;
                        if (state.AutoReferenceCount) state.ReferenceCount = referenceCount;
                        state.ReplaceAutomatic(PaletteAnalysis.Match(source, colors, state.ReferenceLock));
                    }
                    else state.ReplaceAutomatic(source.Select(c => PaletteEntry.Identity(c.Rgb)));
                    state.Mode = RecolorMode.Palette;
                    return state;
                }, cancel.Token);
                if (version != _analysisVersion || material != _currentMatIndex || !ReferenceEquals(original, _originalBytes) ||
                    selectionVersion != _selectionVersion || !SameSnapshot(before, Snapshot())) return;
                _maskCache = mask;
                if (useReference) _referenceExpanded = false;
                next.Write(Settings); CommitRecolor(before); RefreshRecolorUi();
                Status(useReference ? L.F("Rc.MatchedCount", sourceCount, referenceCount) : L.F("Rc.DetectedCount", sourceCount));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (version == _analysisVersion) Status(L.F("Rc.Failed", ex.Message)); }
            finally { if (ReferenceEquals(_analysisCancel, cancel)) _analysisCancel = null; cancel.Dispose(); }
        }
        private async void RunGradeAsync()
        {
            InvalidateGrade();
            if (_originalBytes == null) return;
            var cancel = new CancellationTokenSource(); _gradeCancel = cancel;
            int version = _gradeVersion, material = _currentMatIndex, w = _w, h = _h;
            int selectionVersion = _selectionVersion;
            byte[] original = _originalBytes; bool[] mask = _maskCache; var triangles = mask == null ? CaptureMaskTriangles() : null; var settings = Settings.Copy();
            int maxSide = _sessionActive || _colorDialogOpen ? 1024 : 0;
            try
            {
                var result = await Task.Run(() =>
                {
                    if (mask == null) mask = BuildMaskSnapshot(w, h, triangles, cancel.Token);
                    var rendered = GradeRenderer.Render(original, w, h, settings, mask, maxSide, cancel.Token);
                    return new { rendered.Width, rendered.Height, Bgra = RgbaToBgra(rendered.Rgba, rendered.Width, rendered.Height), Hist = ComputeHistogram(rendered.Rgba) };
                }, cancel.Token);
                if (version != _gradeVersion || material != _currentMatIndex || !ReferenceEquals(original, _originalBytes) || selectionVersion != _selectionVersion) return;
                _maskCache = mask;
                if (_wb == null || _wb.PixelWidth != result.Width || _wb.PixelHeight != result.Height)
                    _wb = new WriteableBitmap(result.Width, result.Height, 96, 96, PixelFormats.Bgra32, null);
                _wb.WritePixels(new Int32Rect(0, 0, result.Width, result.Height), result.Bgra, result.Width * 4, 0);
                _histGraded = result.Hist; ApplyPreviewSource(); DrawHistogram();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { if (version == _gradeVersion) Status(L.F("Rc.Failed", ex.Message)); }
            finally { if (ReferenceEquals(_gradeCancel, cancel)) _gradeCancel = null; cancel.Dispose(); }
        }
    }
}
