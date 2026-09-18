using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 极简 TGA 解码器（只读）。支持 24/32 位真彩（类型 2）与 RLE 真彩（类型 10）。
    /// 覆盖绝大多数 MMD 贴图；其他类型（调色板/灰度）会抛出友好异常，提示先转成 PNG。
    /// 不依赖任何 NuGet 包。
    /// </summary>
    internal static class TgaReader
    {
        public static Bitmap Load(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var r = new BinaryReader(fs))
            {
                int idLen = r.ReadByte();
                r.ReadByte(); // color map type（真彩图恒为 0）
                int imageType = r.ReadByte();
                r.ReadBytes(5); // color map spec (first index 2 + length 2 + depth 1)
                r.ReadInt16(); // x origin
                r.ReadInt16(); // y origin
                int width = r.ReadInt16();
                int height = r.ReadInt16();
                int pixelDepth = r.ReadByte();
                int descriptor = r.ReadByte();

                if (idLen > 0) r.ReadBytes(idLen);

                if (imageType != 2 && imageType != 10)
                    throw new NotSupportedException(
                        $"TGA 图像类型 {imageType} 暂不支持（TextureGrade 仅支持 24/32 位真彩与 RLE 真彩）。请先把贴图转成 PNG。");
                if (pixelDepth != 24 && pixelDepth != 32)
                    throw new NotSupportedException(
                        $"TGA 位深 {pixelDepth} 暂不支持（仅支持 24/32 位）。请先把贴图转成 PNG。");

                int bpp = pixelDepth / 8;
                bool rle = imageType == 10;
                byte[] raw = DecodePixels(r, width, height, bpp, rle);

                bool bottomUp = (descriptor & 0x20) == 0;
                var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    byte[] outBuf = new byte[width * height * 4];
                    for (int y = 0; y < height; y++)
                    {
                        int srcY = bottomUp ? (height - 1 - y) : y;
                        for (int x = 0; x < width; x++)
                        {
                            int src = (srcY * width + x) * bpp;
                            byte b = raw[src], g = raw[src + 1], rr = raw[src + 2];
                            byte a = bpp == 4 ? raw[src + 3] : (byte)255;
                            int o = (y * width + x) * 4;
                            // GDI+ 32bppArgb = BGRA
                            outBuf[o] = b; outBuf[o + 1] = g; outBuf[o + 2] = rr; outBuf[o + 3] = a;
                        }
                    }
                    Marshal.Copy(outBuf, 0, data.Scan0, outBuf.Length);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                return bmp;
            }
        }

        private static byte[] DecodePixels(BinaryReader r, int w, int h, int bpp, bool rle)
        {
            int total = w * h * bpp;
            byte[] buf = new byte[total];
            if (!rle)
            {
                int read = 0;
                while (read < total) read += r.Read(buf, read, total - read);
                return buf;
            }
            int pos = 0;
            while (pos < total)
            {
                int hdr = r.ReadByte();
                int count = (hdr & 0x7F) + 1;
                if ((hdr & 0x80) != 0)
                {
                    byte[] px = r.ReadBytes(bpp);
                    for (int i = 0; i < count; i++) { Array.Copy(px, 0, buf, pos, bpp); pos += bpp; }
                }
                else
                {
                    for (int i = 0; i < count; i++) { byte[] px = r.ReadBytes(bpp); Array.Copy(px, 0, buf, pos, bpp); pos += bpp; }
                }
            }
            return buf;
        }
    }
}
