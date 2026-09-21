using System;
using System.Threading;
using TextureGrade.Models;

namespace TextureGrade.ColorGrade
{
    public sealed class RenderedGrade
    {
        public byte[] Rgba;
        public int Width, Height;
    }
    public static class GradeRenderer
    {
        public static RenderedGrade Render(byte[] original, int width, int height, GradeSettings settings,
            bool[] mask = null, int maxSide = 0, CancellationToken token = default)
        {
            double scale = maxSide > 0 ? Math.Min(1, (double)maxSide / Math.Max(width, height)) : 1;
            int w = Math.Max(1, (int)Math.Round(width * scale)), h = Math.Max(1, (int)Math.Round(height * scale));
            byte[] source; bool[] selected;
            if (w == width && h == height) { source = original; selected = mask; }
            else
            {
                source = new byte[w * h * 4]; selected = mask == null ? null : new bool[w * h];
                for (int y = 0; y < h; y++)
                {
                    token.ThrowIfCancellationRequested();
                    int sy = Math.Min(height - 1, (int)((y + .5) * height / h));
                    for (int x = 0; x < w; x++)
                    {
                        int sx = Math.Min(width - 1, (int)((x + .5) * width / w));
                        int p = y * w + x, old = sy * width + sx;
                        Buffer.BlockCopy(original, old * 4, source, p * 4, 4);
                        if (selected != null) selected[p] = mask[old];
                    }
                }
            }
            var result = (byte[])source.Clone();
            new GradePipeline().Run(result, w, h, settings, selected, token);
            for (int p = 0; p < w * h; p++)
            {
                if ((p & 16383) == 0) token.ThrowIfCancellationRequested();
                int o = p * 4;
                if ((selected != null && !selected[p]) || source[o + 3] == 0) Buffer.BlockCopy(source, o, result, o, 4);
                else result[o + 3] = source[o + 3];
            }
            return new RenderedGrade { Rgba = result, Width = w, Height = h };
        }
    }
}
