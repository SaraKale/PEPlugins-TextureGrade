using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;

namespace TextureGrade.WpfUI
{
    public sealed class OklchWheel : FrameworkElement
    {
        public Oklab Color;
        public event Action<Oklab> Changed;
        private bool dragging;
        protected override void OnRender(DrawingContext dc)
        {
            double radius = Math.Min(ActualWidth, ActualHeight) / 2 - 8;
            if (radius < 10) return;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            for (int h = 0; h < 360; h += 2)
            {
                var c = OklabColor.FromLch(Color.L, OklabColor.MaxChroma(Color.L, h), h);
                var pen = new Pen(Brush(c), 15);
                dc.DrawLine(pen, Polar(center, radius, h), Polar(center, radius, h + 2.5));
            }
            double inner = radius - 18;
            var gradient = new RadialGradientBrush();
            gradient.GradientStops.Add(new GradientStop(ToColor(OklabColor.ToRgb(new Oklab(Color.L, 0, 0))), 0));
            gradient.GradientStops.Add(new GradientStop(ToColor(OklabColor.ToRgb(OklabColor.FromLch(Color.L,
                OklabColor.MaxChroma(Color.L, Color.Hue), Color.Hue))), 1));
            dc.DrawEllipse(gradient, null, center, inner, inner);
            double max = OklabColor.MaxChroma(Color.L, Color.Hue);
            var point = Polar(center, inner * (max > 0 ? Color.Chroma / max : 0), Color.Hue);
            dc.DrawEllipse(null, new Pen(Brushes.White, 3), point, 5, 5);
            dc.DrawEllipse(null, new Pen(Brushes.Black, 1), point, 6, 6);
            dc.DrawEllipse(null, new Pen(Brushes.Black, 2), Polar(center, radius, Color.Hue), 6, 6);
        }
        private static Point Polar(Point center, double radius, double h)
            => new Point(center.X + radius * Math.Cos(h * Math.PI / 180), center.Y + radius * Math.Sin(h * Math.PI / 180));
        public static Color ToColor(int rgb) => System.Windows.Media.Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        public static SolidColorBrush Brush(Oklab c) => new SolidColorBrush(ToColor(OklabColor.ToRgb(c)));
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { dragging = true; CaptureMouse(); Pick(e.GetPosition(this)); e.Handled = true; }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) Pick(e.GetPosition(this)); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { dragging = false; ReleaseMouseCapture(); }
        private void Pick(Point p)
        {
            double x = p.X - ActualWidth / 2, y = p.Y - ActualHeight / 2;
            double radius = Math.Min(ActualWidth, ActualHeight) / 2 - 8, distance = Math.Sqrt(x * x + y * y);
            double hue = distance >= radius - 14 ? (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360 : Color.Hue;
            double max = OklabColor.MaxChroma(Color.L, hue);
            double chroma = distance >= radius - 14 ? (Color.Chroma < 1e-5 ? max * .75 : Math.Min(Color.Chroma, max)) : OklabColor.Clamp(distance / (radius - 18), 0, 1) * max;
            Color = OklabColor.FromLch(Color.L, chroma, hue);
            InvalidateVisual(); Changed?.Invoke(Color);
        }
    }

