using System;
using System.IO;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 只读文件头拿贴图宽高（不解码像素）—— 导出蒙版时要知道目标尺寸，
    /// 但没必要把一张 4096² 的图解到内存里，几十个材质逐个解码会很慢。
    /// 支持 PNG / BMP / JPEG / GIF / TGA / DDS；这些以外（TIFF 等）回退给 GDI+ 只读头。
    /// </summary>
    public static class TextureSize
    {
        public static bool TryRead(string path, out int width, out int height)
        {
            width = height = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    var head = new byte[64];
                    int n = fs.Read(head, 0, head.Length);
                    if (n < 16) return false;

                    switch (ext)
                    {
                        case ".png":
                            if (head[0] == 0x89 && head[1] == 'P' && head[2] == 'N' && head[3] == 'G')
                            {
                                width = BE32(head, 16);
                                height = BE32(head, 20);
                                return width > 0 && height > 0;
                            }
                            break;

                        case ".bmp":
                            if (head[0] == 'B' && head[1] == 'M')
                            {
                                width = (int)LE32(head, 18);
                                height = Math.Abs((int)LE32(head, 22));   // 顶朝下的 BMP 高度为负
                                return width > 0 && height > 0;
                            }
                            break;

                        case ".gif":
                            if (head[0] == 'G' && head[1] == 'I' && head[2] == 'F')
                            {
                                width = head[6] | (head[7] << 8);
                                height = head[8] | (head[9] << 8);
                                return width > 0 && height > 0;
                            }
                            break;

                        case ".tga":
                            width = head[12] | (head[13] << 8);
                            height = head[14] | (head[15] << 8);
                            return width > 0 && height > 0;

                        case ".dds":
                            if (head[0] == 'D' && head[1] == 'D' && head[2] == 'S' && head[3] == ' ')
                            {
                                height = (int)LE32(head, 12);
                                width = (int)LE32(head, 16);
                                return width > 0 && height > 0;
                            }
                            break;

                        case ".jpg":
                        case ".jpeg":
                            return TryJpeg(fs, out width, out height);
                    }
                }

                // 回退：GDI+ 只读头（validateImageData=false，不做完整解码）
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = System.Drawing.Image.FromStream(fs, false, false))
                {
                    width = img.Width;
                    height = img.Height;
                    return width > 0 && height > 0;
                }
            }
            catch
            {
                width = height = 0;
                return false;
            }
        }

        /// <summary>JPEG：扫 SOF 标记拿尺寸。</summary>
        private static bool TryJpeg(FileStream fs, out int width, out int height)
        {
            width = height = 0;
            fs.Position = 2;   // 跳过 SOI
            var b = new byte[4];
            while (true)
            {
                if (fs.ReadByte() != 0xFF) return false;
                int marker;
                do { marker = fs.ReadByte(); } while (marker == 0xFF);
                if (marker < 0) return false;

                // 无长度字段的标记
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) continue;

                if (fs.Read(b, 0, 2) != 2) return false;
                int len = (b[0] << 8) | b[1];
                if (len < 2) return false;

                bool isSof = (marker >= 0xC0 && marker <= 0xCF)
                             && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
                if (isSof)
                {
                    // SOF 段：精度(1) + 高(2) + 宽(2)
                    var d = new byte[5];
                    if (fs.Read(d, 0, 5) != 5) return false;
                    height = (d[1] << 8) | d[2];
                    width = (d[3] << 8) | d[4];
                    return width > 0 && height > 0;
                }

                fs.Seek(len - 2, SeekOrigin.Current);
            }
        }

        private static int BE32(byte[] b, int o)
            => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

        private static uint LE32(byte[] b, int o)
            => (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    }
}
