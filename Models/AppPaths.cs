using System;
using System.IO;

namespace TextureGrade.Models
{
    /// <summary>
    /// 「插件同级目录」的定位。
    /// 预设、语言配置、操作说明 txt 都放这里 —— 用户一眼就能找到，也方便随插件整体搬走。
    /// 程序目录不可写时（例如装在 Program Files 下）自动回退到 %APPDATA%\TextureGrade。
    /// </summary>
    public static class AppPaths
    {
        private static string _dir;
        private static string _fallback;

        /// <summary>插件 DLL 所在目录（已确保存在）。</summary>
        public static string Dir
        {
            get
            {
                if (_dir == null) _dir = Resolve();
                return _dir;
            }
        }

        /// <summary>%APPDATA%\TextureGrade（回退目录，已确保存在）。</summary>
        public static string FallbackDir
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "TextureGrade");
                    TryCreate(_fallback);
                }
                return _fallback;
            }
        }

        /// <summary>插件目录下的文件路径（只是拼路径，不保证文件存在）。</summary>
        public static string PathIn(string fileName) => System.IO.Path.Combine(Dir, fileName);

        private static string Resolve()
        {
            try
            {
                string asm = typeof(AppPaths).Assembly.Location;
                if (!string.IsNullOrEmpty(asm))
                {
                    string dir = System.IO.Path.GetDirectoryName(asm);
                    if (!string.IsNullOrEmpty(dir) && Writable(dir)) return dir;
                }
            }
            catch { /* 落到回退分支 */ }

            return FallbackDir;
        }

        /// <summary>真写一次文件来判定可写 —— 只看「目录存在」在 Program Files 下会误判。</summary>
        public static bool Writable(string dir)
        {
            try
            {
                TryCreate(dir);
                string probe = System.IO.Path.Combine(dir, ".write_test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        private static void TryCreate(string dir)
        {
            try { Directory.CreateDirectory(dir); } catch { /* 创建失败会在写入时报错 */ }
        }
    }
}