    public sealed class HsvSquare : FrameworkElement
    {
        public HsvColor Color;
        public event Action<HsvColor> Changed;
        private bool dragging;
        protected override void OnRender(DrawingContext dc)
        {
            var bounds = new Rect(4, 4, Math.Max(0, ActualWidth - 8), Math.Max(0, ActualHeight - 8));
            dc.DrawRectangle(new LinearGradientBrush(Colors.White, OklchWheel.ToColor(new HsvColor(Color.H, 1, 1).ToRgb()), 0), null, bounds);
            dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent, Colors.Black, 90), new Pen(Brushes.Gray, 1), bounds);
            var point = new Point(bounds.Left + Color.S * bounds.Width, bounds.Top + (1 - Color.V) * bounds.Height);
            dc.DrawEllipse(null, new Pen(Brushes.White, 3), point, 5, 5);
            dc.DrawEllipse(null, new Pen(Brushes.Black, 1), point, 6, 6);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { dragging = true; CaptureMouse(); Pick(e.GetPosition(this)); e.Handled = true; }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) Pick(e.GetPosition(this)); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { dragging = false; ReleaseMouseCapture(); }
        protected override void OnLostMouseCapture(MouseEventArgs e) { dragging = false; base.OnLostMouseCapture(e); }
        private void Pick(Point p)
        {
            Color.S = OklabColor.Clamp((p.X - 4) / Math.Max(1, ActualWidth - 8), 0, 1);
            Color.V = 1 - OklabColor.Clamp((p.Y - 4) / Math.Max(1, ActualHeight - 8), 0, 1);
            InvalidateVisual(); Changed?.Invoke(Color);
        }
    }

    public enum ColorPickerMode { Hsv, Rgb, Oklch }

    public sealed class ColorPickerWindow : Window
    {
        private readonly OklchWheel wheel = new OklchWheel { Width = 230, Height = 230, Margin = new Thickness(8) };
        private readonly HsvSquare hsvSquare = new HsvSquare { Height = 190, Margin = new Thickness(0, 12, 0, 4) };
        private readonly RadioButton[] modeButtons = new RadioButton[3];
        private readonly TextBlock[] labels = new TextBlock[3];
        private readonly Slider[] sliders = new Slider[3];
        private readonly TextBox[] numbers = new TextBox[3];
        private readonly TextBox hex = new TextBox(), rgb = new TextBox();
        private readonly Border swatch = new Border { Height = 32, Margin = new Thickness(0, 8, 0, 8) };
        private readonly TextBlock error = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
        private readonly CheckBox lockBox;
        private readonly double? sourceL;
        private bool syncing;
        private HsvColor hsv;
        public ColorPickerMode Mode { get; private set; } = ColorPickerMode.Hsv;
        public Oklab Selected { get; private set; }
        public bool Locked { get; private set; }
        public event Action<Oklab, bool> ColorChanged;

        public ColorPickerWindow(Oklab initial, double? sourceLightness, bool locked)
        {
            Title = L.T("Rc.Picker"); Width = 390; SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White; sourceL = sourceLightness; Locked = sourceL.HasValue && locked;
            var panel = new StackPanel { Margin = new Thickness(18) }; Content = panel;
            var modes = new StackPanel { Orientation = Orientation.Horizontal };
            modes.Children.Add(new TextBlock { Text = L.T("Rc.PickerMode"), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center });
            foreach (ColorPickerMode mode in Enum.GetValues(typeof(ColorPickerMode)))
            {
                var choice = new RadioButton { Content = mode.ToString().ToUpperInvariant(), Margin = new Thickness(0, 0, 14, 0), VerticalContentAlignment = VerticalAlignment.Center };
                choice.Checked += (s, e) => { if (!syncing) SelectMode(mode); };
                modeButtons[(int)mode] = choice; modes.Children.Add(choice);
            }
            panel.Children.Add(modes); panel.Children.Add(hsvSquare); panel.Children.Add(wheel); panel.Children.Add(swatch);
            lockBox = new CheckBox { Content = L.T("Rc.Lock"), IsChecked = Locked, Visibility = sourceL.HasValue ? Visibility.Visible : Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };
            panel.Children.Add(lockBox);
            lockBox.Checked += (s, e) => { if (!syncing) { Locked = true; Set(Selected, true); } };
            lockBox.Unchecked += (s, e) => { if (!syncing) { Locked = false; Set(Selected, true); } };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
                var label = new TextBlock { Width = 38, VerticalAlignment = VerticalAlignment.Center };
                var number = new TextBox { Width = 66, VerticalContentAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(label, Dock.Left); DockPanel.SetDock(number, Dock.Right);
                var slider = new Slider { Minimum = 0, MinHeight = 26, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
                row.Children.Add(label); row.Children.Add(number); row.Children.Add(slider); panel.Children.Add(row);
                labels[i] = label; sliders[i] = slider; numbers[i] = number;
                slider.ValueChanged += (s, e) => { if (!syncing) ChangeComponent(index, slider.Value); };
                Action commit = () => { if (syncing || number.Text == number.Tag as string) return; if (double.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && !double.IsNaN(v) && !double.IsInfinity(v)) ChangeComponent(index, OklabColor.Clamp(v, 0, slider.Maximum)); else error.Text = L.T("Rc.InvalidColor"); };
                number.LostKeyboardFocus += (s, e) => commit();
                number.KeyDown += (s, e) => { if (e.Key == Key.Enter) { commit(); e.Handled = true; } };
            }
            AddInput(panel, "HEX", hex, () =>
            {
                string text = hex.Text.Trim().TrimStart('#');
                if (text.Length != 6 || !int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value)) { error.Text = L.T("Rc.InvalidColor"); return; }
                Set(OklabColor.FromRgb(value), true);
            });
            AddInput(panel, "RGB", rgb, () =>
            {
                var parts = rgb.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3 || parts.Any(p => !byte.TryParse(p, out _))) { error.Text = L.T("Rc.InvalidColor"); return; }
                Set(OklabColor.FromRgb(byte.Parse(parts[0]), byte.Parse(parts[1]), byte.Parse(parts[2])), true);
            });
            panel.Children.Add(new TextBlock { Text = L.T("Rc.GamutNote"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4), Foreground = Brushes.DimGray });
            panel.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var ok = new Button { Content = L.T("Rc.OK"), MinWidth = 80, Padding = new Thickness(8, 5, 8, 5), IsDefault = true };
            var cancel = new Button { Content = L.T("Rc.Cancel"), MinWidth = 80, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            ok.Click += (s, e) => { if (string.IsNullOrEmpty(error.Text)) DialogResult = true; };
            buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons);
            wheel.Changed += c => Set(c, true);
            hsvSquare.Changed += c => { hsv = c; Set(OklabColor.FromRgb(c.ToRgb()), true); };
            Set(initial, false);
        }

        public void SelectMode(ColorPickerMode mode)
        {
            if (!Enum.IsDefined(typeof(ColorPickerMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            Mode = mode;
            // A coordinate-system switch is presentation only: keep the exact floating-point color.
            RefreshControls();
        }

        private void AddInput(Panel panel, string label, TextBox input, Action commit)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            row.Children.Add(new TextBlock { Text = label, Width = 42, VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(input); panel.Children.Add(row);
            input.LostKeyboardFocus += (s, e) => { if (!syncing && input.Text != input.Tag as string) commit(); };
            input.KeyDown += (s, e) => { if (e.Key == Key.Enter) { if (!syncing && input.Text != input.Tag as string) commit(); e.Handled = true; } };
        }
        private void ChangeComponent(int index, double value)
        {
            if (Mode == ColorPickerMode.Hsv)
            {
                if (index == 0) hsv.H = value;
                else if (index == 1) hsv.S = value / 100;
                else hsv.V = value / 100;
                Set(OklabColor.FromRgb(hsv.ToRgb()), true);
            }
            else if (Mode == ColorPickerMode.Rgb)
            {
                int shift = (2 - index) * 8, color = OklabColor.ToRgb(Selected);
                color = (color & ~(255 << shift)) | ((int)Math.Round(value) << shift);
                Set(OklabColor.FromRgb(color), true);
            }
            else Set(OklabColor.FromLch(index == 0 ? value : Selected.L, index == 1 ? value : Selected.Chroma, index == 2 ? value : Selected.Hue), true);
        }
        private void Set(Oklab c, bool notify)
        {
            if (Locked && sourceL.HasValue) c.L = sourceL.Value;
            Selected = OklabColor.FitGamut(c);
            RefreshControls();
            if (notify) ColorChanged?.Invoke(Selected, Locked);
        }
        private void RefreshControls()
        {
            syncing = true;
            try
            {
                int color = OklabColor.ToRgb(Selected);
                hsv = HsvColor.FromRgb(color, hsv.H, hsv.S);
                string[] names; double[] values, max;
                if (Mode == ColorPickerMode.Hsv)
                {
                    names = new[] { "H°", "S%", "V%" }; values = new[] { hsv.H, hsv.S * 100, hsv.V * 100 }; max = new[] { 360.0, 100, 100 };
                }
                else if (Mode == ColorPickerMode.Rgb)
                {
                    names = new[] { "R", "G", "B" }; values = new[] { (double)((color >> 16) & 255), (color >> 8) & 255, color & 255 }; max = new[] { 255.0, 255, 255 };
                }
                else
                {
                    names = new[] { "L", "C", "h°" }; values = new[] { Selected.L, Selected.Chroma, Selected.Hue }; max = new[] { 1.0, .5, 360 };
                }
                for (int i = 0; i < 3; i++)
                {
                    labels[i].Text = names[i];
                    sliders[i].Maximum = max[i];
                    sliders[i].IsSnapToTickEnabled = Mode == ColorPickerMode.Rgb;
                    sliders[i].TickFrequency = Mode == ColorPickerMode.Oklch && i < 2 ? .001 : 1;
                    sliders[i].SmallChange = sliders[i].TickFrequency;
                    sliders[i].LargeChange = sliders[i].SmallChange * 10;
                    sliders[i].Value = values[i];
                    string format = Mode == ColorPickerMode.Rgb ? "F0" : Mode == ColorPickerMode.Oklch && i < 2 ? "F4" : "F1";
                    WriteInput(numbers[i], values[i].ToString(format, CultureInfo.InvariantCulture));
                    sliders[i].IsEnabled = numbers[i].IsEnabled = !(Locked && Mode == ColorPickerMode.Oklch && i == 0);
                    modeButtons[i].IsChecked = i == (int)Mode;
                }
                hsvSquare.Visibility = Mode == ColorPickerMode.Hsv ? Visibility.Visible : Visibility.Collapsed;
                hsvSquare.Color = hsv; hsvSquare.InvalidateVisual();
                wheel.Visibility = Mode == ColorPickerMode.Oklch ? Visibility.Visible : Visibility.Collapsed;
                wheel.Color = Selected; wheel.InvalidateVisual(); swatch.Background = OklchWheel.Brush(Selected);
                WriteInput(hex, "#" + color.ToString("X6")); WriteInput(rgb, $"{(color >> 16) & 255}, {(color >> 8) & 255}, {color & 255}");
                error.Text = "";
            }
            finally { syncing = false; }
        }
        private static void WriteInput(TextBox input, string value) { input.Text = value; input.Tag = value; }
    }
}
