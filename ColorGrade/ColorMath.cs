using System;

namespace TextureGrade.ColorGrade
{
    /// <summary>调色用的小工具：钳制、RGB&lt;-&gt;HSL 互转。</summary>
    internal static class ColorMath
    {
        public static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        public static byte ClampToByte(double v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)Math.Round(v);
        }

        public static void RgbToHsl(byte r, byte g, byte b, out double h, out double s, out double l)
        {
            double rn = r / 255.0, gn = g / 255.0, bn = b / 255.0;
            double max = Math.Max(rn, Math.Max(gn, bn));
            double min = Math.Min(rn, Math.Min(gn, bn));
            double d = max - min;
            l = (max + min) / 2.0;
            if (d < 1e-9) { h = 0; s = 0; return; }
            s = d / (1.0 - Math.Abs(2.0 * l - 1.0));
            if (max == rn) h = (gn - bn) / d + (gn < bn ? 6 : 0);
            else if (max == gn) h = (bn - rn) / d + 2;
            else h = (rn - gn) / d + 4;
            h /= 6.0; // 归一到 0..1
        }

        public static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
        {
            if (s < 1e-9) { r = g = b = ClampToByte(l * 255); return; }
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            r = ClampToByte(Hue2Rgb(p, q, h + 1.0 / 3) * 255);
            g = ClampToByte(Hue2Rgb(p, q, h) * 255);
            b = ClampToByte(Hue2Rgb(p, q, h - 1.0 / 3) * 255);
        }

        private static double Hue2Rgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }
}
