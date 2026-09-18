using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureGrade.ColorGrade;

namespace TextureGrade.WpfUI
{
    /// <summary>
    /// Lab 取色环。
    ///
    /// 设计（对应需求里的「色环第三维 H 保留、把 SV 二维加一个约束压成一维」）：
    ///   · 外环 = 色相轴（角度就是 Lab 色相角）。环上每个角度的颜色都是
    ///     「在当前 L* 下、这个色相能达到的色域内最高彩度」——
    ///     所以整个环是一条**等亮度的色相环**：在环上随便转到哪，L* 都一模一样。
    ///   · 内圈 = 彩度轴（灰色中心 -> 该色相的最高彩度）。锁定亮度后，
    ///     SV 平面上的自由度只剩这一条径向的线，这就是需求说的「一维的线」。
    ///   · L* 由外部给定（DrawL）。锁定亮度时它取「画面自身的平均亮度」，
    ///     于是环上的颜色看起来就跟画面同明暗；不锁定时可由用户自定。
    ///
    /// 用户一交互就触发 Changed，由宿主把颜色写回调色设置。
    /// </summary>
    public sealed class LabWheel : FrameworkElement
    {
        /// <summary>环带内边界（相对半径）。</summary>
        private const double InnerRatio = 0.63;
        private const int RingBitmapSize = 256;

        private WriteableBitmap _ring;
        private double _ringL = double.NaN;

        private double _drawL = 55;
        private bool _lockLightness = true;

        private double _hue;        // 0..360
        private double _chroma;     // 0..Cmax(_drawL, _hue)

        private bool _dragging;

        /// <summary>选色变化（拖动环或内圈时连续触发）。</summary>
        public event Action Changed;

        /// <summary>绘制环所用的亮度锚点 L*（0..100）。</summary>
        public double DrawL
        {
            get => _drawL;
            set
            {
                double v = Clamp(value, 0, 100);
                if (Math.Abs(v - _drawL) < 0.01) return;
                _drawL = v;
                _chroma = Math.Min(_chroma, Cmax);
                InvalidateVisual();
            }
        }

        public bool LockLightness
        {
            get => _lockLightness;
            set { _lockLightness = value; InvalidateVisual(); }
        }

        public double HueDeg => _hue;
        public double Chroma => _chroma;
        public double Cmax => LabColor.MaxChroma(_drawL, _hue);
        public double ChromaRatio => Cmax > 0 ? Clamp(_chroma / Cmax, 0, 1) : 0;

        /// <summary>当前选中的颜色（用于显示 hex）。</summary>
        public void GetRgb(out byte r, out byte g, out byte b)
            => LabColor.ColorFromLch(_drawL, _chroma, _hue, out r, out g, out b);

        /// <summary>外部（撤销/预设/换材质）同步：直接给出目标 a*/b* 与绘制亮度。</summary>
        public void SetState(double drawL, bool lockLightness, double a, double b)
        {
            _lockLightness = lockLightness;

            double v = Clamp(drawL, 0, 100);
            bool lChanged = Math.Abs(v - _drawL) >= 0.01;
            _drawL = v;

            _hue = LabColor.HueDeg(a, b);
            double c = Math.Sqrt(a * a + b * b);
            double cmax = LabColor.MaxChroma(_drawL, _hue);
            _chroma = Clamp(c, 0, cmax > 0 ? cmax : 0);

            if (lChanged) InvalidateVisual();
        }

        // ---------------- 布局 ----------------

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0 ? 240 : availableSize.Width;
            double side = Clamp(Math.Min(w, 250), 140, 250);
            return new Size(side, side);
        }

        // ---------------- 渲染 ----------------

