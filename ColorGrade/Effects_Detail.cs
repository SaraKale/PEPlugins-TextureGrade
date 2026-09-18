using System;
using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>清晰度：中调局部对比度（近似，无需邻域采样）。0 = 不变。</summary>
    public class Clarity : IGradeEffect
    {
        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            double cl = s["Clarity"] / 100.0;
            if (cl == 0) return;
            int n = w * h * 4;
            for (int i = 0; i < n; i += 4)
            {
                c[i]     = Cl(Midtone(c[i] / 255.0, cl));
                c[i + 1] = Cl(Midtone(c[i + 1] / 255.0, cl));
                c[i + 2] = Cl(Midtone(c[i + 2] / 255.0, cl));
            }
        }
        private static double Midtone(double x, double k)
        {
            double edge = 1 - Math.Abs(x - 0.5) * 2; // 中调=1，极值=0
            double y = x + k * (x - 0.5) * edge * 0.6;
            return ColorMath.Clamp01(y);
        }
        private static byte Cl(double v) => ColorMath.ClampToByte(v * 255);
    }

    /// <summary>
    /// 锐化：非锐化掩模（Unsharp Mask）。result = orig + amount*(orig - blur)，
    /// blur 用可分离的 1-2-1 高斯核（横/纵两趟），边界 clamp。
    /// 滑块 0 = 不变；正值锐化，负值轻微柔化。保留 alpha。
    /// </summary>
    public class Sharpen : IGradeEffect
    {
        public void Apply(byte[] rgba, int width, int height, GradeSettings s)
        {
            double amt = s["Sharpen"] / 100.0;
            if (amt == 0 || width < 3 || height < 3) return;

            int n = rgba.Length;
            var src = (byte[])rgba.Clone();
            var blur = new byte[n];
            BlurSeparable(src, blur, width, height);

            for (int i = 0; i < n; i += 4)
            {
                for (int c = 0; c < 3; c++) // 只处理 RGB，alpha 保持
                {
                    int k = i + c;
                    double o = src[k];
                    double b = blur[k];
                    rgba[k] = ColorMath.ClampToByte(o + amt * (o - b));
                }
            }
        }

        /// <summary>可分离 1-2-1 高斯模糊（两趟），等价于 3x3 核 [1 2 1;2 4 2;1 2 1]/16。</summary>
        private static void BlurSeparable(byte[] src, byte[] dst, int w, int h)
        {
            var tmp = new byte[src.Length];

            // 水平趟
            for (int y = 0; y < h; y++)
            {
                int row = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int xm = x > 0 ? x - 1 : 0;
                    int xp = x < w - 1 ? x + 1 : w - 1;
                    int c0 = row + xm * 4, c1 = row + x * 4, c2 = row + xp * 4;
                    for (int c = 0; c < 4; c++)
                        tmp[c1 + c] = (byte)((src[c0 + c] + 2 * src[c1 + c] + src[c2 + c]) >> 2);
                }
            }

            // 垂直趟
            for (int y = 0; y < h; y++)
            {
                int ym = y > 0 ? y - 1 : 0;
                int yp = y < h - 1 ? y + 1 : h - 1;
                for (int x = 0; x < w; x++)
                {
                    int o = (y * w + x) * 4;
                    int a = (ym * w + x) * 4, b = o, c = (yp * w + x) * 4;
                    for (int ch = 0; ch < 4; ch++)
                        dst[o + ch] = (byte)((tmp[a + ch] + 2 * tmp[b + ch] + tmp[c + ch]) >> 2);
                }
            }
        }
    }
}
