using System.Collections.Generic;
using System.IO;

namespace TextureGrade.Models
{
    /// <summary>
    /// 界面状态的持久化：直方图开关 + 预览工具三组的折叠状态。
    /// 存插件目录 ui.json（目录不可写时 AppPaths 会自动回退到 %APPDATA%\TextureGrade），
    /// 结构就是扁平的 "string -> 0/1"，直接复用 MiniJson，保持零依赖。
    ///
    /// 缺省值统一为 0：直方图关闭、工具组收起 —— 首次启动就是最省画布空间的样子，
    /// 之后用户改成什么样就一直保持什么样。
    /// </summary>
    public sealed class UiState
    {
        public const string KeyHistogram = "Histogram";
        public const string KeyToolsView = "ToolsView";
        public const string KeyToolsSelection = "ToolsSelection";
        public const string KeyToolsVertices = "ToolsVertices";

        private readonly string _file;
        private readonly Dictionary<string, double> _data = new Dictionary<string, double>();
        private bool _dirty;

        public UiState(string fileName = "ui.json")
        {
            _file = AppPaths.PathIn(fileName);
            Load();
        }

        /// <summary>读一个开关；键不存在（首次启动）时为 0 = 关闭/收起。</summary>
        public double Get(string key)
        {
            double v;
            return _data.TryGetValue(key, out v) ? v : 0;
        }

        public bool IsOn(string key) => Get(key) != 0;

        /// <summary>写一个开关；返回 true 表示值真的变了（调用方据此决定要不要落盘）。</summary>
        public bool Set(string key, double value)
        {
            double old;
            if (_data.TryGetValue(key, out old) && old == value) return false;
            _data[key] = value;
            _dirty = true;
            return true;
        }

        public bool Set(string key, bool value) => Set(key, value ? 1d : 0d);

        /// <summary>落盘。插件目录不可写时静默失败 —— 记不住状态不该影响正常使用。</summary>
        public void Save()
        {
            if (!_dirty) return;
            _dirty = false;
            try { File.WriteAllText(_file, MiniJson.WriteObject(_data)); }
            catch { /* 只读目录：放弃持久化 */ }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_file)) return;
                foreach (var kv in MiniJson.ReadObject(File.ReadAllText(_file))) _data[kv.Key] = kv.Value;
            }
            catch { /* 文件损坏就当首次启动 */ }
        }
    }
}
