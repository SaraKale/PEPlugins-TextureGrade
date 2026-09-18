using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>色温(暖/冷)与色调(绿/品红)。0 = 不变。</summary>
    public class WhiteBalance : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double temp = s["Temperature"] / 100.0; // 正=暖(加红减蓝)
            double tint = s["Tint"] / 100.0;        // 正=品红(加红蓝减绿)
            if (temp == 0 && tint == 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl(c[i]     + temp * 60 + tint * 30);
                c[i + 1] = Cl(c[i + 1] - tint * 30);
                c[i + 2] = Cl(c[i + 2] - temp * 60 + tint * 30);
            }
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v);
    }

    /// <summary>曝光：EV，按 2^EV 倍率缩放亮度。</summary>
    public class Exposure : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double ev = s["Exposure"];
            if (ev == 0) return;
            double mul = System.Math.Pow(2, ev);
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl(c[i] * mul);
                c[i + 1] = Cl(c[i + 1] * mul);
                c[i + 2] = Cl(c[i + 2] * mul);
            }
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v);
    }

    /// <summary>对比度：以中灰(0.5)为轴拉伸，1.0=无变化。</summary>
    public class Contrast : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double p = s["Contrast"];
            if (p == 0) return;
            double f = (p + 100) / 100.0;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl((c[i] / 255.0 - 0.5) * f + 0.5);
                c[i + 1] = Cl((c[i + 1] / 255.0 - 0.5) * f + 0.5);
                c[i + 2] = Cl((c[i + 2] / 255.0 - 0.5) * f + 0.5);
            }
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v * 255);
    }

    /// <summary>高光/阴影：按亮度分区的提亮（平滑过渡）。</summary>
    public class HighlightsShadows : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double hl = s["Highlights"] / 100.0;
            double sh = s["Shadows"] / 100.0;
            if (hl == 0 && sh == 0) return;
            const double amt = 90;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                double lum = (0.2126 * c[i] + 0.7152 * c[i + 1] + 0.0722 * c[i + 2]) / 255.0;
                double tHi = Smoothstep(0.5, 1.0, lum);
                double tLo = Smoothstep(0.5, 0.0, lum); // 暗部权重
                double d = hl * tHi * amt + sh * tLo * amt;
                c[i]     = Cl(c[i] + d);
                c[i + 1] = Cl(c[i + 1] + d);
                c[i + 2] = Cl(c[i + 2] + d);
            }
        }
        private static double Smoothstep(double e0, double e1, double x)
        {
            double t = (x - e0) / (e1 - e0);
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            return t * t * (3 - 2 * t);
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v);
    }

    /// <summary>白色/黑色：线性分区偏移（高光区/暗部区）。</summary>
    public class WhitesBlacks : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double wh = s["Whites"] / 100.0;
            double bl = s["Blacks"] / 100.0;
            if (wh == 0 && bl == 0) return;
            const double amt = 90;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                double lum = (0.2126 * c[i] + 0.7152 * c[i + 1] + 0.0722 * c[i + 2]) / 255.0;
                double d = wh * lum * amt + bl * (1 - lum) * amt;
                c[i]     = Cl(c[i] + d);
                c[i + 1] = Cl(c[i + 1] + d);
                c[i + 2] = Cl(c[i + 2] + d);
            }
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v);
    }

    /// <summary>色阶：黑点 / 白点 / 灰阶(gamma)。0 值=无变化。</summary>
    public class Levels : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double black = s["LevelsBlack"] / 200.0;   // -0.5..0.5
            double white = 1.0 - s["LevelsWhite"] / 200.0; // 0.5..1.5
            double gammaExp = 1.0 + s["LevelsGamma"] / 100.0; // 0..2
            if (black == 0 && white == 1 && gammaExp == 1) return;
            double range = System.Math.Max(1e-6, white - black);
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl((c[i] / 255.0 - black) / range, gammaExp);
                c[i + 1] = Cl((c[i + 1] / 255.0 - black) / range, gammaExp);
                c[i + 2] = Cl((c[i + 2] / 255.0 - black) / range, gammaExp);
            }
        }
        private static byte Cl(double norm, double gammaExp)
        {
            double v = ColorMath.Clamp01(norm);
            v = System.Math.Pow(v, 1.0 / System.Math.Max(0.01, gammaExp));
            return ColorMath.ClampToByte(v * 255);
        }
    }
}
