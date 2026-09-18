using System;
using System.Collections.Generic;
using System.IO;
using PEPlugin;
using PEPlugin.Pmd;
using PEPlugin.Pmx;
using PEPlugin.SDX;
using TextureGrade.Models;
using TextureGrade.TextureIO;

namespace TextureGrade.Bridge
{
    /// <summary>
    /// PEPlugin 实现。集中所有 host.Connector.* 调用，便于隔离与测试。
    /// 关键 API（来自 UVEditor 反编译源码）：
    ///   host.Connector.Pmx.GetCurrentState() -> IPXPmx
    ///   host.Connector.Pmx.Update(ipxmx, PmxUpdateObject.Material, materialIndex)
    ///   host.Connector.Form.UpdateList(PEPlugin.Pmd.UpdateObject.Material)
    ///   host.Connector.View.PMDView.UpdateModel() / UpdateView()
    ///   pmx.Material[i].Tex (贴图路径), pmx.Vertex[i].UV (V2: .U/.V), pmx.Material[i].Faces
    /// </summary>
    public class PmxBridge : IPMDBridge
    {
        private readonly IPEPluginHost _host;
        private IPXPmx _lastPmx;
        private readonly Dictionary<int, string> _originals = new Dictionary<int, string>();
        private readonly Dictionary<int, string> _lastTemp = new Dictionary<int, string>();
        private string _pmxDir;

        public PmxBridge(IPEPluginHost host)
        {
            _host = host;
        }

        public string PmxDirectory => _pmxDir;

        public ModelSnapshot LoadCurrent()
        {
            var pmx = _host.Connector.Pmx.GetCurrentState();
            _lastPmx = pmx;
            // 未打开模型时 FilePath 为 null/空，Path.GetDirectoryName 会抛 ArgumentException
            _pmxDir = string.IsNullOrEmpty(pmx?.FilePath) ? null : Path.GetDirectoryName(pmx.FilePath);

            var list = new List<MaterialInfo>();
            if (pmx == null || pmx.Material == null) return new ModelSnapshot(pmx?.FilePath ?? "", _pmxDir, list);
            for (int i = 0; i < pmx.Material.Count; i++)
            {
                var m = pmx.Material[i];
                string rel = m.Tex ?? "";
                string abs = string.IsNullOrEmpty(rel) ? null : Resolve(rel, _pmxDir);
                bool has = !string.IsNullOrEmpty(abs) && File.Exists(abs);
                if (!_originals.ContainsKey(i)) _originals[i] = rel; // 仅在首次记录真正的原始值

                byte dr, dg, db;
                ReadDiffuse(m.Diffuse, out dr, out dg, out db);
                list.Add(new MaterialInfo(i, m.Name, rel, abs, has, dr, dg, db));
            }
            return new ModelSnapshot(pmx.FilePath, _pmxDir, list);
        }

        public void ApplyPreview(int materialIndex, string absTexturePath)
        {
            // 删除上一次同材质的临时文件，避免堆积
            if (_lastTemp.TryGetValue(materialIndex, out var prev) && prev != absTexturePath && File.Exists(prev))
                TryDelete(prev);
            _lastTemp[materialIndex] = absTexturePath;

            var pmx = _host.Connector.Pmx.GetCurrentState();
            pmx.Material[materialIndex].Tex = absTexturePath; // 临时文件用绝对路径，保证 PMXEditor 能解析
            _host.Connector.Pmx.Update(pmx, PmxUpdateObject.Material, materialIndex);
            _host.Connector.Form.UpdateList(UpdateObject.Material);
            _host.Connector.View.PMDView.UpdateModel();
            _host.Connector.View.PMDView.UpdateView();
        }

        public void ApplySaved(int materialIndex, string absTexturePath)
        {
            var pmx = _host.Connector.Pmx.GetCurrentState();
            string rel = TextureNaming.ToMaterialTexPath(absTexturePath, _pmxDir, GetOriginal(materialIndex));
            pmx.Material[materialIndex].Tex = rel;
            _lastTemp.Remove(materialIndex); // 已正式保存，不再视为临时
            _host.Connector.Pmx.Update(pmx, PmxUpdateObject.Material, materialIndex);
            _host.Connector.Form.UpdateList(UpdateObject.Material);
            _host.Connector.View.PMDView.UpdateModel();
            _host.Connector.View.PMDView.UpdateView();
        }

