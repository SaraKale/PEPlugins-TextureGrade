using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>
    /// 渐变映射：按亮度在两色之间映射（默认黑→白）。amount 控制与原图的混合比例。
    /// 完整多色标编辑器为后续扩展。
    /// </summary>
    public class GradientMap : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double amt = s["Gradient"] / 100.0;
            if (amt <= 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                double lum = (0.2126 * c[i] + 0.7152 * c[i + 1] + 0.0722 * c[i + 2]);
                // 默认渐变 = 灰阶（黑->白），即 lum 本身
                c[i]     = Mix(c[i], lum, amt);
                c[i + 1] = Mix(c[i + 1], lum, amt);
                c[i + 2] = Mix(c[i + 2], lum, amt);
            }
        }
        private static byte Mix(double orig, double grad, double amt)
            => ColorMath.ClampToByte(orig + (grad - orig) * amt);
    }

    /// <summary>黑白：按 amount 混合到亮度。</summary>
    public class Grayscale : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double amt = s["Grayscale"] / 100.0;
            if (amt <= 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                double lum = (0.2126 * c[i] + 0.7152 * c[i + 1] + 0.0722 * c[i + 2]);
                c[i]     = Mix(c[i], lum, amt);
                c[i + 1] = Mix(c[i + 1], lum, amt);
                c[i + 2] = Mix(c[i + 2], lum, amt);
            }
        }
        private static byte Mix(double orig, double gray, double amt)
            => ColorMath.ClampToByte(orig + (gray - orig) * amt);
    }

    /// <summary>反相：按 amount 混合到反色。</summary>
    public class Invert : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double amt = s["Invert"];
            if (amt <= 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Mix(c[i], 255 - c[i], amt);
                c[i + 1] = Mix(c[i + 1], 255 - c[i + 1], amt);
                c[i + 2] = Mix(c[i + 2], 255 - c[i + 2], amt);
            }
        }
        private static byte Mix(double orig, double inv, double amt)
            => ColorMath.ClampToByte(orig + (inv - orig) * amt);
    }

    /// <summary>阈值：以 0.5 为界二值化，amount 控制与原图混合。</summary>
    public class Threshold : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double amt = s["Threshold"];
            if (amt <= 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                double lum = (0.2126 * c[i] + 0.7152 * c[i + 1] + 0.0722 * c[i + 2]);
                double tv = lum >= 127.5 ? 255 : 0;
                c[i]     = Mix(c[i], tv, amt);
                c[i + 1] = Mix(c[i + 1], tv, amt);
                c[i + 2] = Mix(c[i + 2], tv, amt);
            }
        }
        private static byte Mix(double orig, double tv, double amt)
            => ColorMath.ClampToByte(orig + (tv - orig) * amt);
    }
}
