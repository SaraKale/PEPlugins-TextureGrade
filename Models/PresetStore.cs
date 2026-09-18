using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TextureGrade.Models
{
    /// <summary>
    /// 调色预设（收藏）的存取。
    /// 一个预设 = 一个 JSON 文件，内容就是「参数键 -> 数值」的扁平对象，文件名即预设名。
    /// 存放位置：**插件 DLL 同级的 presets 文件夹** —— 用户一眼就能找到，也方便随插件一起打包搬走。
    /// 若取不到 DLL 路径、或该目录不可写（例如插件装在 Program Files 下），
    /// 自动回退到 %APPDATA%\TextureGrade\presets，保证功能永远可用。
    /// </summary>
    public static class PresetStore
    {
        private const string Ext = ".json";

        private static string _folder;

        /// <summary>预设文件夹（不存在则创建）。命名避开 System.IO.Directory，防止同名解析歧义。</summary>
        public static string Folder
        {
            get
            {
                if (_folder != null) return _folder;
                _folder = ResolveFolder();
                try { System.IO.Directory.CreateDirectory(_folder); } catch { /* 创建失败会在写入时报错 */ }
                return _folder;
            }
        }

        private static string ResolveFolder()
        {
            try
            {
                string asm = typeof(PresetStore).Assembly.Location;
                if (!string.IsNullOrEmpty(asm))
                {
                    string dir = Path.Combine(Path.GetDirectoryName(asm) ?? "", "presets");
                    if (!string.IsNullOrEmpty(dir) && EnsureWritable(dir)) return dir;
                }
            }
            catch { /* 落到下面的回退分支 */ }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TextureGrade", "presets");
        }

        /// <summary>试着建目录并真写一次文件 —— 只判断「目录存在」在 Program Files 下会误判。</summary>
        private static bool EnsureWritable(string dir)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write_test");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        /// <summary>列出全部预设名（按名称排序）。</summary>
        public static List<string> List()
        {
            try
            {
                return System.IO.Directory.GetFiles(Folder, "*" + Ext)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static Dictionary<string, double> Load(string name)
        {
            try
            {
                string p = PathFor(name);
                if (!File.Exists(p)) return new Dictionary<string, double>();
                return MiniJson.ReadObject(File.ReadAllText(p));
            }
            catch
            {
                return new Dictionary<string, double>();
            }
        }

        public static void Save(string name, Dictionary<string, double> values)
        {
            System.IO.Directory.CreateDirectory(Folder);
            File.WriteAllText(PathFor(name), MiniJson.WriteObject(values));
        }

        public static void Delete(string name)
        {
            try
            {
                string p = PathFor(name);
                if (File.Exists(p)) File.Delete(p);
            }
            catch { /* 删除失败不影响主流程 */ }
        }

        public static bool Exists(string name) => File.Exists(PathFor(name));

        private static string PathFor(string name)
            => Path.Combine(Folder, Sanitize(name) + Ext);

        /// <summary>把预设名变成安全文件名（替换掉路径分隔符等非法字符）。</summary>
        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "preset";
            char[] bad = Path.GetInvalidFileNameChars();
            char[] chars = name.Trim().Select(c => Array.IndexOf(bad, c) >= 0 ? '_' : c).ToArray();
            string s = new string(chars).Trim();
            return string.IsNullOrEmpty(s) ? "preset" : s;
        }
    }
}
