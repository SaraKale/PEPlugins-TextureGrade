using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// System.Windows.Media 与 System.Drawing.Imaging 都有 PixelFormat，用别名消歧义
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 生成材质列表用的小缩略图（可冻结的 WPF ImageSource）。
    ///
    /// 性能策略（关键，直接决定"会不会拖慢界面"）：
    ///   1. PNG/JPG/BMP/GIF/TIFF —— 走 WPF 的 BitmapImage + DecodePixelWidth，
    ///      解码器在解码阶段就降采样，**不会**把整张 2048² 图铺开成托管数组；
    ///   2. TGA/DDS —— WPF 不支持，只能走自研解码器拿到 RGBA 再缩到目标尺寸；
    ///   3. 返回的对象全部 Freeze()，可跨线程安全使用，调用方放在后台线程串行生成。
    /// 常驻内存：每张缩略图几十 KB 量级（尺寸由调用方的 size 与图片长宽比共同决定），
    /// 几十个材质合计也只有几 MB，且不与调色管线共享任何缓冲。
    /// </summary>
    public static class ThumbnailFactory
    {
        /// <summary>小于这个边长就直接按原图当缩略图，不必缩放。</summary>
        private const int MinUsefulSize = 8;

        /// <summary>生成缩略图；失败返回 null（调用方回退到漫反射色块）。</summary>
        public static ImageSource Create(string path, int size)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                switch (ext)
                {
                    case ".tga":
                    case ".dds":
                        return FromCustomDecoder(path, size);
                    default:
                        return FromWpfDecoder(path, size);
                }
            }
            catch
            {
                return null;
            }
        }

        // ---------- 路线 1：WPF 解码器（解码期降采样，最省） ----------
        /// <summary>
        /// 先只读文件头拿到原图尺寸，再决定解码目标尺寸：
        ///   短边解到 size*2（2 倍过采样，缩到列表尺寸仍然清晰），
        ///   长边不超过 size*4（避免 512×4096 这种极端长条解码后占大内存）。
        /// 这样每张缩略图常驻内存被限制在几十 KB 量级，几十个材质也毫无压力。
        /// </summary>
        private static ImageSource FromWpfDecoder(string path, int size)
        {
            int srcW, srcH;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var head = BitmapFrame.Create(fs,
                    BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                    BitmapCacheOption.None);
                srcW = head.PixelWidth;
                srcH = head.PixelHeight;
            }
            if (srcW <= 0 || srcH <= 0) return null;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;            // 读完即释放文件句柄
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

            double shortTarget = size * 2.0, longCap = size * 4.0;
            double scale = Math.Min(shortTarget / Math.Min(srcW, srcH),
                                    longCap / Math.Max(srcW, srcH));
            if (scale < 1.0) bi.DecodePixelWidth = Math.Max(1, (int)Math.Round(srcW * scale));

            bi.UriSource = new Uri(path, UriKind.Absolute);
            bi.EndInit();
            bi.Freeze();
            return bi.PixelWidth > MinUsefulSize ? bi : null;
        }

        // ---------- 路线 2：自研解码器（TGA / DDS） ----------
        private static ImageSource FromCustomDecoder(string path, int size)
        {
            var (rgba, w, h) = TextureLoader.Load(path);
            if (w <= 0 || h <= 0) return null;
            return ScaleToSquare(rgba, w, h, size);
        }

        /// <summary>把 RGBA 缓冲等比裁切成 size×size（uniform-to-fill），输出冻结的 BitmapSource。</summary>
        private static ImageSource ScaleToSquare(byte[] rgba, int w, int h, int size)
        {
            using (var src = NewBitmapFromRgba(rgba, w, h))
            using (var dst = new Bitmap(size, size, GdiPixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(dst))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    // uniform-to-fill：短边铺满，长边溢出（由目标矩形负偏移居中裁掉）
                    double scale = Math.Max((double)size / w, (double)size / h);
                    double dw = w * scale, dh = h * scale;
                    g.DrawImage(src, new RectangleF((float)((size - dw) / 2), (float)((size - dh) / 2),
                                                    (float)dw, (float)dh));
                }
                return ToFrozenBitmapSource(dst);
            }
        }

        private static Bitmap NewBitmapFromRgba(byte[] rgba, int w, int h)
        {
            var bmp = new Bitmap(w, h, GdiPixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, GdiPixelFormat.Format32bppArgb);
            try
            {
                int bytes = w * h * 4;
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
            finally { bmp.UnlockBits(data); }
            return bmp;
        }

        /// <summary>GDI+ 位图 -> 冻结的 WPF BitmapSource（直接读像素，不经过 HBITMAP，避免预乘 alpha 的坑）。</summary>
        private static BitmapSource ToFrozenBitmapSource(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var rect = new Rectangle(0, 0, w, h);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, GdiPixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] buf = new byte[stride * h];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                var src = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, buf, stride);
                src.Freeze();
                return src;
            }
            finally { bmp.UnlockBits(data); }
        }
    }
}
