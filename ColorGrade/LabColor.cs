using System;

namespace TextureGrade.ColorGrade
{
    /// <summary>
    /// CIE L*a*b* / LCh 与 sRGB 的互转（D65 白点，sRGB 传递函数）。
    ///
    /// 为什么需要它：Lab 的 L* 是「感知亮度」，跟明度/饱和度耦合度很低。
    /// 于是在 Lab 里只改 a*/b*（色相与彩度）、保留 L*，就能做到
    /// 「换颜色但画面明暗关系完全不变」——
    /// 这正是 Lab 取色环「锁定亮度」模式要的映射。
    /// </summary>
    public static class LabColor
    {
        // D65 白点
        private const double Xn = 0.95047, Yn = 1.00000, Zn = 1.08883;
        private const double Eps = 216.0 / 24389.0;    // (6/29)^3
        private const double Kappa = 24389.0 / 27.0;   // (29/3)^3

        /// <summary>sRGB 字节 -> 线性光。查表，避免每像素 pow。</summary>
        private static readonly double[] SrgbToLinear = BuildSrgbToLinear();

        private static double[] BuildSrgbToLinear()
        {
            var t = new double[256];
            for (int i = 0; i < 256; i++)
            {
                double c = i / 255.0;
                t[i] = c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }
            return t;
        }

        // ---------------- sRGB <-> Lab ----------------

        public static void RgbToLab(byte r, byte g, byte b, out double L, out double a, out double bb)
        {
            double lr = SrgbToLinear[r], lg = SrgbToLinear[g], lb = SrgbToLinear[b];

            double x = (0.4124564 * lr + 0.3575761 * lg + 0.1804375 * lb) / Xn;
            double y = (0.2126729 * lr + 0.7151522 * lg + 0.0721750 * lb) / Yn;
            double z = (0.0193339 * lr + 0.1191920 * lg + 0.9503041 * lb) / Zn;

            double fx = F(x), fy = F(y), fz = F(z);
            L = 116.0 * fy - 16.0;
            a = 500.0 * (fx - fy);
            bb = 200.0 * (fy - fz);
        }

        private static double F(double t)
            => t > Eps ? Math.Pow(t, 1.0 / 3.0) : (Kappa * t + 16.0) / 116.0;

        private static double FInv(double f)
        {
            double f3 = f * f * f;
            return f3 > Eps ? f3 : (116.0 * f - 16.0) / Kappa;
        }

        /// <summary>
        /// Lab -> sRGB。inGamut 表示结果是否落在 sRGB 色域内（false 说明该 Lab 色在 sRGB 里表达不出来，
        /// 返回值已经被钳制到最近的边界色）。
        /// </summary>
        public static void LabToRgb(double L, double a, double b, out byte r, out byte g, out byte bl, out bool inGamut)
        {
            double fy = (L + 16.0) / 116.0;
            double fx = fy + a / 500.0;
            double fz = fy - b / 200.0;

            double x = Xn * FInv(fx);
            double y = Yn * FInv(fy);
            double z = Zn * FInv(fz);

            double lr = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
            double lg = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
            double lb = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;

            const double t = 1e-4;
            inGamut = lr >= -t && lr <= 1 + t && lg >= -t && lg <= 1 + t && lb >= -t && lb <= 1 + t;

            r = LinearToSrgbByte(lr);
            g = LinearToSrgbByte(lg);
            bl = LinearToSrgbByte(lb);
        }

        public static byte LinearToSrgbByte(double v)
        {
            if (v <= 0) return 0;
            if (v >= 1) return 255;
            double s = v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
            return (byte)Math.Round(s * 255.0);
        }

        // ---------------- LCh（L* / 彩度 / 色相角） ----------------

        /// <summary>由 a*/b* 求色相角（度，0..360）。</summary>
        public static double HueDeg(double a, double b)
        {
            double h = Math.Atan2(b, a) * 180.0 / Math.PI;
            return h < 0 ? h + 360.0 : h;
        }

        public static void LchToLab(double L, double c, double hueDeg, out double a, out double b)
        {
            double rad = hueDeg * Math.PI / 180.0;
            a = c * Math.Cos(rad);
            b = c * Math.Sin(rad);
        }

        /// <summary>
        /// 在「亮度固定为 L*、色相固定为 hueDeg」的前提下，二分出 sRGB 色域内能达到的最大彩度。
        /// 这是「等亮度色环」的半径上限：环上每个色相都取到这个值，颜色才最饱满，
        /// 而过不了色域的色相会自动收缩（例如深亮度下没有鲜黄色）。
        /// </summary>
        public static double MaxChroma(double L, double hueDeg)
        {
            if (L <= 0.2 || L >= 99.8) return 0;

            double lo = 0, hi = 200;
            // 若极端情况下 200 仍在色域内（几乎不可能），直接返回它
            if (InGamut(L, hi, hueDeg)) return hi;

            for (int i = 0; i < 26; i++)
            {
                double mid = (lo + hi) * 0.5;
                if (InGamut(L, mid, hueDeg)) lo = mid; else hi = mid;
            }
            return lo;
        }

        private static bool InGamut(double L, double c, double hueDeg)
        {
            double a, b;
            LchToLab(L, c, hueDeg, out a, out b);
            byte r, g, bl;
            bool ok;
            LabToRgb(L, a, b, out r, out g, out bl, out ok);
            return ok;
        }

        /// <summary>L* 固定、色相固定、给定彩度 -> sRGB（越界会钳到色域边界）。</summary>
        public static void ColorFromLch(double L, double c, double hueDeg, out byte r, out byte g, out byte b)
        {
            double a, bb;
            LchToLab(L, c, hueDeg, out a, out bb);
            bool ok;
            LabToRgb(L, a, bb, out r, out g, out b, out ok);
        }
    }
}
