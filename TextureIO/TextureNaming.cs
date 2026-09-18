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
        /// 原贴图为相对路径则保持相对（可移植），否则回退为绝对路径。
        /// </summary>
        public static string ToMaterialTexPath(string newAbsPath, string pmxDir, string originalRelPath)
        {
            if (string.IsNullOrEmpty(originalRelPath) || Path.IsPathRooted(originalRelPath))
                return newAbsPath;

            var origDir = Path.GetDirectoryName(originalRelPath) ?? "";
            var newName = Path.GetFileName(newAbsPath); // body_d_new.png
            return Path.Combine(origDir, newName).Replace('\\', '/');
        }
    }
}
