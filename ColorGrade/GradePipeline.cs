using TextureGrade.Models;
using System.Threading;

namespace TextureGrade.ColorGrade
{
    /// <summary>
    /// 调色管线：按固定顺序跑完所有效果。每次都从「原始贴图」重跑整条链，
    /// 因此天然非破坏、可撤销 —— 改任意滑块都重新计算，不累积误差。
    /// </summary>
    public class GradePipeline
    {
        // 顺序：白平衡 -> 曝光/对比 -> 高光阴影 -> 色阶 -> 饱和 -> HSL分通道 -> 色相/色彩平衡 -> HSV/曲线 -> RGB -> 清晰锐化 -> 效果
        // 注1：HslBands 放在全局色相旋转之前，这样 8 色带是按「原始色相」归类的（与 Lightroom 一致）
        private static readonly IGradeEffect[] Effects =
        {
            new WhiteBalance(), new Exposure(), new Contrast(),
            new HighlightsShadows(), new WhitesBlacks(), new Levels(),
            new Saturation(), new Vibrance(), new HslBands(),
            new Hue(), new ColorBalance(),
            new HsvValue(), new Curves(), new RgbGain(),
            new Clarity(), new Sharpen(),
            new Grayscale(), new Invert(), new Threshold()
        };

        public void Run(byte[] rgba, int width, int height, GradeSettings s, bool[] mask = null,
            CancellationToken cancellationToken = default)
        {
            var recolor = RecolorSettings.Read(s);
            if (recolor.Mode == RecolorMode.Palette)
                new PaletteRecolor(recolor).Apply(rgba, mask, cancellationToken);
            foreach (var e in Effects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                e.Apply(rgba, width, height, s);
            }
        }
    }
}
