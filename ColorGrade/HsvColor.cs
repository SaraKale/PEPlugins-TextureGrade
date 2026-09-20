using System;

namespace TextureGrade.ColorGrade
{
    /// <summary>Picker coordinates in sRGB: hue in degrees, saturation/value in [0,1].</summary>
    public struct HsvColor
    {
        public double H, S, V;
        public HsvColor(double h, double s, double v) { H = h; S = s; V = v; }

        public static HsvColor FromRgb(int rgb, double neutralHue = 0, double blackSaturation = 0)
        {
            double r = ((rgb >> 16) & 255) / 255.0, g = ((rgb >> 8) & 255) / 255.0, b = (rgb & 255) / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            double h = neutralHue;
            if (delta > 0)
            {
                if (max == r) h = 60 * ((g - b) / delta);
                else if (max == g) h = 60 * ((b - r) / delta + 2);
                else h = 60 * ((r - g) / delta + 4);
            }
            return new HsvColor((h % 360 + 360) % 360, max == 0 ? blackSaturation : delta / max, max);
        }

        public int ToRgb()
        {
            double h = (H % 360 + 360) % 360 / 60;
            double s = OklabColor.Clamp(S, 0, 1), v = OklabColor.Clamp(V, 0, 1);
            double c = v * s, x = c * (1 - Math.Abs(h % 2 - 1)), m = v - c;
            double r = 0, g = 0, b = 0;
            switch ((int)h)
            {
                case 0: r = c; g = x; break;
                case 1: r = x; g = c; break;
                case 2: g = c; b = x; break;
                case 3: g = x; b = c; break;
                case 4: r = x; b = c; break;
                default: r = c; b = x; break;
            }
            return ((int)Math.Round((r + m) * 255) << 16) | ((int)Math.Round((g + m) * 255) << 8) | (int)Math.Round((b + m) * 255);
        }
    }
}
