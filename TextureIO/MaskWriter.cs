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
    }
}
