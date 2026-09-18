using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 极简 DDS 解码器（只读）。支持：
    ///   - 未压缩 RGB/RGBA（24/32 位，按通道掩码提取）
    ///   - DXT1 / BC1、DXT3 / BC2、DXT5 / BC3（MMD 贴图最常见的压缩格式）
    /// 只解码最顶层 mipmap；DX10 扩展头（BC7 等）会抛出友好异常，提示先转 PNG。
    /// 不依赖任何 NuGet 包。
    /// </summary>
    internal static class DdsReader
    {
        private const uint DDS_MAGIC = 0x20534444; // "DDS "
        private const int DDPF_ALPHAPIXELS = 0x1;
        private const int DDPF_FOURCC = 0x4;
        private const int DDPF_RGB = 0x40;

        public static Bitmap Load(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var r = new BinaryReader(fs))
            {
                if (r.ReadUInt32() != DDS_MAGIC)
                    throw new NotSupportedException("不是有效的 DDS 文件（缺少 \"DDS \" 魔数）。");

                r.ReadInt32();                 // dwSize (124)
                r.ReadInt32();                 // dwFlags
                int height = r.ReadInt32();
                int width = r.ReadInt32();
                r.ReadInt32();                 // dwPitchOrLinearSize
                r.ReadInt32();                 // dwDepth
                r.ReadInt32();                 // dwMipMapCount
                r.ReadBytes(44);               // dwReserved1[11]

                r.ReadInt32();                 // ddspf.dwSize (32)
                int pfFlags = r.ReadInt32();
                uint fourCC = r.ReadUInt32();
                int bpp = r.ReadInt32();
                uint rMask = r.ReadUInt32();
                uint gMask = r.ReadUInt32();
                uint bMask = r.ReadUInt32();
                uint aMask = r.ReadUInt32();

                r.ReadInt32(); r.ReadInt32(); r.ReadInt32(); r.ReadInt32(); // caps 1..4
                r.ReadInt32();                 // dwReserved2

                if (width <= 0 || height <= 0 || width > 65536 || height > 65536)
                    throw new NotSupportedException("DDS 尺寸异常：" + width + "x" + height);

                byte[] bgra = new byte[width * height * 4];

                if ((pfFlags & DDPF_FOURCC) != 0)
                {
                    string cc = FourCC(fourCC);
                    if (cc == "DX10")
                        throw new NotSupportedException("DX10 扩展头 DDS（如 BC4/5/6/7）暂不支持，请先转成 PNG。");
                    if (cc != "DXT1" && cc != "DXT3" && cc != "DXT5")
                        throw new NotSupportedException("不支持的 DDS FourCC：" + cc + "（仅支持 DXT1/3/5）。");

                    int blockBytes = cc == "DXT1" ? 8 : 16;
                    int blocksX = (width + 3) / 4;
                    int blocksY = (height + 3) / 4;
                    byte[] data = ReadExact(r, blocksX * blocksY * blockBytes);
                    DecodeDXT(cc, data, width, height, bgra);
                }
                else if ((pfFlags & DDPF_RGB) != 0)
                {
                    DecodeUncompressed(r, width, height, bpp, rMask, gMask, bMask, aMask, bgra);
                }
                else
                {
                    throw new NotSupportedException("不支持的 DDS 像素格式。");
                }

                return ToBitmap(bgra, width, height);
            }
        }

        // ---------------- DXT / BC 解码 ----------------

        private static void DecodeDXT(string fourCC, byte[] data, int w, int h, byte[] bgra)
        {
            int blocksX = (w + 3) / 4;
            int blocksY = (h + 3) / 4;
            int blockBytes = fourCC == "DXT1" ? 8 : 16;

            var colors = new byte[16];   // 4 个 RGBA 颜色
            var alphas = new byte[16];   // 16 个 texel 的 alpha（DXT3/5）

            int p = 0;
            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int off = p; p += blockBytes;

                    if (fourCC == "DXT3") DecodeDxt3Alpha(data, off, alphas);
                    else if (fourCC == "DXT5") DecodeDxt5Alpha(data, off, alphas);
                    else for (int i = 0; i < 16; i++) alphas[i] = 255;

                    int colorOff = fourCC == "DXT1" ? off : off + 8;
                    bool dxt1 = fourCC == "DXT1";
                    DecodeColorTable(data, colorOff, dxt1, colors);

                    uint bits = (uint)(data[colorOff + 4] | (data[colorOff + 5] << 8)
                                     | (data[colorOff + 6] << 16) | (data[colorOff + 7] << 24));

                    for (int ty = 0; ty < 4; ty++)
                    {
                        int y = by * 4 + ty;
                        if (y >= h) break;
                        for (int tx = 0; tx < 4; tx++)
                        {
                            int x = bx * 4 + tx;
                            if (x >= w) continue;
                            int ti = ty * 4 + tx;
                            int ci = (int)((bits >> (ti * 2)) & 3);
                            byte a = dxt1 ? colors[ci * 4 + 3] : alphas[ti];
                            int o = (y * w + x) * 4;
                            bgra[o]     = colors[ci * 4 + 2]; // B
                            bgra[o + 1] = colors[ci * 4 + 1]; // G
                            bgra[o + 2] = colors[ci * 4];     // R
                            bgra[o + 3] = a;                  // A
                        }
                    }
                }
            }
        }

        private static void DecodeColorTable(byte[] d, int off, bool dxt1, byte[] outColors)
        {
            ushort c0 = (ushort)(d[off] | (d[off + 1] << 8));
            ushort c1 = (ushort)(d[off + 2] | (d[off + 3] << 8));
            Expand565(c0, out byte r0, out byte g0, out byte b0);
            Expand565(c1, out byte r1, out byte g1, out byte b1);

            outColors[0] = r0; outColors[1] = g0; outColors[2] = b0; outColors[3] = 255;
            outColors[4] = r1; outColors[5] = g1; outColors[6] = b1; outColors[7] = 255;

            if (!dxt1 || c0 > c1)
            {
                outColors[8]  = (byte)((2 * r0 + r1) / 3);
                outColors[9]  = (byte)((2 * g0 + g1) / 3);
                outColors[10] = (byte)((2 * b0 + b1) / 3);
                outColors[11] = 255;
                outColors[12] = (byte)((r0 + 2 * r1) / 3);
                outColors[13] = (byte)((g0 + 2 * g1) / 3);
                outColors[14] = (byte)((b0 + 2 * b1) / 3);
                outColors[15] = 255;
            }
            else
            {
                // DXT1 且 c0<=c1：第 3 色为均值，第 4 色为透明黑
                outColors[8]  = (byte)((r0 + r1) / 2);
                outColors[9]  = (byte)((g0 + g1) / 2);
                outColors[10] = (byte)((b0 + b1) / 2);
                outColors[11] = 255;
                outColors[12] = 0; outColors[13] = 0; outColors[14] = 0; outColors[15] = 0;
            }
        }

        private static void DecodeDxt3Alpha(byte[] d, int off, byte[] alphas)
        {
            for (int i = 0; i < 16; i++)
            {
                int b = d[off + (i / 2)];
                int nib = (i % 2 == 0) ? (b & 0x0F) : ((b >> 4) & 0x0F);
                alphas[i] = (byte)(nib * 17); // 4bit -> 8bit
            }
        }

        private static void DecodeDxt5Alpha(byte[] d, int off, byte[] alphas)
        {
            int a0 = d[off], a1 = d[off + 1];
            byte[] table = new byte[8];
            table[0] = (byte)a0; table[1] = (byte)a1;
            if (a0 > a1)
                for (int i = 1; i <= 6; i++) table[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
            else
            {
                for (int i = 1; i <= 4; i++) table[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                table[6] = 0; table[7] = 255;
            }

            ulong bits = 0;
            for (int i = 0; i < 6; i++) bits |= (ulong)d[off + 2 + i] << (8 * i);
            for (int i = 0; i < 16; i++)
            {
                int idx = (int)((bits >> (i * 3)) & 7);
                alphas[i] = table[idx];
            }
        }

        private static void Expand565(ushort c, out byte r, out byte g, out byte b)
        {
            int rr = (c >> 11) & 0x1F;
            int gg = (c >> 5) & 0x3F;
            int bb = c & 0x1F;
            r = (byte)((rr << 3) | (rr >> 2));
            g = (byte)((gg << 2) | (gg >> 4));
            b = (byte)((bb << 3) | (bb >> 2));
        }

        // ---------------- 未压缩 ----------------

        private static void DecodeUncompressed(BinaryReader r, int w, int h, int bpp,
            uint rMask, uint gMask, uint bMask, uint aMask, byte[] bgra)
        {
            if (bpp != 24 && bpp != 32)
                throw new NotSupportedException("不支持的未压缩 DDS 位深：" + bpp + "（仅 24/32 位）。");

            int bytesPerPixel = bpp / 8;
            int pitch = ((w * bpp + 7) / 8 + 3) & ~3; // DWORD 对齐
            byte[] row = new byte[pitch];

            for (int y = 0; y < h; y++)
            {
                int got = 0;
                while (got < pitch) got += r.Read(row, got, pitch - got);

                for (int x = 0; x < w; x++)
                {
                    int src = x * bytesPerPixel;
                    uint px = (uint)(row[src] | (row[src + 1] << 8) | (row[src + 2] << 16));
                    if (bytesPerPixel == 4) px |= (uint)(row[src + 3] << 24);

                    byte rr = Extract(px, rMask);
                    byte gg = Extract(px, gMask);
                    byte bb = Extract(px, bMask);
                    byte aa = aMask != 0 ? Extract(px, aMask) : (byte)255;

                    int o = (y * w + x) * 4;
                    bgra[o] = bb; bgra[o + 1] = gg; bgra[o + 2] = rr; bgra[o + 3] = aa;
                }
            }
        }

        private static byte Extract(uint px, uint mask)
        {
            if (mask == 0) return 255;
            int shift = 0;
            while (((mask >> shift) & 1) == 0 && shift < 32) shift++;
            uint bits = mask >> shift;
            uint v = (px & mask) >> shift;
            // 归一化到 8bit
            int width = 0;
            uint m = bits;
            while (m != 0) { width++; m >>= 1; }
            if (width >= 8) return (byte)(v >> (width - 8));
            return (byte)((v * 255) / ((1u << width) - 1));
        }

        // ---------------- 工具 ----------------

        private static byte[] ReadExact(BinaryReader r, int count)
        {
            var buf = new byte[count];
            int got = 0;
            while (got < count)
            {
                int n = r.Read(buf, got, count - got);
                if (n <= 0) break;
                got += n;
            }
            return buf;
        }

        private static string FourCC(uint cc)
        {
            var chars = new char[4];
            for (int i = 0; i < 4; i++) chars[i] = (char)((cc >> (8 * i)) & 0xFF);
            return new string(chars);
        }

        private static Bitmap ToBitmap(byte[] bgra, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
            }
            finally
            {
                bmp.UnlockBits(data);
            }
            return bmp;
        }
    }
}
