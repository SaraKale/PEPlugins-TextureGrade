using System.Collections.Generic;
using TextureGrade.Models;

namespace TextureGrade.Bridge
{
    /// <summary>
    /// 封装所有对 PEPlugin / PMXEditor 的访问，UI 不直接碰 PEPlugin 类型。
    /// </summary>
    public interface IPMDBridge
    {
        /// <summary>重新读取当前 PMX 的材质与贴图</summary>
        ModelSnapshot LoadCurrent();

        /// <summary>把调好的贴图推送到 3D 模型（写临时文件 + 改 Material.Tex + 刷新视图）</summary>
        void ApplyPreview(int materialIndex, string absTexturePath);

        /// <summary>永久另存：改 Material.Tex 指向 _new 文件</summary>
        void ApplySaved(int materialIndex, string absTexturePath);

        /// <summary>还原 Material.Tex 到进入插件前的原始值，并清理临时文件</summary>
        void Revert(int materialIndex);

        /// <summary>取某材质的 UV 三角面（左栏线框叠加用）</summary>
        IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3)> GetUVTriangles(int materialIndex);

        /// <summary>插件关闭时调用：还原未保存的修改并删除全部临时文件</summary>
        void Cleanup();

        string PmxDirectory { get; }
    }
}