        protected override void OnRender(DrawingContext dc)
        {
            double side = Math.Min(ActualWidth, ActualHeight);
            if (side < 24) return;

            double cx = ActualWidth / 2, cy = ActualHeight / 2;
            double outerR = side / 2 - 2;
            double innerR = outerR * InnerRatio;

            EnsureRing();
            if (_ring != null)
                dc.DrawImage(_ring, new Rect(cx - outerR, cy - outerR, outerR * 2, outerR * 2));

            // 内圈：当前色相的彩度渐变（中心 = 该亮度的中性灰，边缘 = 该色相的最高彩度）
            var center = new Point(cx, cy);
            dc.DrawEllipse(MakeDiskBrush(), null, center, innerR, innerR);

            // 当前色相的指示方块（画在环带中央）
            double midR = outerR * (InnerRatio + 1) / 2;
            var huePt = PolarToPoint(cx, cy, midR, _hue);
            dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)), 1.6), huePt, 5.2, 5.2);

            // 当前彩度的指示点（画在内圈上）
            var chrPt = PolarToPoint(cx, cy, innerR * ChromaRatio, _hue);
            dc.DrawEllipse(Brushes.White, new Pen(new SolidColorBrush(Color.FromArgb(200, 20, 20, 20)), 1.6), chrPt, 4.4, 4.4);
        }

        private static Point PolarToPoint(double cx, double cy, double radius, double hueDeg)
        {
            double rad = hueDeg * Math.PI / 180.0;
            return new Point(cx + radius * Math.Cos(rad), cy - radius * Math.Sin(rad));
        }

        private Brush MakeDiskBrush()
        {
            byte nr, ng, nb, er, eg, eb;
            LabColor.ColorFromLch(_drawL, 0, _hue, out nr, out ng, out nb);
            LabColor.ColorFromLch(_drawL, Cmax, _hue, out er, out eg, out eb);
            var b = new RadialGradientBrush(
                Color.FromRgb(nr, ng, nb),
                Color.FromRgb(er, eg, eb));
            b.Freeze();
            return b;
        }

        /// <summary>生成等亮度色相环位图（只在 L* 变化时重建；色相查表避免逐像素二分）。</summary>
        private void EnsureRing()
        {
            if (_ring != null && Math.Abs(_ringL - _drawL) < 0.01) return;
            _ringL = _drawL;

            // 360 个色相 -> 颜色（每度一档，视觉上足够平滑）
            var lut = new byte[360, 3];
            for (int h = 0; h < 360; h++)
            {
                byte r, g, b;
                LabColor.ColorFromLch(_drawL, LabColor.MaxChroma(_drawL, h), h, out r, out g, out b);
                lut[h, 0] = r; lut[h, 1] = g; lut[h, 2] = b;
            }

            const int N = RingBitmapSize;
            var px = new int[N * N];
            double half = N / 2.0;
            double outer = half - 0.5;
            double inner = outer * InnerRatio;

            // 2x2 超采样，环带内外边缘更平滑
            double[] off = { 0.25, 0.75 };
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    int sr = 0, sg = 0, sb = 0, sa = 0;
                    foreach (double oy in off)
                    {
                        foreach (double ox in off)
                        {
                            double dx = x + ox - half;
                            double dy = y + oy - half;
                            double r = Math.Sqrt(dx * dx + dy * dy);
                            if (r > outer || r < inner) continue;

                            double ang = Math.Atan2(-dy, dx) * 180.0 / Math.PI;
                            if (ang < 0) ang += 360;
                            int hi = (int)ang % 360;

                            sr += lut[hi, 0]; sg += lut[hi, 1]; sb += lut[hi, 2]; sa += 255;
                        }
                    }

                    px[y * N + x] = sa == 0
                        ? 0
                        : Pack((byte)(sa / 4), (byte)(sr / 4), (byte)(sg / 4), (byte)(sb / 4));
                }
            }

            var bmp = new WriteableBitmap(N, N, 96, 96, PixelFormats.Bgra32, null);
            bmp.WritePixels(new Int32Rect(0, 0, N, N), px, N * 4, 0);
            bmp.Freeze();
            _ring = bmp;
        }

        private static int Pack(byte a, byte r, byte g, byte b)
            => (a << 24) | (r << 16) | (g << 8) | b;

        // ---------------- 交互 ----------------

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            _dragging = true;
            CaptureMouse();
            HandlePoint(e.GetPosition(this));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) HandlePoint(e.GetPosition(this));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!_dragging) return;
            _dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        /// <summary>环上 -> 改色相；内圈 -> 改彩度（亮度始终不动）。</summary>
        private void HandlePoint(Point p)
        {
            double side = Math.Min(ActualWidth, ActualHeight);
            if (side < 24) return;

            double cx = ActualWidth / 2, cy = ActualHeight / 2;
            double outerR = side / 2 - 2;
            double innerR = outerR * InnerRatio;

            double dx = p.X - cx, dy = p.Y - cy;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r > outerR * 1.06) return;   // 环外太远，忽略

            if (r >= innerR)
            {
                double ang = Math.Atan2(-dy, dx) * 180.0 / Math.PI;
                if (ang < 0) ang += 360;
                _hue = ang;
                // 色相变了，彩度上限随之变化：按比例保号，避免选到越界色
                double cmax = Cmax;
                if (_chroma > cmax) _chroma = cmax;
            }
            else
            {
                double ratio = innerR <= 0 ? 0 : Clamp(r / innerR, 0, 1);
                _chroma = ratio * Cmax;
            }

            InvalidateVisual();
            Changed?.Invoke();
        }

        private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
