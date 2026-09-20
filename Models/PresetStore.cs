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

        /// <summary>预设列表的排序方式。</summary>
        public enum PresetSort
        {
            /// <summary>按名称（当前区域性的忽略大小写排序）——默认。</summary>
            Name,
            /// <summary>按修改时间，最近改过的排最前。改预设名也算改动。</summary>
            Time,
            /// <summary>手动顺序（用上移/下移调），顺序存在 presets\order.txt 里。</summary>
            Custom
        }

        private const string OrderFile = "order.txt";

        /// <summary>列出全部预设名（默认按名称排序）。</summary>
        public static List<string> List() => List(PresetSort.Name);

        /// <summary>按指定方式列出预设名。</summary>
        public static List<string> List(PresetSort sort)
        {
            var names = new List<string>();
            try
            {
                foreach (var f in System.IO.Directory.GetFiles(Folder, "*" + Ext))
                {
                    string n = Path.GetFileNameWithoutExtension(f);
                    if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
                }
            }
            catch { return names; }

            switch (sort)
            {
                case PresetSort.Time:
                    // 按修改时间倒序；读不到时间就退回按名称
                    try
                    {
                        names.Sort((a, b) => LastWrite(b).CompareTo(LastWrite(a)));
                        return names;
                    }
                    catch { break; }

                case PresetSort.Custom:
                    return ApplyOrder(names, LoadOrder());
            }

            names.Sort(StringComparer.CurrentCultureIgnoreCase.Compare);
            return names;
        }

        private static DateTime LastWrite(string name)
        {
            try { return File.GetLastWriteTimeUtc(PathFor(name)); }
            catch { return DateTime.MinValue; }
        }

        /// <summary>按 order.txt 里记录的顺序排；没记录过的名字按名称接在后面。</summary>
        private static List<string> ApplyOrder(List<string> names, List<string> order)
        {
            var result = new List<string>();
            var left = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            foreach (string n in order)
            {
                string hit = null;
                foreach (string c in names)
                    if (string.Equals(c, n, StringComparison.OrdinalIgnoreCase)) { hit = c; break; }
                if (hit != null) { result.Add(hit); left.Remove(hit); }
            }
            var rest = new List<string>(left);
            rest.Sort(StringComparer.CurrentCultureIgnoreCase.Compare);
            result.AddRange(rest);
            return result;
        }

        /// <summary>读取自定义顺序（每行一个预设名）。</summary>
        public static List<string> LoadOrder()
        {
            var list = new List<string>();
            try
            {
                string p = Path.Combine(Folder, OrderFile);
                if (!File.Exists(p)) return list;
                foreach (string line in File.ReadAllLines(p))
                {
                    string n = line == null ? "" : line.Trim();
                    if (n.Length > 0 && !list.Contains(n)) list.Add(n);
                }
            }
            catch { /* 顺序文件坏了就当没有 */ }
            return list;
        }

        /// <summary>写入自定义顺序。</summary>
        public static void SaveOrder(IEnumerable<string> names)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Folder);
                File.WriteAllLines(Path.Combine(Folder, OrderFile), new List<string>(names));
            }
            catch { /* 存不了顺序不影响使用 */ }
        }

        /// <summary>
        /// 预设改名（文件内容原样保留，只是换文件名）。
        /// 目标名已存在、或名字非法时返回 false，调用方负责提示。
        /// </summary>
        public static bool Rename(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName)) return false;
            string clean = Sanitize(newName);
            if (string.IsNullOrEmpty(clean) || clean == Sanitize(oldName)) return false;

            string src = PathFor(oldName), dst = PathFor(clean);
            if (!File.Exists(src) || File.Exists(dst)) return false;

            try
            {
                File.Move(src, dst);

                // 顺序文件里同步改名，否则改名后自定义顺序会对不上
                var order = LoadOrder();
                bool touched = false;
                for (int i = 0; i < order.Count; i++)
                    if (string.Equals(order[i], oldName, StringComparison.OrdinalIgnoreCase)) { order[i] = clean; touched = true; }
                if (touched) SaveOrder(order);
                return true;
            }
            catch { return false; }
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
