using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 把 UV 面选区导出成 PS 能直接用的蒙版：
    /// 选中区域 = 纯白，其余 = 纯黑，全不透明白底黑图（可直接当图层蒙版 / 选区通道用）。
    /// 尺寸与贴图一致，所以蒙版和贴图像素是严丝合缝对齐的。
    /// </summary>
    public static class MaskWriter
    {
        public static void SaveBlackWhitePng(string path, bool[] mask, int width, int height)
        {
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var rect = new Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int bytes = width * height * 4;
                    var buf = new byte[bytes];
                    int n = mask == null ? 0 : Math.Min(mask.Length, width * height);
                    for (int i = 0; i < n; i++)
                    {
                        byte v = mask[i] ? (byte)255 : (byte)0;
                        int o = i * 4;
                        // BGRA；灰阶所以三通道同值，A 恒为 255（不透明白底黑图）
                        buf[o] = v; buf[o + 1] = v; buf[o + 2] = v; buf[o + 3] = 255;
                    }
                    // 掩码比画布短时补黑（防御性，正常不会发生）
                    for (int i = n; i < width * height; i++)
                    {
                        int o = i * 4;
                        buf[o] = 0; buf[o + 1] = 0; buf[o + 2] = 0; buf[o + 3] = 255;
                    }
                    Marshal.Copy(buf, 0, data.Scan0, bytes);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }

        /// <summary>
        /// 导出「带 Alpha 通道」的选区 PNG：alpha = 选区（选中 255 / 未选 0），RGB 由 rgbRgba 决定。
        ///   - rgbRgba == null    -> RGB 全白（最纯粹的蒙版：拖进 PS 就是一张白底透明图，可直接贴到图层蒙版上）
        ///   - rgbRgba = 原贴图   -> 原图 + 选区透明，方便在 PS 里对照着看「选的是哪块」
        ///   - rgbRgba = 调色结果 -> 调好的图 + 选区透明，方便只把调色部分抠出来合成
        /// 与 SaveBlackWhitePng 的区别：那张是不透明黑白图（Ctrl+点击缩略图载入选区最方便），
        /// 这张靠 alpha 通道（当图层蒙版 / 当通道用最直接），两者用途不同，所以都保留。
        /// </summary>
        public static void SaveAlphaPng(string path, bool[] mask, int width, int height, byte[] rgbRgba = null)
        {
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var rect = new Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int bytes = width * height * 4;
                    var buf = new byte[bytes];
                    int n = mask == null ? 0 : Math.Min(mask.Length, width * height);
                    bool hasRgb = rgbRgba != null && rgbRgba.Length >= bytes;

                    for (int i = 0; i < n; i++)
                    {
                        int o = i * 4;
                        if (hasRgb)
                        {
                            buf[o] = rgbRgba[o + 2];      // B
                            buf[o + 1] = rgbRgba[o + 1];  // G
                            buf[o + 2] = rgbRgba[o];      // R
                        }
                        else
                        {
                            buf[o] = 255; buf[o + 1] = 255; buf[o + 2] = 255;
                        }
                        buf[o + 3] = mask[i] ? (byte)255 : (byte)0;
                    }
                    // 掩码比画布短时补「白 + 全透明」（防御性，正常不会发生）
                    for (int i = n; i < width * height; i++)
                    {
                        int o = i * 4;
                        buf[o] = 255; buf[o + 1] = 255; buf[o + 2] = 255; buf[o + 3] = 0;
                    }
                    Marshal.Copy(buf, 0, data.Scan0, bytes);
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
