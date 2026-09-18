using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 贴图读写。统一读成 RGBA byte[]（每像素 4 字节，调色管线内部统一按 RGBA 计算），写回 PNG（无损）。
    /// 解码不依赖任何 NuGet 包：
    ///   - PNG/JPG/BMP/GIF/TIFF 用 GDI+（net48 自带 System.Drawing）
    ///   - TGA 用手写的极简解码器 TgaReader（覆盖 MMD 最常见的 24/32 位真彩与 RLE 真彩）
    ///   - DDS 用手写的极简解码器 DdsReader（未压缩 RGB(A) + DXT1/3/5）
    /// </summary>
    public static class TextureLoader
    {
        public static (byte[] rgba, int width, int height) Load(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            Bitmap bmp;
            switch (ext)
            {
                case ".tga": bmp = TgaReader.Load(path); break;
                case ".dds": bmp = DdsReader.Load(path); break;
                default: bmp = new Bitmap(path); break;
            }
            using (bmp) return FromBitmap(bmp);
        }

        private static (byte[] rgba, int width, int height) FromBitmap(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int bytes = w * h * 4; // Format32bppArgb 无行对齐填充，stride == w*4
                byte[] bgra = new byte[bytes];
                Marshal.Copy(data.Scan0, bgra, 0, bytes);
                // GDI+ 32bppArgb 字节序为 BGRA，转换为 RGBA 供调色管线使用
                byte[] rgba = new byte[bytes];
                for (int i = 0; i < bytes; i += 4)
                {
                    rgba[i] = bgra[i + 2];
                    rgba[i + 1] = bgra[i + 1];
                    rgba[i + 2] = bgra[i];
                    rgba[i + 3] = bgra[i + 3];
                }
                return (rgba, w, h);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        public static void SavePng(string path, byte[] rgba, int width, int height)
        {
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var rect = new Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int bytes = width * height * 4;
                    byte[] bgra = new byte[bytes];
                    for (int i = 0; i < bytes; i += 4)
                    {
                        bgra[i] = rgba[i + 2];
                        bgra[i + 1] = rgba[i + 1];
                        bgra[i + 2] = rgba[i];
                        bgra[i + 3] = rgba[i + 3];
                    }
                    Marshal.Copy(bgra, 0, data.Scan0, bytes);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
