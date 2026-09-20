using System;

namespace TextureGrade.ColorGrade
{
    public struct Oklab
    {
        public double L, A, B;
        public Oklab(double l, double a, double b) { L = l; A = a; B = b; }
        public double Chroma => Math.Sqrt(A * A + B * B);
        public double Hue => (Math.Atan2(B, A) * 180 / Math.PI + 360) % 360;
    }

    /// <summary>D65 sRGB / OKLab / OKLCH. Matrices: Björn Ottosson, 2021 (public domain).</summary>
    public static class OklabColor
    {
        private static readonly double[] Linear = MakeLinear();
        private static double[] MakeLinear()
        {
            var table = new double[256];
            for (int i = 0; i < 256; i++)
            {
                double v = i / 255.0;
                table[i] = v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
            }
            return table;
        }
        public static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
        public static Oklab FromRgb(int rgb) => FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        public static Oklab FromRgb(byte r, byte g, byte b)
        {
            double x = Linear[r], y = Linear[g], z = Linear[b];
            double l = Math.Pow(.4122214708 * x + .5363325363 * y + .0514459929 * z, 1.0 / 3);
            double m = Math.Pow(.2119034982 * x + .6806995451 * y + .1073969566 * z, 1.0 / 3);
            double s = Math.Pow(.0883024619 * x + .2817188376 * y + .6299787005 * z, 1.0 / 3);
            return new Oklab(.2104542553 * l + .7936177850 * m - .0040720468 * s,
                1.9779984951 * l - 2.4285922050 * m + .4505937099 * s,
                .0259040371 * l + .7827717662 * m - .8086757660 * s);
        }
        public static Oklab FromLch(double l, double c, double h)
            => new Oklab(l, c * Math.Cos(h * Math.PI / 180), c * Math.Sin(h * Math.PI / 180));

        public static void ToLinear(Oklab c, out double r, out double g, out double b)
        {
            double l = c.L + .3963377774 * c.A + .2158037573 * c.B;
            double m = c.L - .1055613458 * c.A - .0638541728 * c.B;
            double s = c.L - .0894841775 * c.A - 1.2914855480 * c.B;
            l *= l * l; m *= m * m; s *= s * s;
            r = 4.0767416621 * l - 3.3077115913 * m + .2309699292 * s;
            g = -1.2684380046 * l + 2.6097574011 * m - .3413193965 * s;
            b = -.0041960863 * l - .7034186147 * m + 1.7076147010 * s;
        }
        public static bool InGamut(Oklab c)
        {
            ToLinear(c, out double r, out double g, out double b);
            const double e = 1e-7;
            return r >= -e && g >= -e && b >= -e && r <= 1 + e && g <= 1 + e && b <= 1 + e;
        }
        public static Oklab FitGamut(Oklab c)
        {
            c.L = Clamp(c.L, 0, 1);
            if (InGamut(c)) return c;
            double lo = 0, hi = 1;
            for (int i = 0; i < 24; i++)
            {
                double scale = (lo + hi) / 2;
                if (InGamut(new Oklab(c.L, c.A * scale, c.B * scale))) lo = scale; else hi = scale;
            }
            return new Oklab(c.L, c.A * lo, c.B * lo);
        }
        public static double MaxChroma(double l, double h)
        {
            return FitGamut(FromLch(l, .5, h)).Chroma;
        }
        public static int ToRgb(Oklab c)
        {
            ToLinear(FitGamut(c), out double r, out double g, out double b);
            return (LinearToSrgbByte(r) << 16) | (LinearToSrgbByte(g) << 8) | LinearToSrgbByte(b);
        }
        private static byte LinearToSrgbByte(double v)
        {
            if (v <= 0) return 0;
            if (v >= 1) return 255;
            double s = v <= .0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1.0 / 2.4) - .055;
            return (byte)Math.Round(s * 255);
        }
        public static double DistanceSquared(Oklab a, Oklab b)
        {
            double dl = a.L - b.L, da = a.A - b.A, db = a.B - b.B;
            return .25 * dl * dl + da * da + db * db;
        }
    }
}
