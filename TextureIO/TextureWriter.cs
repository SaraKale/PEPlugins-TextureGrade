using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TextureGrade.TextureIO
{
    /// <summary>另存贴图时可选的封装格式。</summary>
    public enum TextureFormat
    {
        Png,          // 无损、带 alpha —— 默认
        Jpg,          // 有损、无 alpha（alpha 与白底合成）
        Bmp,          // 无压缩、无 alpha（24bpp，兼容性最好）
        Gif,          // 256 色索引、无 alpha（贴图基本用不到，只是列出来备用）
        Tiff,         // LZW 压缩、无 alpha
        Tga,          // 32bpp BGRA 无压缩、带 alpha（MMD 常见）
        DdsRaw,       // DDS 未压缩 A8R8G8B8、带 alpha
        DdsDxt1,      // DDS DXT1（8 字节/块，alpha 只有 0/1 两级）
        DdsDxt3,      // DDS DXT3（显式 alpha，适合 alpha 边界锐利的贴图）
        DdsDxt5       // DDS DXT5（插值 alpha，通用首选）
    }

    /// <summary>
    /// 贴图写出。输入统一是调色管线的 RGBA byte[]（每像素 4 字节）。
    ///
    /// 一律不依赖 NuGet：
    ///   - PNG / JPG / BMP / GIF / TIFF 走 GDI+（net48 自带 System.Drawing）
    ///   - TGA 手写（32bpp 无压缩，与自研 TgaReader 完全对称）
    ///   - DDS 手写（未压缩 + DXT1/3/5 编码，与自研 DdsReader 完全对称）
    ///
    /// 注意：GDI+ 的 Bitmap.Save 在目标文件已存在且被占用的情况下会抛异常，
    /// 这里不吞异常 —— 交给调用方提示用户，比静默保存失败要好。
    /// </summary>
    public static class TextureWriter
    {
<<<<<<< HEAD
=======
        public const long MaxPixels = 64L * 1024 * 1024;
>>>>>>> pr-1
        public static string Extension(TextureFormat f)
        {
            switch (f)
            {
                case TextureFormat.Jpg: return ".jpg";
                case TextureFormat.Bmp: return ".bmp";
                case TextureFormat.Gif: return ".gif";
                case TextureFormat.Tiff: return ".tif";
                case TextureFormat.Tga: return ".tga";
                case TextureFormat.DdsRaw:
                case TextureFormat.DdsDxt1:
                case TextureFormat.DdsDxt3:
                case TextureFormat.DdsDxt5: return ".dds";
                default: return ".png";
            }
        }

        /// <summary>该格式是否保留 alpha 通道（用来在界面上提示用户）。</summary>
        public static bool KeepsAlpha(TextureFormat f)
        {
            switch (f)
            {
                case TextureFormat.Png:
                case TextureFormat.Tga:
                case TextureFormat.DdsRaw:
                case TextureFormat.DdsDxt3:
                case TextureFormat.DdsDxt5:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>是否有损（界面提示用）。</summary>
        public static bool IsLossy(TextureFormat f)
            => f == TextureFormat.Jpg || f == TextureFormat.Gif
               || f == TextureFormat.DdsDxt1 || f == TextureFormat.DdsDxt3 || f == TextureFormat.DdsDxt5;

        // ================================================================
        /// <summary>
        /// 按指定格式写文件。jpegQuality 只对 JPG 生效（1-100）。
        /// </summary>
        public static void Save(string path, byte[] rgba, int width, int height,
                                TextureFormat format, int jpegQuality = 92)
        {
<<<<<<< HEAD
=======
            Validate(rgba, width, height);
            if (!Enum.IsDefined(typeof(TextureFormat), format)) throw new ArgumentOutOfRangeException(nameof(format));
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination), ".texturegrade-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                SaveCore(temporary, rgba, width, height, format, jpegQuality);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void Validate(byte[] rgba, int width, int height)
        {
            if (rgba == null) throw new ArgumentNullException(nameof(rgba));
            long pixels = (long)width * height;
            if (width <= 0 || height <= 0 || pixels > MaxPixels) throw new ArgumentOutOfRangeException(nameof(width));
            if (rgba.LongLength < pixels * 4) throw new ArgumentException("RGBA buffer too small.", nameof(rgba));
        }
        private static void SaveCore(string path, byte[] rgba, int width, int height, TextureFormat format, int jpegQuality)
        {
>>>>>>> pr-1
            if (rgba == null) throw new ArgumentNullException("rgba");
            if (width <= 0 || height <= 0) throw new ArgumentException("bad size");
            if (rgba.Length < width * height * 4) throw new ArgumentException("buffer too small");

            switch (format)
            {
                case TextureFormat.Tga: SaveTga(path, rgba, width, height); return;
                case TextureFormat.DdsRaw: SaveDds(path, rgba, width, height, DdsKind.Raw); return;
                case TextureFormat.DdsDxt1: SaveDds(path, rgba, width, height, DdsKind.Dxt1); return;
                case TextureFormat.DdsDxt3: SaveDds(path, rgba, width, height, DdsKind.Dxt3); return;
                case TextureFormat.DdsDxt5: SaveDds(path, rgba, width, height, DdsKind.Dxt5); return;
                case TextureFormat.Gif: SaveIndexed(path, rgba, width, height, ImageFormat.Gif); return;
                default: SaveViaGdi(path, rgba, width, height, format, jpegQuality); return;
            }
        }

        // ---------------- GDI+ 路径：PNG / JPG / BMP / TIFF ----------------
        private static void SaveViaGdi(string path, byte[] rgba, int width, int height,
                                       TextureFormat format, int jpegQuality)
        {
            // JPG/BMP/TIFF 不带 alpha -> 先把 alpha 与白底合成，否则半透明像素会变成黑边
            bool flatten = format == TextureFormat.Jpg || format == TextureFormat.Bmp || format == TextureFormat.Tiff;
            PixelFormat pf = flatten ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;

            using (var bmp = new Bitmap(width, height, pf))
            {
                var rect = new Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, pf);
                try
                {
                    int stride = Math.Abs(data.Stride);
                    var buf = new byte[stride * height];
                    for (int y = 0; y < height; y++)
                    {
                        int srcRow = y * width * 4;
                        int dstRow = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int s = srcRow + x * 4;
                            int d = dstRow + x * (flatten ? 3 : 4);
                            byte r = rgba[s], g = rgba[s + 1], b = rgba[s + 2], a = rgba[s + 3];
                            if (flatten)
                            {
                                if (a != 255)
                                {
                                    int ia = 255 - a;
                                    r = (byte)((r * a + 255 * ia) / 255);
                                    g = (byte)((g * a + 255 * ia) / 255);
                                    b = (byte)((b * a + 255 * ia) / 255);
                                }
                                buf[d] = b; buf[d + 1] = g; buf[d + 2] = r;
                            }
                            else
                            {
                                buf[d] = b; buf[d + 1] = g; buf[d + 2] = r; buf[d + 3] = a;
                            }
                        }
                    }
                    Marshal.Copy(buf, 0, data.Scan0, buf.Length);
                }
                finally { bmp.UnlockBits(data); }

                switch (format)
                {
                    case TextureFormat.Jpg:
<<<<<<< HEAD
                        bmp.Save(path, FindCodec("image/jpeg"), QualityParams(jpegQuality));
                        break;
                    case TextureFormat.Tiff:
                        bmp.Save(path, FindCodec("image/tiff"), CompressionParams());
=======
                        using (var parameters = QualityParams(jpegQuality)) bmp.Save(path, FindCodec("image/jpeg"), parameters);
                        break;
                    case TextureFormat.Tiff:
                        using (var parameters = CompressionParams()) bmp.Save(path, FindCodec("image/tiff"), parameters);
>>>>>>> pr-1
                        break;
                    case TextureFormat.Bmp:
                        bmp.Save(path, ImageFormat.Bmp);
                        break;
                    default:
                        bmp.Save(path, ImageFormat.Png);
                        break;
                }
            }
        }

        private static ImageCodecInfo FindCodec(string mime)
        {
            foreach (var c in ImageCodecInfo.GetImageEncoders())
                if (string.Equals(c.MimeType, mime, StringComparison.OrdinalIgnoreCase)) return c;
            return null;
        }

        private static EncoderParameters QualityParams(int quality)
        {
            var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Max(1, Math.Min(100, quality)));
            return p;
        }

        private static EncoderParameters CompressionParams()
        {
            var p = new EncoderParameters(1);
            p.Param[0] = new EncoderParameter(Encoder.Compression, (long)EncoderValue.CompressionLZW);
            return p;
        }

        // ---------------- GIF：8bpp 索引 + 均匀调色板 ----------------
        private static void SaveIndexed(string path, byte[] rgba, int width, int height, ImageFormat fmt)
        {
            using (var bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed))
            {
                var pal = bmp.Palette;
                // 216 色（6×6×6 均匀立方）+ 后面补灰阶，凑满 256
                int i = 0;
                for (int r = 0; r < 6; r++)
                    for (int g = 0; g < 6; g++)
                        for (int b = 0; b < 6; b++)
                            pal.Entries[i++] = Color.FromArgb(255, r * 51, g * 51, b * 51);
                while (i < 256)
                {
                    int v = (int)Math.Round((i - 216) * 255.0 / (256 - 216 - 1));
                    pal.Entries[i++] = Color.FromArgb(255, v, v, v);
                }
                bmp.Palette = pal;

                var rect = new Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                try
                {
                    int stride = Math.Abs(data.Stride);
                    var buf = new byte[stride * height];
                    for (int y = 0; y < height; y++)
                    {
                        int srcRow = y * width * 4;
                        int dstRow = y * stride;
                        for (int x = 0; x < width; x++)
                        {
                            int s = srcRow + x * 4;
                            byte r = rgba[s], g = rgba[s + 1], b = rgba[s + 2], a = rgba[s + 3];
                            // GIF 的透明只有 0/1 两级，这里干脆与白底合成，避免半透明像素变黑点
                            if (a != 255)
                            {
                                int ia = 255 - a;
                                r = (byte)((r * a + 255 * ia) / 255);
                                g = (byte)((g * a + 255 * ia) / 255);
                                b = (byte)((b * a + 255 * ia) / 255);
                            }
                            buf[dstRow + x] = NearestIndex(r, g, b);
                        }
                    }
                    Marshal.Copy(buf, 0, data.Scan0, buf.Length);
                }
                finally { bmp.UnlockBits(data); }

                bmp.Save(path, fmt);
            }
        }

        /// <summary>均匀立方 + 灰阶两套调色板里挑最近的（O(1)，不做全表扫描）。</summary>
        private static byte NearestIndex(byte r, byte g, byte b)
        {
            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));
            if (max - min <= 10)   // 接近灰 -> 用灰阶梯，色带更干净
                return (byte)(216 + (int)Math.Round((max - min <= 0 ? r : (r + g + b) / 3) * 39 / 255.0));

            int ri = (r * 5 + 127) / 255, gi = (g * 5 + 127) / 255, bi = (b * 5 + 127) / 255;
            return (byte)(ri * 36 + gi * 6 + bi);
        }

        // ---------------- TGA：32bpp BGRA，无压缩，原点左上 ----------------
        private static void SaveTga(string path, byte[] rgba, int width, int height)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((byte)0);            // id length
                w.Write((byte)0);            // color map type
                w.Write((byte)2);            // image type: uncompressed true-color
                w.Write((short)0);           // color map origin
                w.Write((short)0);           // color map length
                w.Write((byte)0);            // color map depth
                w.Write((short)0);           // x origin
                w.Write((short)0);           // y origin
                w.Write((short)width);
                w.Write((short)height);
                w.Write((byte)32);           // bits per pixel
                w.Write((byte)0x28);         // descriptor: 8bit alpha + 原点左上

                var row = new byte[width * 4];
                for (int y = 0; y < height; y++)
                {
                    int src = y * width * 4;
                    for (int x = 0; x < width; x++)
                    {
                        int s = src + x * 4, d = x * 4;
                        row[d] = rgba[s + 2];      // B
                        row[d + 1] = rgba[s + 1];  // G
                        row[d + 2] = rgba[s];      // R
                        row[d + 3] = rgba[s + 3];  // A
                    }
                    w.Write(row);
                }
            }
        }

        // ---------------- DDS ----------------
        private enum DdsKind { Raw, Dxt1, Dxt3, Dxt5 }

        private static void SaveDds(string path, byte[] rgba, int width, int height, DdsKind kind)
        {
            byte[] payload;
            int linearSize = 0, pitch = 0;
            uint pfFlags, fourCC = 0, bitCount = 0, rMask = 0, gMask = 0, bMask = 0, aMask = 0;

            if (kind == DdsKind.Raw)
            {
                payload = new byte[width * height * 4];
                int o = 0;
                for (int i = 0; i < width * height * 4; i += 4)
                {
                    payload[o++] = rgba[i + 2];  // B
                    payload[o++] = rgba[i + 1];  // G
                    payload[o++] = rgba[i];      // R
                    payload[o++] = rgba[i + 3];  // A
                }
                pitch = width * 4;
                pfFlags = 0x1 /*alphapixels*/ | 0x40 /*rgb*/;
                bitCount = 32;
                rMask = 0x00FF0000; gMask = 0x0000FF00; bMask = 0x000000FF; aMask = 0xFF000000;
            }
            else
            {
                payload = CompressDxt(rgba, width, height, kind);
                int blocks = ((width + 3) / 4) * ((height + 3) / 4);
                int bytesPerBlock = kind == DdsKind.Dxt1 ? 8 : 16;
                linearSize = blocks * bytesPerBlock;
                pfFlags = 0x4 /*fourcc*/;
                if (kind == DdsKind.Dxt1) fourCC = 0x31545844;      // 'DXT1'
                else if (kind == DdsKind.Dxt3) fourCC = 0x33545844;  // 'DXT3'
                else fourCC = 0x35545844;                            // 'DXT5'
            }

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((uint)0x20534444);   // 'DDS '
                w.Write((uint)124);          // dwSize
                w.Write((uint)(0x1 /*caps*/ | 0x2 /*height*/ | 0x4 /*width*/ | 0x1000 /*pixelformat*/
                               | (kind == DdsKind.Raw ? 0x8 /*pitch*/ : 0x80000 /*linearsize*/)));
                w.Write((uint)height);
                w.Write((uint)width);
                w.Write((uint)(kind == DdsKind.Raw ? pitch : linearSize));
                w.Write((uint)0);            // depth
                w.Write((uint)0);            // mipmap count
                for (int i = 0; i < 11; i++) w.Write((uint)0);   // reserved

                // DDS_PIXELFORMAT
                w.Write((uint)32);
                w.Write(pfFlags);
                w.Write(fourCC);
                w.Write(bitCount);
                w.Write(rMask); w.Write(gMask); w.Write(bMask); w.Write(aMask);

                w.Write((uint)0x1000);       // dwCaps: DDSCAPS_TEXTURE
                w.Write((uint)0);            // dwCaps2
                w.Write((uint)0);            // dwCaps3
                w.Write((uint)0);            // dwCaps4
                w.Write((uint)0);            // reserved2

                w.Write(payload);
            }
        }

        /// <summary>整图 DXT 压缩：按 4×4 块遍历，不足整块的边角用 clamp 复制补满。</summary>
        private static byte[] CompressDxt(byte[] rgba, int width, int height, DdsKind kind)
        {
            int bw = (width + 3) / 4, bh = (height + 3) / 4;
            int bytesPerBlock = kind == DdsKind.Dxt1 ? 8 : 16;
            var outBuf = new byte[bw * bh * bytesPerBlock];

            var pr = new int[16]; var pg = new int[16]; var pb = new int[16]; var pa = new int[16];
            byte[] block = new byte[bytesPerBlock];

            for (int by = 0; by < bh; by++)
            {
                for (int bx = 0; bx < bw; bx++)
                {
                    // 1) 取块内 16 个像素（越界 clamp，等于把边缘像素拉满整块）
                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            int sx = bx * 4 + x; if (sx >= width) sx = width - 1;
                            int sy = by * 4 + y; if (sy >= height) sy = height - 1;
                            int o = (sy * width + sx) * 4;
                            int k = y * 4 + x;
                            pr[k] = rgba[o]; pg[k] = rgba[o + 1]; pb[k] = rgba[o + 2]; pa[k] = rgba[o + 3];
                        }
                    }

                    Array.Clear(block, 0, bytesPerBlock);

                    if (kind == DdsKind.Dxt3)
                    {
                        // 显式 alpha：每像素 4bit，两个像素挤一个字节
                        for (int k = 0; k < 16; k++)
                        {
<<<<<<< HEAD
                            byte av = (byte)(pa[k] / 17);
=======
                            byte av = (byte)((pa[k] + 8) / 17);
>>>>>>> pr-1
                            if ((k & 1) == 0) block[k / 2] = av;
                            else block[k / 2] |= (byte)(av << 4);
                        }
                        WriteColorBlock(block, 8, pr, pg, pb, pa, false);
                    }
                    else if (kind == DdsKind.Dxt5)
                    {
                        WriteAlphaBlockDxt5(block, pa);
                        WriteColorBlock(block, 8, pr, pg, pb, pa, false);
                    }
                    else
                    {
                        WriteColorBlock(block, 0, pr, pg, pb, pa, true);
                    }

                    Buffer.BlockCopy(block, 0, outBuf, (by * bw + bx) * bytesPerBlock, bytesPerBlock);
                }
            }
            return outBuf;
        }

        /// <summary>
        /// 颜色块（DXT1/3/5 共用 8 字节）。
        /// allowTransparent=true 时若块内有 alpha&lt;128 的像素，会强制进入 DXT1 的「3 色 + 透明」模式。
        /// </summary>
        private static void WriteColorBlock(byte[] block, int offset, int[] pr, int[] pg, int[] pb, int[] pa, bool allowTransparent)
        {
            bool anyTransparent = false;
            if (allowTransparent)
                for (int k = 0; k < 16; k++) if (pa[k] < 128) { anyTransparent = true; break; }

            // 端点：按 RGB 包围盒的两个对角（只统计不透明像素，避免透明像素把色域拉歪）
            int minR = 255, minG = 255, minB = 255, maxR = 0, maxG = 0, maxB = 0, n = 0;
            for (int k = 0; k < 16; k++)
            {
                if (allowTransparent && pa[k] < 128) continue;
                n++;
                if (pr[k] < minR) minR = pr[k];
                if (pg[k] < minG) minG = pg[k];
                if (pb[k] < minB) minB = pb[k];
                if (pr[k] > maxR) maxR = pr[k];
                if (pg[k] > maxG) maxG = pg[k];
                if (pb[k] > maxB) maxB = pb[k];
            }
            if (n == 0) { minR = minG = minB = 0; maxR = maxG = maxB = 0; }

            int c0 = To565(maxR, maxG, maxB);
            int c1 = To565(minR, minG, minB);

            // 3 色模式（含 1 个透明色）的触发条件是 c0 <= c1
            bool threeColor = anyTransparent || c0 <= c1;
            if (c0 <= c1 && !anyTransparent)
            {
                int t = c0; c0 = c1; c1 = t;   // 交换后就是普通 4 色模式
                threeColor = false;
            }
            else if (anyTransparent && c0 > c1)
            {
                int t = c0; c0 = c1; c1 = t;   // 让 c0<=c1 以启用透明槽
                threeColor = true;
            }

            block[offset] = (byte)(c0 & 0xFF);
            block[offset + 1] = (byte)((c0 >> 8) & 0xFF);
            block[offset + 2] = (byte)(c1 & 0xFF);
            block[offset + 3] = (byte)((c1 >> 8) & 0xFF);

            int r0, g0, b0, r1, g1, b1;
            From565(c0, out r0, out g0, out b0);
            From565(c1, out r1, out g1, out b1);

            int[] cand = new int[12];
            cand[0] = r0; cand[1] = g0; cand[2] = b0;
            cand[3] = r1; cand[4] = g1; cand[5] = b1;
            if (threeColor)
            {
                cand[6] = (r0 + r1) / 2; cand[7] = (g0 + g1) / 2; cand[8] = (b0 + b1) / 2;
                cand[9] = cand[10] = cand[11] = -1;   // 索引 3 = 透明
            }
            else
            {
                cand[6] = (2 * r0 + r1) / 3; cand[7] = (2 * g0 + g1) / 3; cand[8] = (2 * b0 + b1) / 3;
                cand[9] = (r0 + 2 * r1) / 3; cand[10] = (g0 + 2 * g1) / 3; cand[11] = (b0 + 2 * b1) / 3;
            }

            // 每像素 2bit 索引，按行序从低位往高位塞
            uint bits = 0;
            for (int k = 0; k < 16; k++)
            {
                int idx;
                if (threeColor && pa[k] < 128) idx = 3;
                else idx = NearestColor(cand, pr[k], pg[k], pb[k], threeColor ? 3 : 4);
                bits |= (uint)(idx & 3) << (k * 2);
            }
            block[offset + 4] = (byte)(bits & 0xFF);
            block[offset + 5] = (byte)((bits >> 8) & 0xFF);
            block[offset + 6] = (byte)((bits >> 16) & 0xFF);
            block[offset + 7] = (byte)((bits >> 24) & 0xFF);
        }

        private static void WriteAlphaBlockDxt5(byte[] block, int[] pa)
        {
            int a0 = 0, a1 = 255;
            for (int k = 0; k < 16; k++)
            {
                if (pa[k] > a0) a0 = pa[k];
                if (pa[k] < a1) a1 = pa[k];
            }
            // a0 <= a1 时进入 6 值模式（索引 6=0、7=255 两个特殊值）
            bool six = a0 <= a1;

            int[] vals = new int[8];
            if (six)
            {
                vals[0] = a0; vals[1] = a1;
                vals[2] = (4 * a0 + a1) / 5; vals[3] = (3 * a0 + 2 * a1) / 5;
                vals[4] = (2 * a0 + 3 * a1) / 5; vals[5] = (a0 + 4 * a1) / 5;
                vals[6] = 0; vals[7] = 255;
            }
            else
            {
                vals[0] = a0; vals[1] = a1;
                vals[2] = (6 * a0 + a1) / 7; vals[3] = (5 * a0 + 2 * a1) / 7;
                vals[4] = (4 * a0 + 3 * a1) / 7; vals[5] = (3 * a0 + 4 * a1) / 7;
                vals[6] = (2 * a0 + 5 * a1) / 7; vals[7] = (a0 + 6 * a1) / 7;
            }

            block[0] = (byte)a0;
            block[1] = (byte)a1;

            ulong bits = 0;
            for (int k = 0; k < 16; k++)
            {
                int best = 0, bd = int.MaxValue;
                int limit = six ? 6 : 8;
                for (int i = 0; i < limit; i++)
                {
                    int d = Math.Abs(vals[i] - pa[k]);
                    if (d < bd) { bd = d; best = i; }
                }
                if (six && pa[k] <= 1) best = 6;
                else if (six && pa[k] >= 254) best = 7;
                bits |= (ulong)(best & 7) << (k * 3);
            }
            for (int i = 0; i < 6; i++) block[2 + i] = (byte)((bits >> (i * 8)) & 0xFF);
        }

        private static int NearestColor(int[] cand, int r, int g, int b, int count)
        {
            int best = 0, bd = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int dr = cand[i * 3] - r, dg = cand[i * 3 + 1] - g, db = cand[i * 3 + 2] - b;
                int d = dr * dr + dg * dg + db * db;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        private static int To565(int r, int g, int b)
            => ((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3);

        private static void From565(int c, out int r, out int g, out int b)
        {
            r = ((c >> 11) & 0x1F) * 255 / 31;
            g = ((c >> 5) & 0x3F) * 255 / 63;
            b = (c & 0x1F) * 255 / 31;
        }

        // ---------------- 尺寸缩放 ----------------
        /// <summary>
        /// 双线性缩放 RGBA 缓冲。newW/newH 与原来相同则原样返回（不复制）。
        /// 缩小时先做一次盒式预平均，避免大比例缩小出现摩尔纹/锯齿（贴图缩一半最常见）。
        /// </summary>
        public static byte[] Resample(byte[] rgba, int width, int height, int newW, int newH)
        {
<<<<<<< HEAD
=======
            Validate(rgba, width, height);
            if (newW <= 0 || newH <= 0 || (long)newW * newH > MaxPixels) throw new ArgumentOutOfRangeException(nameof(newW));
>>>>>>> pr-1
            if (newW == width && newH == height) return rgba;
            if (rgba == null || width <= 0 || height <= 0 || newW <= 0 || newH <= 0) return rgba;

            byte[] src = rgba;
            int sw = width, sh = height;

            // 缩小超过 2 倍时，先按整数倍盒式降采样到 2 倍以内
<<<<<<< HEAD
            while (sw / 2 >= newW && sh / 2 >= newH && sw > 2 && sh > 2)
=======
            while (sw / 2 >= newW && sh / 2 >= newH && sw > 2 && sh > 2 && sw % 2 == 0 && sh % 2 == 0)
>>>>>>> pr-1
            {
                src = BoxHalf(src, sw, sh);
                sw /= 2; sh /= 2;
            }

            var dst = new byte[newW * newH * 4];
            double fx = (double)sw / newW, fy = (double)sh / newH;

            for (int y = 0; y < newH; y++)
            {
                double sy = (y + 0.5) * fy - 0.5;
                if (sy < 0) sy = 0; if (sy > sh - 1) sy = sh - 1;
                int y0 = (int)Math.Floor(sy), y1 = Math.Min(y0 + 1, sh - 1);
                double wy = sy - y0;

                for (int x = 0; x < newW; x++)
                {
                    double sx = (x + 0.5) * fx - 0.5;
                    if (sx < 0) sx = 0; if (sx > sw - 1) sx = sw - 1;
                    int x0 = (int)Math.Floor(sx), x1 = Math.Min(x0 + 1, sw - 1);
                    double wx = sx - x0;

                    int d = (y * newW + x) * 4;
                    int p00 = (y0 * sw + x0) * 4, p01 = (y0 * sw + x1) * 4;
                    int p10 = (y1 * sw + x0) * 4, p11 = (y1 * sw + x1) * 4;

<<<<<<< HEAD
                    for (int c = 0; c < 4; c++)
                    {
                        double top = src[p00 + c] * (1 - wx) + src[p01 + c] * wx;
                        double bot = src[p10 + c] * (1 - wx) + src[p11 + c] * wx;
                        double v = top * (1 - wy) + bot * wy;
                        dst[d + c] = v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)(v + 0.5);
                    }
=======
                    FilterFour(src, p00, p01, p10, p11, (1 - wx) * (1 - wy), wx * (1 - wy), (1 - wx) * wy, wx * wy, dst, d);
>>>>>>> pr-1
                }
            }
            return dst;
        }

        private static byte[] BoxHalf(byte[] src, int w, int h)
        {
            int nw = w / 2, nh = h / 2;
            var dst = new byte[nw * nh * 4];
            for (int y = 0; y < nh; y++)
            {
                for (int x = 0; x < nw; x++)
                {
                    int d = (y * nw + x) * 4;
<<<<<<< HEAD
                    for (int c = 0; c < 4; c++)
                    {
                        int sum = src[((y * 2) * w + x * 2) * 4 + c]
                                + src[((y * 2) * w + x * 2 + 1) * 4 + c]
                                + src[((y * 2 + 1) * w + x * 2) * 4 + c]
                                + src[((y * 2 + 1) * w + x * 2 + 1) * 4 + c];
                        dst[d + c] = (byte)((sum + 2) / 4);
                    }
=======
                    int p = ((y * 2) * w + x * 2) * 4;
                    FilterFour(src, p, p + 4, p + w * 4, p + w * 4 + 4, .25, .25, .25, .25, dst, d);
>>>>>>> pr-1
                }
            }
            return dst;
        }
<<<<<<< HEAD
=======
        private static void FilterFour(byte[] src, int p0, int p1, int p2, int p3,
            double w0, double w1, double w2, double w3, byte[] dst, int offset)
        {
            double a0 = src[p0 + 3] * w0, a1 = src[p1 + 3] * w1, a2 = src[p2 + 3] * w2, a3 = src[p3 + 3] * w3;
            double alpha = a0 + a1 + a2 + a3;
            dst[offset + 3] = (byte)Math.Max(0, Math.Min(255, alpha + .5));
            for (int c = 0; c < 3; c++)
            {
                double value = alpha > 1e-9 ? (src[p0 + c] * a0 + src[p1 + c] * a1 + src[p2 + c] * a2 + src[p3 + c] * a3) / alpha :
                    src[p0 + c] * w0 + src[p1 + c] * w1 + src[p2 + c] * w2 + src[p3 + c] * w3;
                dst[offset + c] = (byte)Math.Max(0, Math.Min(255, value + .5));
            }
        }
>>>>>>> pr-1
    }
}
