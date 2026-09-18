using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    /// <summary>
    /// 单个调色效果。所有效果都原地修改 RGBA byte[]（每像素 4 字节，顺序 R,G,B,A）。
    /// 顺序由 GradePipeline 固定，保证结果与 Lightroom/Camera Raw 一致。
    /// </summary>
    public interface IGradeEffect
    {
        void Apply(byte[] rgba, int width, int height, GradeSettings s);
    }
}
