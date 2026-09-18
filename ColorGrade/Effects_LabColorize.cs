using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>
    /// Lab 取色环的落点：把像素的 a*/b*（色相 + 彩度）朝取色环选中的目标色靠拢。
    ///
    /// 两种模式：
    ///   · LabUnlock = 0（默认，即勾选「锁定亮度」）：**只改 a*/b*，L\* 原样保留** ——
    ///     不管你在环上选什么颜色，画面明暗关系一点不变，就是「只能选亮度相同的映射」。
    ///   · LabUnlock = 1：连 L* 一起朝目标靠拢，等效于拿选中的 Lab 色去整体染色。
    ///
    /// 相关设置键（键名用 LabUnlock 而非 LabLock，是为了让「未设置 = 0」天然等于默认的「锁定」）：
    ///   LabAmount  强度 0..100（0 = 关闭，此时直接返回，不产生任何开销）
    ///   LabTargetL 目标亮度 L*（0..100）
    ///   LabTargetA / LabTargetB 目标色在 Lab 里的 a*/b*
    ///   LabUnlock  是否解除亮度锁定（0 = 锁定亮度，1 = 亮度也一起变）
    /// </summary>
    public class LabColorize : IGradeEffect
    {
        public void Apply(byte[] rgba, int width, int height, GradeSettings s)
        {
            double amount = s["LabAmount"] / 100.0;
            if (amount <= 1e-6) return;
            if (amount > 1) amount = 1;

            bool lockL = s["LabUnlock"] < 0.5;
            double targetL = s["LabTargetL"];
            double targetA = s["LabTargetA"];
            double targetB = s["LabTargetB"];

            for (int i = 0; i < rgba.Length; i += 4)
            {
                if (rgba[i + 3] == 0) continue;   // 全透明像素不参与

                double L, a, b;
                LabColor.RgbToLab(rgba[i], rgba[i + 1], rgba[i + 2], out L, out a, out b);

                // 锁定亮度时 nL == L：像素自己的感知亮度一动不动
                double nL = lockL ? L : L + (targetL - L) * amount;
                double nA = a + (targetA - a) * amount;
                double nB = b + (targetB - b) * amount;

                byte r, g, bl;
                LabColor.LabToRgb(nL, nA, nB, out r, out g, out bl, out _);
                rgba[i] = r;
                rgba[i + 1] = g;
                rgba[i + 2] = bl;
            }
        }
    }
}