        public void Revert(int materialIndex)
        {
            if (_lastTemp.TryGetValue(materialIndex, out var tmp) && File.Exists(tmp)) TryDelete(tmp);
            _lastTemp.Remove(materialIndex);

            var pmx = _host.Connector.Pmx.GetCurrentState();
            pmx.Material[materialIndex].Tex = GetOriginal(materialIndex);
            _host.Connector.Pmx.Update(pmx, PmxUpdateObject.Material, materialIndex);
            _host.Connector.Form.UpdateList(UpdateObject.Material);
            _host.Connector.View.PMDView.UpdateModel();
            _host.Connector.View.PMDView.UpdateView();
        }

        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3)> GetUVTriangles(int materialIndex)
        {
            var result = new List<(float, float, float, float, float, float)>();
            if (_lastPmx == null) return result;
            var mat = _lastPmx.Material[materialIndex];
            foreach (var f in mat.Faces)
            {
                var a = f.Vertex1.UV;
                var b = f.Vertex2.UV;
                var c = f.Vertex3.UV;
                result.Add((a.U, a.V, b.U, b.V, c.U, c.V));
            }
            return result;
        }

        public IReadOnlyList<(float u1, float v1, float u2, float v2, float u3, float v3, int i1, int i2, int i3)> GetUVTrianglesWithIndices(int materialIndex)
        {
            var result = new List<(float, float, float, float, float, float, int, int, int)>();
            if (_lastPmx == null) return result;

            // IPXVertex -> 全局顶点索引（UVEditor 的 SetFaces 同款做法：按引用建字典）
            var dict = new Dictionary<IPXVertex, int>();
            for (int i = 0; i < _lastPmx.Vertex.Count; i++)
                dict[_lastPmx.Vertex[i]] = i;

            foreach (var f in _lastPmx.Material[materialIndex].Faces)
            {
                var a = f.Vertex1.UV;
                var b = f.Vertex2.UV;
                var c = f.Vertex3.UV;
                result.Add((a.U, a.V, b.U, b.V, c.U, c.V,
                            dict[f.Vertex1], dict[f.Vertex2], dict[f.Vertex3]));
            }
            return result;
        }

        public int[] GetPmxSelectedVertices()
        {
            // 仿 UVEditor MyPMX.ReceiveSelected：取 3D 视图当前选中顶点索引
            return new List<int>(_host.Connector.View.PMDView.GetSelectedVertexIndices()).ToArray();
        }

        public void SetPmxSelectedVertices(int[] vertexIndices)
        {
            // 仿 UVEditor MyPMX.SendSelected
            _host.Connector.View.PMDView.SetSelectedVertexIndices(vertexIndices);
            _host.Connector.View.PMDView.UpdateView();
        }

        public void Cleanup()
        {
            foreach (var kv in _lastTemp)
                if (File.Exists(kv.Value)) TryDelete(kv.Value);
            _lastTemp.Clear();
        }

        private string GetOriginal(int materialIndex)
            => _originals.TryGetValue(materialIndex, out var o) ? o : "";

        /// <summary>
        /// 用反射读取 Diffuse 颜色，避免依赖 Color4 的具体类型/字段命名
        /// （只要它的字段叫 R/Red/X、G/Green/Y、B/Blue/Z 之一即可）；
        /// 浮点型按 0-1 归一化处理，整型按 0-255 处理。读不到就返回中性灰。
        /// </summary>
        private static void ReadDiffuse(object diffuse, out byte r, out byte g, out byte b)
        {
            r = g = b = 210;
            if (diffuse == null) return;
            try
            {
                var t = diffuse.GetType();
                r = Channel(t, diffuse, "R", "Red", "X");
                g = Channel(t, diffuse, "G", "Green", "Y");
                b = Channel(t, diffuse, "B", "Blue", "Z");
            }
            catch { /* 读不到就用默认灰 */ }
        }

        private static byte Channel(Type t, object o, params string[] names)
        {
            foreach (var n in names)
            {
                object v = null;
                var p = t.GetProperty(n);
                if (p != null) v = p.GetValue(o, null);
                else { var f = t.GetField(n); if (f != null) v = f.GetValue(o); }
                if (v == null) continue;

                bool isInt = v is byte || v is sbyte || v is short || v is int || v is long;
                double d = Convert.ToDouble(v);
                d = isInt ? d : d * 255.0;            // 浮点按 0-1 归一化
                if (d < 0) d = 0; if (d > 255) d = 255;
                return (byte)Math.Round(d);
            }
            return 210;
        }

        private static string Resolve(string rel, string pmxDir)
        {
            if (Path.IsPathRooted(rel)) return rel;
            return Path.Combine(pmxDir, rel);
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* 被占用则忽略，下次清理 */ }
        }
    }
}
