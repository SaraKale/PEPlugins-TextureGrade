using System;
using System.Collections.Generic;
using System.IO;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 新贴图文件名规则。
    /// 例：body_d.png -> 另存为 body_d_new.png；临时预览 body_d_tg0920...001.png
    /// 临时文件全局唯一（进程标记 + 时间戳 + 计数），确保每次推送的路径都不一样。
    /// 这一点很关键：PMXEditor 大概率按「贴图路径」缓存纹理，路径重复时即使文件内容变了
    /// 画面也不会更新（表现就是"点了刷新模型没反应，手动换个路径就好了"）。
    /// </summary>
    public static class TextureNaming
    {
        /// <summary>本进程唯一标记，避免关掉插件再打开时临时文件名撞车。</summary>
        private static readonly string SessionTag =
            DateTime.Now.ToString("MMddHHmmss") + Guid.NewGuid().ToString("N").Substring(0, 4);

        /// <summary>永久另存：body_d.png -> body_d_new.png（同目录，PNG 无损）</summary>
        public static string NewSavePath(string originalAbsPath)
<<<<<<< HEAD
            => NewSavePath(originalAbsPath, ".png");
=======
            => ChangeName(originalAbsPath, "_new");
        public static string NewSavePath(string originalAbsPath, string extension)
            => Path.ChangeExtension(NewSavePath(originalAbsPath), extension);
>>>>>>> pr-1

        /// <summary>永久另存，后缀可扩展名（另存对话框选了 JPG/TGA/DDS 时用）。</summary>
        public static string NewSavePath(string originalAbsPath, string ext)
            => ChangeName(originalAbsPath, "_new", ext);

        /// <summary>临时预览（推送到 3D 模型用），进程标记 + counter 保证文件名唯一</summary>
        public static string PreviewPath(string originalAbsPath, int counter)
            => ChangeName(originalAbsPath, $"_tg{SessionTag}_{counter:D3}");

        /// <summary>
        /// 原贴图所在目录不可写时的兜底路径（系统临时目录）。
        /// 命名规则与 PreviewPath 完全一致，这样同一套清理逻辑也能照顾到它。
        /// </summary>
        public static string FallbackPreviewPath(string originalAbsPath, int counter)
        {
            var dir = Path.Combine(Path.GetTempPath(), "TextureGrade");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir,
                Path.GetFileNameWithoutExtension(originalAbsPath) + $"_tg{SessionTag}_{counter:D3}.png");
        }

        private static string ChangeName(string absPath, string suffix, string ext = ".png")
        {
            var dir = Path.GetDirectoryName(absPath);
            var name = Path.GetFileNameWithoutExtension(absPath);
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            return Path.Combine(dir, name + suffix + ext);
        }

        /// <summary>
        /// 计算写入 Material.Tex 的路径：
        /// 根据当前 PMX 所在目录计算相对路径，不沿用原贴图的绝对路径形式。
        /// 模型未保存或跨盘 / 跨共享目录时保留绝对路径。
        /// </summary>
        public static string ToMaterialTexPath(string newAbsPath, string pmxDir)
        {
            string absolute = Path.GetFullPath(newAbsPath);
            if (string.IsNullOrEmpty(pmxDir)) return absolute;
            string directory = Path.GetFullPath(pmxDir);
            if (!string.Equals(Path.GetPathRoot(absolute), Path.GetPathRoot(directory), StringComparison.OrdinalIgnoreCase))
                return absolute;
            // Work on filesystem segments, not URI escaping: '#' and literal '%20' are valid filenames.
            var separators = new[] { '\\', '/' };
            var from = directory.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            var to = absolute.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            int common = 0;
            while (common < from.Length && common < to.Length && string.Equals(from[common], to[common], StringComparison.OrdinalIgnoreCase)) common++;
            var parts = new List<string>();
            for (int i = common; i < from.Length; i++) parts.Add("..");
            for (int i = common; i < to.Length; i++) parts.Add(to[i]);
            return string.Join("/", parts);
        }

        /// <summary>Load both legacy absolute paths and model-relative paths without relying on the process working directory.</summary>
        public static string ResolveMaterialTexPath(string texturePath, string pmxDir)
        {
            if (string.IsNullOrWhiteSpace(texturePath)) return null;
            try
            {
                if (Path.IsPathRooted(texturePath)) return Path.GetFullPath(texturePath);
                if (string.IsNullOrEmpty(pmxDir)) return null;
                return Path.GetFullPath(Path.Combine(pmxDir, texturePath));
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }

        /// <summary>
        /// 清掉同一张原贴图名下的历史临时预览文件（上次运行插件留下的那批）。
        /// keep 里是当前仍在使用的临时文件，一律不删 —— 同一张贴图被多个材质共用时才不会误删。
        /// </summary>
        public static void DeleteStalePreviews(string currentTempPath, ICollection<string> keep)
        {
            try
            {
                var dir = Path.GetDirectoryName(currentTempPath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                var name = Path.GetFileNameWithoutExtension(currentTempPath);
                int cut = name.LastIndexOf("_tg", StringComparison.Ordinal);
                if (cut < 0) return;
                string prefix = name.Substring(0, cut + 3);   // "原贴图名_tg"

                foreach (var f in Directory.GetFiles(dir, prefix + "*.png"))
                {
                    if (string.Equals(f, currentTempPath, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!IsOurPreviewName(Path.GetFileNameWithoutExtension(f), prefix)) continue;

                    bool inUse = false;
                    if (keep != null)
                        foreach (var k in keep)
                            if (k != null && string.Equals(k, f, StringComparison.OrdinalIgnoreCase)) { inUse = true; break; }
                    if (inUse) continue;

                    try { File.Delete(f); } catch { /* 被占用就下次再说 */ }
                }
            }
            catch { /* 清理失败不影响主流程 */ }
        }

        /// <summary>
        /// 判断文件名是不是本插件自己生成的临时预览：「原贴图名_tg」+ 14 位进程标记 + "_" + 3 位计数。
        /// 严格匹配是为了不误删用户恰好叫「xxx_tg…」的贴图。
        /// </summary>
        private static bool IsOurPreviewName(string fileName, string prefix)
        {
            // 14 位标记 + 1 个下划线 + 3 位计数
            if (fileName.Length != prefix.Length + 18) return false;
            if (fileName[prefix.Length + 14] != '_') return false;
            for (int i = prefix.Length + 15; i < fileName.Length; i++)
                if (fileName[i] < '0' || fileName[i] > '9') return false;
            return true;
        }
    }
}
