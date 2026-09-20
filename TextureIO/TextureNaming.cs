using System;
using System.Collections.Generic;
using System.IO;

namespace TextureGrade.TextureIO
{
    /// <summary>
    /// 新贴图文件名规则。
    /// 例：body_d.png -> 另存为 body_d_new.png；临时预览 body_d_tgpreview003.png
    /// 临时文件带计数，确保每次推送路径都不同，强制 PMXEditor 重新加载贴图（避免按路径缓存）。
    /// </summary>
    public static class TextureNaming
    {
        /// <summary>永久另存：body_d.png -> body_d_new.png（同目录，PNG 无损）</summary>
        public static string NewSavePath(string originalAbsPath)
            => ChangeName(originalAbsPath, "_new");
        public static string NewSavePath(string originalAbsPath, string extension)
            => Path.ChangeExtension(NewSavePath(originalAbsPath), extension);

        /// <summary>临时预览（推送到 3D 模型用），counter 保证文件名唯一</summary>
        public static string PreviewPath(string originalAbsPath, int counter)
            => ChangeName(originalAbsPath, $"_tgpreview{counter:D3}");

        private static string ChangeName(string absPath, string suffix)
        {
            var dir = Path.GetDirectoryName(absPath);
            var name = Path.GetFileNameWithoutExtension(absPath);
            return Path.Combine(dir, name + suffix + ".png");
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
    }
}
