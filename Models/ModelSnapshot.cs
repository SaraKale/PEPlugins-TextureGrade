using System.Collections.Generic;

namespace TextureGrade.Models
{
    /// <summary>
    /// 单个材质的信息（从 IPXPmx 读取后缓存，避免反复访问 PEPlugin）。
    /// </summary>
    public class MaterialInfo
    {
        public int Index { get; }
        public string Name { get; }
        /// <summary>PMX 中存储的贴图路径（相对或绝对）</summary>
        public string TexRelPath { get; }
        /// <summary>解析后的绝对路径，无贴图时为 null</summary>
        public string TexAbsPath { get; }
        public bool HasTexture { get; }

        /// <summary>材质的 Diffuse 颜色（0-255），用于列表左侧色块。</summary>
        public byte DiffuseR { get; }
        public byte DiffuseG { get; }
        public byte DiffuseB { get; }

        /// <summary>列表显示用：带 2 位 ID 序号，例如 "03 · 体"。</summary>
        public string Display => $"{Index:D2} · {Name}";

        /// <summary>是否有贴图（方便 UI 直接绑定/判断）。</summary>
        public bool TexExists => HasTexture;

        public MaterialInfo(int index, string name, string texRelPath, string texAbsPath, bool hasTexture,
                            byte diffuseR = 210, byte diffuseG = 210, byte diffuseB = 210)
        {
            Index = index;
            Name = name;
            TexRelPath = texRelPath;
            TexAbsPath = texAbsPath;
            HasTexture = hasTexture;
            DiffuseR = diffuseR;
            DiffuseG = diffuseG;
            DiffuseB = diffuseB;
        }

        public override string ToString() => $"{Index:D2} · {Name}";
    }

    /// <summary>
    /// 一次 GetCurrentState() 的快照。
    /// </summary>
    public class ModelSnapshot
    {
        public string PmxFilePath { get; }
        public string PmxDirectory { get; }
        public IReadOnlyList<MaterialInfo> Materials { get; }

        public ModelSnapshot(string pmxFilePath, string pmxDir, IReadOnlyList<MaterialInfo> materials)
        {
            PmxFilePath = pmxFilePath;
            PmxDirectory = pmxDir;
            Materials = materials;
        }
    }
}
