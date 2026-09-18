using System;
using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>
    /// HSL 分通道（Lightroom 的 HSL 面板）：
    /// 把色相环切成 8 个色带（红/橙/黄/绿/青/蓝/紫/品红），每个色带可单独调
    /// 色相偏移、饱和度、明度。像素按与各色带中心的角距离做平滑权重混合，
    /// 因此色带之间过渡连续、不会有硬边。
    ///
    /// 键名：Hsl{Red|Orange|Yellow|Green|Aqua|Blue|Purple|Magenta}{H|S|L}
    ///   H: -100..100 -> 色相偏移 ±36°
    ///   S: -100..100 -> 饱和度（正=增强，负=去色）
    ///   L: -100..100 -> 明度（正=提亮，负=压暗）
    /// </summary>
    public class HslBands : IGradeEffect
    {
        private static readonly string[] Names = { "Red", "Orange", "Yellow", "Green", "Aqua", "Blue", "Purple", "Magenta" };

        /// <summary>各色带中心色相（度）。取值贴近 Lightroom 的 8 色带分布。</summary>
        private static readonly double[] Centers = { 0, 30, 60, 120, 180, 240, 275, 315 };

        /// <summary>色带半宽（度）。相邻色带权重重叠，保证过渡平滑。</summary>
        private const double HalfWidth = 42.0;

        /// <summary>色相滑块的满量程偏移角度。</summary>
        private const double HueRangeDeg = 36.0;

        public void Apply(byte[] c, int w, int h, GradeSettings s)
        {
            int n = Names.Length;
            var shiftH = new double[n];
            var shiftS = new double[n];
            var shiftL = new double[n];
            bool any = false;
            for (int k = 0; k < n; k++)
            {
                shiftH[k] = s["Hsl" + Names[k] + "H"] / 100.0;
                shiftS[k] = s["Hsl" + Names[k] + "S"] / 100.0;
                shiftL[k] = s["Hsl" + Names[k] + "L"] / 100.0;
                if (shiftH[k] != 0 || shiftS[k] != 0 || shiftL[k] != 0) any = true;
            }
            if (!any) return;   // 全零：完全不碰像素（保持管线开销最小）

            int len = w * h * 4;
            for (int i = 0; i < len; i += 4)
            {
                ColorMath.RgbToHsl(c[i], c[i + 1], c[i + 2], out var hh, out var ss, out var ll);
                if (ss < 1e-4) continue;   // 近灰像素无色相可言，跳过

                double hd = hh * 360.0;
                double wsum = 0, dh = 0, ds = 0, dl = 0;
                for (int k = 0; k < n; k++)
                {
                    if (shiftH[k] == 0 && shiftS[k] == 0 && shiftL[k] == 0) continue;
                    double wgt = Weight(hd, Centers[k]);
                    if (wgt <= 0) continue;
                    wsum += wgt;
                    dh += wgt * shiftH[k];
                    ds += wgt * shiftS[k];
                    dl += wgt * shiftL[k];
                }
                if (wsum <= 0) continue;

                dh /= wsum; ds /= wsum; dl /= wsum;   // 归一化权重 -> 平滑混合，不因色带重叠放大强度

                double h2 = hh + dh * HueRangeDeg / 360.0;
                h2 -= Math.Floor(h2);

                double s2 = ds >= 0 ? ss + (1 - ss) * ds : ss * (1 + ds);
                double l2 = dl >= 0 ? ll + (1 - ll) * dl : ll * (1 + dl);

                ColorMath.HslToRgb(h2, ColorMath.Clamp01(s2), ColorMath.Clamp01(l2),
                                   out var r, out var g, out var b);
                c[i] = r; c[i + 1] = g; c[i + 2] = b;
            }
        }

        /// <summary>色相 h（度）与色带中心中心的线性衰减权重（角距离）。</summary>
        private static double Weight(double h, double center)
        {
            double d = Math.Abs(h - center);
            if (d > 180) d = 360 - d;           // 环形最短距离
            if (d >= HalfWidth) return 0;
            return 1.0 - d / HalfWidth;
        }
    }
}
