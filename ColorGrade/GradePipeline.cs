using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>
    /// 调色管线：按固定顺序跑完所有效果。每次都从「原始贴图」重跑整条链，
    /// 因此天然非破坏、可撤销 —— 改任意滑块都重新计算，不累积误差。
    /// </summary>
    public class GradePipeline
    {
        // 顺序：白平衡 -> 曝光/对比 -> 高光阴影 -> 色阶 -> 饱和 -> HSL分通道 -> 色相/色彩平衡 -> HSV/曲线 -> RGB -> Lab换色 -> 清晰锐化 -> 效果
        // 注1：HslBands 放在全局色相旋转之前，这样 8 色带是按「原始色相」归类的（与 Lightroom 一致）
        // 注2：LabColorize 放在色彩链的最后一环 —— 它承诺「锁定亮度时 L* 不变」，
        //      排在它后面的就只剩清晰度/锐化与效果类，不会被别的色彩操作再改掉亮度。
        private static readonly IGradeEffect[] Effects =
        {
            new WhiteBalance(), new Exposure(), new Contrast(),
            new HighlightsShadows(), new WhitesBlacks(), new Levels(),
            new Saturation(), new Vibrance(), new HslBands(),
            new Hue(), new ColorBalance(),
            new HsvValue(), new Curves(), new RgbGain(), new LabColorize(),
            new Clarity(), new Sharpen(),
            new GradientMap(), new Grayscale(), new Invert(), new Threshold()
        };

        public void Run(byte[] rgba, int width, int height, GradeSettings s)
        {
            foreach (var e in Effects)
                e.Apply(rgba, width, height, s);
        }
    }
}
