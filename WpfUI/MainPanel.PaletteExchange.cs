using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;
using TextureGrade.Models;

namespace TextureGrade.WpfUI
{
    public partial class MainPanel
    {
        private sealed class PaletteCell
        {
            public int Index, Rgb;
            public bool Source;
            public Button Button;
            public Border Swatch;
            public TextBlock Hex;
            public Brush Border;
        }
        private sealed class PaletteExchange
        {
            public PaletteCell Origin;
            public int Material, Revision;
        }
        private readonly List<PaletteCell> _paletteCells = new List<PaletteCell>();
        private PaletteExchange _paletteExchange;
        private PaletteCell _palettePress, _paletteHover;
        private Point _palettePressPoint;
        private int _paletteRevision;
        private bool _paletteDragging, _paletteClickExchange;
        private CheckBox _paletteExchangeCheck;
        private DispatcherTimer _paletteScrollTimer;
        private readonly Stopwatch _paletteScrollClock = new Stopwatch();
        private PaletteDragAdorner _paletteGhost;
        private AdornerLayer _paletteAdornerLayer;

        private void InitializePaletteExchange()
        {
            _paletteScrollTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
                { Interval = TimeSpan.FromMilliseconds(33) };
            _paletteScrollTimer.Tick += (s, e) =>
            {
                double elapsed = Math.Min(.1, _paletteScrollClock.Elapsed.TotalSeconds);
                _paletteScrollClock.Restart();
                if (_paletteDragging) UpdatePaletteDrag(Mouse.GetPosition(AdjustScroll), elapsed);
            };
            PaletteSwapCancel.Click += (s, e) => CancelPaletteExchange(true);
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape || (_paletteExchange == null && !_paletteClickExchange)) return;
                CancelPaletteExchange(true); e.Handled = true;
            };
            // Mouse capture is local to this editor. No OLE nested loop, external drop data,
            // texture reads, RBF evaluation or setting changes are needed while dragging.
            AdjustScroll.ScrollChanged += (s, e) =>
            {
                if (_paletteDragging && e.VerticalChange != 0) UpdatePaletteDrag(Mouse.GetPosition(AdjustScroll), 0);
            };
            Settings.PropertyChanged += (s, e) => { _paletteRevision++; CancelPaletteExchange(); };
        }

        private UIElement BuildPaletteExchangeTools()
        {
            var tools = new StackPanel();
            tools.Children.Add(RcText("Rc.SwapHint"));
            _paletteExchangeCheck = new CheckBox { Content = L.T("Rc.SwapClickMode"), IsChecked = _paletteClickExchange,
                ToolTip = L.T("Rc.SwapClickHint"), Margin = new Thickness(3, 4, 3, 4) };
            _paletteExchangeCheck.Checked += (s, e) =>
            {
                _paletteClickExchange = true;
                SetPaletteExchangeStatus(L.T("Rc.SwapChooseFirst"));
            };
            _paletteExchangeCheck.Unchecked += (s, e) => { _paletteClickExchange = false; CancelPaletteExchange(); };
            tools.Children.Add(_paletteExchangeCheck);
            PaletteSwapCancel.Content = L.T("Rc.Cancel");
            if (_paletteClickExchange) SetPaletteExchangeStatus(L.T("Rc.SwapChooseFirst"));
            return tools;
        }

        private Button PaletteColorButton(int index, bool source, int rgb)
        {
            var cell = new PaletteCell { Index = index, Source = source, Rgb = rgb };
            var button = ColorButton(rgb, source ? "Rc.Source" : "Rc.Target", () => ActivatePaletteCell(cell));
            cell.Button = button;
            var content = (StackPanel)button.Content;
            cell.Swatch = (Border)content.Children[0]; cell.Hex = (TextBlock)content.Children[1];
            cell.Border = button.BorderBrush;
            button.Tag = source ? "PaletteSource" : "PaletteTarget";
            button.ToolTip = L.F("Rc.SwapCellHint", index + 1, L.T(source ? "Rc.SourceColumn" : "Rc.TargetColumn"));
            System.Windows.Automation.AutomationProperties.SetName(button, (string)button.ToolTip);
            _paletteCells.Add(cell);
            // Run after ButtonBase focuses the button (which can commit a count text box).
            button.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler((s, e) =>
            {
                _palettePress = cell; _palettePressPoint = e.GetPosition(AdjustScroll);
            }), true);
            button.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                Point p = e.GetPosition(AdjustScroll);
                if (!_paletteDragging && _palettePress == cell &&
                    (Math.Abs(p.X - _palettePressPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                     Math.Abs(p.Y - _palettePressPoint.Y) >= SystemParameters.MinimumVerticalDragDistance))
                {
                    if (!StartPaletteDrag(cell.Index, cell.Source)) return;
                }
                if (_paletteDragging) { UpdatePaletteDrag(p, 0); e.Handled = true; }
            };
            button.PreviewMouseLeftButtonUp += (s, e) =>
            {
                _palettePress = null;
                if (!_paletteDragging) return; // Keep an ordinary click's color picker behavior.
                e.Handled = true; // Never open a picker on the mouse-up that finishes a drag.
                FinishPaletteDrag(e.GetPosition(AdjustScroll));
            };
            button.LostMouseCapture += (s, e) =>
            {
                if (_paletteDragging) CancelPaletteExchange();
                if (_palettePress == cell) _palettePress = null;
            };
            var menu = new ContextMenu();
            var exchange = new MenuItem { Header = L.T("Rc.SwapStart") };
            exchange.Click += (s, e) =>
            {
                _paletteClickExchange = true; _paletteExchangeCheck.IsChecked = true;
                BeginPaletteExchange(index, source);
            };
            var edit = new MenuItem { Header = L.T(source ? "Rc.Source" : "Rc.Target") };
            edit.Click += (s, e) => EditPaletteColor(index, source);
            menu.Items.Add(exchange); menu.Items.Add(edit); button.ContextMenu = menu;
            return button;
        }

        private void ActivatePaletteCell(PaletteCell cell)
        {
            if (_paletteClickExchange || _paletteExchange != null)
            {
                if (_paletteExchange == null) BeginPaletteExchange(cell.Index, cell.Source);
                else CompletePaletteExchange(cell.Index, cell.Source);
            }
            else EditPaletteColor(cell.Index, cell.Source);
        }

        private PaletteCell FindPaletteCell(int index, bool source)
            => _paletteCells.Find(c => c.Index == index && c.Source == source);

        private bool StartPaletteDrag(int index, bool source)
        {
            if (!BeginPaletteExchange(index, source)) return false;
            var cell = _paletteExchange.Origin;
            if (!cell.Button.CaptureMouse()) { CancelPaletteExchange(); return false; }
            ShowPaletteDragFeedback();
            return true;
        }

        private void ShowPaletteDragFeedback()
        {
            _paletteDragging = true;
            _paletteGhost = new PaletteDragAdorner(AdjustScroll, _paletteExchange.Origin.Rgb);
            _paletteAdornerLayer = AdornerLayer.GetAdornerLayer(AdjustScroll);
            _paletteAdornerLayer?.Add(_paletteGhost);
            _paletteScrollClock.Restart(); _paletteScrollTimer.Start();
        }

        private void FinishPaletteDrag(Point point)
        {
            var target = PaletteCellAt(point);
            if (target == null || !CompletePaletteExchange(target.Index, target.Source)) CancelPaletteExchange();
        }

        private bool BeginPaletteExchange(int index, bool source)
        {
            if (_buildingRecolor || _colorDialogOpen || _currentMatIndex < 0) return false;
            var cell = FindPaletteCell(index, source);
            if (cell == null) return false;
            CancelPaletteExchange(); StopPalettePick();
            _paletteExchange = new PaletteExchange { Origin = cell, Material = _currentMatIndex, Revision = _paletteRevision };
            HighlightPaletteCell(cell, Brushes.DarkOrange);
            SetPaletteExchangeStatus(L.F("Rc.SwapPending", index + 1, L.T(source ? "Rc.SourceColumn" : "Rc.TargetColumn")));
            return true;
        }

        private bool CompletePaletteExchange(int index, bool source)
        {
            var exchange = _paletteExchange;
            if (exchange == null) return false;
            if (exchange.Material != _currentMatIndex || exchange.Revision != _paletteRevision)
            { CancelPaletteExchange(); return false; }
            if (source != exchange.Origin.Source)
            { SetPaletteExchangeStatus(L.T("Rc.SwapSameColumn")); return false; }
            int first = exchange.Origin.Index;
            CancelPaletteExchange();
            var state = RecolorSettings.Read(Settings);
            if (!state.SwapColors(first, index, source)) return false;
            var before = Snapshot();
            CancelAnalysis(); // An in-flight detection must not replace the user's new pairing.
            state.Write(Settings); CommitRecolor(before);
            // Only the two affected rows' swatches change. Keep the entire visual tree,
            // keyboard focus and scroll position; the normal debounce queues one render.
            UpdatePaletteRowColors(first, state.Entries[first]);
            UpdatePaletteRowColors(index, state.Entries[index]);
            Status(L.F("Rc.SwapDone", first + 1, index + 1));
            return true;
        }

        private void UpdatePaletteRowColors(int index, PaletteEntry entry)
        {
            foreach (bool source in new[] { true, false })
            {
                var cell = FindPaletteCell(index, source);
                if (cell == null) continue;
                cell.Rgb = source ? entry.SourceRgb : OklabColor.ToRgb(entry.EffectiveTarget);
                cell.Swatch.Background = new SolidColorBrush(OklchWheel.ToColor(cell.Rgb));
                cell.Hex.Text = "#" + cell.Rgb.ToString("X6");
            }
        }

        private void SetPaletteExchangeStatus(string text)
        {
            TxtAdjustTitle.Visibility = Visibility.Collapsed;
            PaletteSwapStatus.Text = text; PaletteSwapStatus.ToolTip = text;
            PaletteSwapStatus.Visibility = PaletteSwapCancel.Visibility = Visibility.Visible;
        }

        private void CancelPaletteExchange(bool exitClickMode = false)
        {
            bool wasDragging = _paletteDragging; var origin = _paletteExchange?.Origin;
            _paletteDragging = false; _paletteExchange = null; _palettePress = null;
            _paletteScrollTimer?.Stop(); _paletteScrollClock.Stop();
            if (_paletteGhost != null) _paletteAdornerLayer?.Remove(_paletteGhost);
            _paletteGhost = null; _paletteAdornerLayer = null;
            RestorePaletteCell(origin); RestorePaletteCell(_paletteHover); _paletteHover = null;
            if (wasDragging && origin != null && Mouse.Captured == origin.Button) origin.Button.ReleaseMouseCapture();
            if (PaletteSwapStatus != null)
            {
                if (_paletteClickExchange && !exitClickMode) SetPaletteExchangeStatus(L.T("Rc.SwapChooseFirst"));
                else
                {
                    PaletteSwapStatus.Visibility = PaletteSwapCancel.Visibility = Visibility.Collapsed;
                    TxtAdjustTitle.Visibility = Visibility.Visible;
                }
            }
            if (exitClickMode)
            {
                _paletteClickExchange = false;
                if (_paletteExchangeCheck != null) _paletteExchangeCheck.IsChecked = false;
            }
        }

        private static void HighlightPaletteCell(PaletteCell cell, Brush brush)
        {
            if (cell == null) return;
            cell.Button.BorderBrush = brush; // Keep border width stable to avoid layout on hover.
        }
        private static void RestorePaletteCell(PaletteCell cell)
        {
            if (cell == null) return;
            cell.Button.BorderBrush = cell.Border;
        }
        private PaletteCell PaletteCellAt(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= AdjustScroll.ViewportWidth || p.Y >= AdjustScroll.ViewportHeight) return null;
            var node = AdjustScroll.InputHitTest(p) as DependencyObject;
            while (node != null && node != AdjustScroll)
            {
                if (node is Button button) return _paletteCells.Find(c => ReferenceEquals(c.Button, button));
                node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
            }
            return null;
        }

        // Units are WPF device-independent pixels per second. Outside the viewport and
        // in its middle: no movement. A short viewport still has a neutral middle zone.
        private static double PaletteScrollSpeed(Point point, double width, double height)
        {
            if (point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height || height <= 0) return 0;
            double edge = Math.Min(56, height / 4);
            double fraction = point.Y < edge ? -(edge - point.Y) / edge :
                point.Y > height - edge ? (point.Y - height + edge) / edge : 0;
            return 1100 * fraction * Math.Abs(fraction);
        }
        private void UpdatePaletteDrag(Point point, double elapsed)
        {
            if (!_paletteDragging || _paletteExchange == null) return;
            double speed = PaletteScrollSpeed(point, AdjustScroll.ViewportWidth, AdjustScroll.ViewportHeight);
            if (elapsed > 0 && speed != 0)
            {
                // Stop at the last palette row rather than racing into unrelated effect groups.
                double end = _paletteRows.TranslatePoint(new Point(0, _paletteRows.ActualHeight), AdjustScroll).Y +
                    AdjustScroll.VerticalOffset - AdjustScroll.ViewportHeight + 6;
                double limit = Math.Max(AdjustScroll.VerticalOffset, end);
                AdjustScroll.ScrollToVerticalOffset(Math.Max(0, Math.Min(limit, AdjustScroll.VerticalOffset + speed * elapsed)));
            }
            var target = PaletteCellAt(point);
            if (target == _paletteExchange.Origin || target?.Source != _paletteExchange.Origin.Source) target = null;
            if (target != _paletteHover)
            {
                RestorePaletteCell(_paletteHover); _paletteHover = target;
                HighlightPaletteCell(target, Brushes.DodgerBlue);
            }
            string message = target == null ? L.F("Rc.SwapDragging", _paletteExchange.Origin.Index + 1,
                L.T(_paletteExchange.Origin.Source ? "Rc.SourceColumn" : "Rc.TargetColumn")) :
                L.F("Rc.SwapDrop", _paletteExchange.Origin.Index + 1, target.Index + 1);
            _paletteGhost?.Move(point, message);
        }

        private sealed class PaletteDragAdorner : Adorner
        {
            private readonly Brush _color;
            private Point _point;
            private string _message;
            private FormattedText _text;
            public PaletteDragAdorner(UIElement element, int rgb) : base(element)
            {
                IsHitTestVisible = false; _color = new SolidColorBrush(OklchWheel.ToColor(rgb));
                ClipToBounds = true;
            }
            public void Move(Point point, string message)
            {
                if (_point == point && _message == message) return;
                _point = point;
                if (_message != message)
                {
                    _message = message;
                    _text = new FormattedText(message, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 12, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    _text.MaxTextWidth = Math.Max(40, Math.Min(320, AdornedElement.RenderSize.Width - 60));
                }
                InvalidateVisual();
            }
            protected override void OnRender(DrawingContext dc)
            {
                if (_text == null) return;
                double w = _text.Width + 42, h = _text.Height + 16;
                double x = Math.Max(0, Math.Min(_point.X + 16, AdornedElement.RenderSize.Width - w));
                double y = _point.Y + 20;
                if (y + h > AdornedElement.RenderSize.Height) y = Math.Max(0, _point.Y - h - 10);
                dc.DrawRoundedRectangle(Brushes.DimGray, null, new Rect(x, y, w, h), 5, 5);
                dc.DrawRectangle(_color, new Pen(Brushes.White, 1), new Rect(x + 8, y + 8, 20, 16));
                dc.DrawText(_text, new Point(x + 34, y + 8));
            }
        }
    }
}
