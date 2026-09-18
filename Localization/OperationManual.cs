using System;
using System.IO;
using System.Text;
using TextureGrade.Models;

namespace TextureGrade.Localization
{
    /// <summary>
    /// 「帮助 — 操作说明」的文本内容管理。
    ///
    /// 约定：插件同级目录下建一个 <c>data</c> 文件夹，里面按语言放四份说明：
    ///   data\TextureGrade_Operation_EN.txt   English
    ///   data\TextureGrade_Operation_SC.txt   简体中文
    ///   data\TextureGrade_Operation_TC.txt   繁體中文
    ///   data\TextureGrade_Operation_JP.txt   日本語
    ///
    /// 插件启动时调用一次 EnsureAll()：缺哪份就按内置默认文本生成哪份（已存在的不覆盖，
    /// 用户改过的内容不会被冲掉）。之后帮助窗口只读当前语言对应的那份，切换语言即时改读。
    /// 用户随时可以直接编辑这些 txt，点窗口里的「重新载入」即可看到最新内容，不用重新编译。
    ///
    /// data 目录不可写（例如装在 Program Files）时自动回退到 %APPDATA%\TextureGrade\data。
    /// </summary>
    public static class OperationManual
    {
        public const string BaseName = "TextureGrade_Operation";
        public const string Ext = ".txt";
        public const string FolderName = "data";

        /// <summary>语言后缀：EN / SC / TC / JP（用户指定的命名，勿改大小写习惯）。</summary>
        public static string Suffix(Lang l)
        {
            switch (l)
            {
                case Lang.ZhCn: return "SC";
                case Lang.ZhTw: return "TC";
                case Lang.Ja: return "JP";
                default: return "EN";
            }
        }

        public static Lang[] All => new[] { Lang.En, Lang.ZhCn, Lang.ZhTw, Lang.Ja };

        private static string _folder;

        /// <summary>说明文件所在目录（插件目录\data，不可写时回退 AppData）。</summary>
        public static string Folder
        {
            get
            {
                if (_folder == null) _folder = ResolveFolder();
                return _folder;
            }
        }

        /// <summary>某语言对应的说明文件路径。</summary>
        public static string PathFor(Lang l)
        {
            return Path.Combine(Folder, BaseName + "_" + Suffix(l) + Ext);
        }

        /// <summary>启动时调用：四种语言各保证有一份，缺的用内置默认文本生成。</summary>
        public static void EnsureAll()
        {
            foreach (var l in All)
            {
                string path = PathFor(l);
                try
                {
                    if (File.Exists(path)) continue;
                    File.WriteAllText(path, SeedText(l), new UTF8Encoding(true));
                }
                catch
                {
                    // 写不进去就算了：帮助窗口会退化成直接显示内置文本
                }
            }
        }

        /// <summary>
        /// 当前语言该读的那份：data 里按后缀找；缺失则就地补一份；
        /// data 完全不可用时退回插件目录根部的通用 TextureGrade_Operation.txt。
        /// </summary>
        public static string Resolve(Lang l)
        {
            try
            {
                string p = PathFor(l);
                if (File.Exists(p)) return p;
                File.WriteAllText(p, SeedText(l), new UTF8Encoding(true));
                return p;
            }
            catch
            {
                return Path.Combine(AppPaths.Dir, BaseName + Ext);
            }
        }

        /// <summary>生成某语言的初始文本：简体中文优先沿用旧的根目录说明（保住用户改过的内容）。</summary>
        private static string SeedText(Lang l)
        {
            if (l == Lang.ZhCn)
            {
                try
                {
                    string legacy = Path.Combine(AppPaths.Dir, BaseName + Ext);
                    if (File.Exists(legacy)) return File.ReadAllText(legacy, Encoding.UTF8);
                }
                catch { /* 读不到就用内置文本 */ }
            }
            return DefaultManual.Get(l);
        }

        private static string ResolveFolder()
        {
            try
            {
                string dir = Path.Combine(AppPaths.Dir, FolderName);
                if (AppPaths.Writable(dir)) return dir;
            }
            catch { /* 落到回退分支 */ }

            try
            {
                string fb = Path.Combine(AppPaths.FallbackDir, FolderName);
                Directory.CreateDirectory(fb);
                return fb;
            }
            catch { return AppPaths.Dir; }
        }
    }
}
