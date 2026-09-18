using System;
using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>饱和度：提满或去色。0 = 不变。</summary>
    public class Saturation : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double sat = s["Saturation"] / 100.0;
            if (sat == 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                ColorMath.RgbToHsl(c[i], c[i + 1], c[i + 2], out var hh, out var ss, out var ll);
                double s2 = sat >= 0 ? ss + (1 - ss) * sat : ss * (1 + sat);
                ColorMath.HslToRgb(hh, Clamp01(s2), ll, out var r, out var g, out var b);
                c[i] = r; c[i + 1] = g; c[i + 2] = b;
            }
        }
        private static double Clamp01(double v) => ColorMath.Clamp01(v);
    }

    /// <summary>自然饱和度：对低饱和颜色加强更多，避免肤色过饱和。</summary>
    public class Vibrance : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double vib = s["Vibrance"] / 100.0;
            if (vib == 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                ColorMath.RgbToHsl(c[i], c[i + 1], c[i + 2], out var hh, out var ss, out var ll);
                double boost = vib * (1 - ss); // 越不饱和加强越多
                double s2 = ss + boost;
                ColorMath.HslToRgb(hh, Clamp01(s2), ll, out var r, out var g, out var b);
                c[i] = r; c[i + 1] = g; c[i + 2] = b;
            }
        }
        private static double Clamp01(double v) => ColorMath.Clamp01(v);
    }

    /// <summary>色相旋转：度。</summary>
    public class Hue : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double deg = s["Hue"];
            if (deg == 0) return;
            double shift = deg / 360.0;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                ColorMath.RgbToHsl(c[i], c[i + 1], c[i + 2], out var hh, out var ss, out var ll);
                double h2 = hh + shift;
                h2 -= Math.Floor(h2);
                ColorMath.HslToRgb(h2, ss, ll, out var r, out var g, out var b);
                c[i] = r; c[i + 1] = g; c[i + 2] = b;
            }
        }
    }

    /// <summary>色彩平衡：全局 R/G/B 偏移（lift）。</summary>
    public class ColorBalance : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double r = s["ColorBalanceR"] / 100.0 * 80;
            double g = s["ColorBalanceG"] / 100.0 * 80;
            double b = s["ColorBalanceB"] / 100.0 * 80;
            if (r == 0 && g == 0 && b == 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl(c[i] + r);
                c[i + 1] = Cl(c[i + 1] + g);
                c[i + 2] = Cl(c[i + 2] + b);
            }
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v);
    }

    /// <summary>HSV 明度：缩放亮度。</summary>
    public class HsvValue : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double val = s["HsvValue"] / 100.0;
            if (val == 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                ColorMath.RgbToHsl(c[i], c[i + 1], c[i + 2], out var hh, out var ss, out var ll);
                double l2 = ColorMath.Clamp01(ll * (1 + val));
                ColorMath.HslToRgb(hh, ss, l2, out var r, out var g, out var b);
                c[i] = r; c[i + 1] = g; c[i + 2] = b;
            }
        }
    }

    /// <summary>曲线：单一强度 S 形曲线（强度 0 = 线性）。完整节点编辑器为后续扩展。</summary>
    public class Curves : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double cr = s["Curve"] / 100.0;
            if (cr == 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl(Scurve(c[i] / 255.0, cr));
                c[i + 1] = Cl(Scurve(c[i + 1] / 255.0, cr));
                c[i + 2] = Cl(Scurve(c[i + 2] / 255.0, cr));
            }
        }
        private static double Scurve(double x, double k)
        {
            double y = x + k * 0.5 * Math.Sin((x - 0.5) * Math.PI);
            return ColorMath.Clamp01(y);
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v * 255);
    }

    /// <summary>RGB 通道增益。</summary>
    public class RgbGain : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double fr = 1 + s["RgbR"] / 100.0;
            double fg = 1 + s["RgbG"] / 100.0;
            double fb = 1 + s["RgbB"] / 100.0;
            if (fr == 1 && fg == 1 && fb == 1) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl(c[i] * fr);
                c[i + 1] = Cl(c[i + 1] * fg);
                c[i + 2] = Cl(c[i + 2] * fb);
            }
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v);
    }
}
