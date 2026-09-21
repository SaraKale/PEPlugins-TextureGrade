using System.Collections.Generic;
using System.ComponentModel;

namespace TextureGrade.Models
{
    /// <summary>
    /// 所有调色滑块的值。用「索引器 + 字典」实现，方便动态增减滑块而无需改类。
    /// 对称滑块以 0 为「无变化」；阈值分界默认位于 0–255 的中点。
    /// </summary>
    public class GradeSettings : INotifyPropertyChanged
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();
        public const double DefaultThresholdLevel = 127.5;
        public static double DefaultValue(string key) => key == "ThresholdLevel" ? DefaultThresholdLevel : 0;

        public double Get(string key, double fallback) => _values.TryGetValue(key, out var value) ? value : fallback;

        public Dictionary<string, double> Capture() => new Dictionary<string, double>(_values);

        // Replace as one transaction: controls and background renders never see a half-written palette.
        public void Replace(IDictionary<string, double> values)
        {
            var copy = new Dictionary<string, double>(values);
            _values.Clear();
            foreach (var pair in copy)
                if (!double.IsNaN(pair.Value) && !double.IsInfinity(pair.Value)) _values[pair.Key] = pair.Value;
            OnPropertyChanged("Item[]");
        }

        public GradeSettings Copy()
        {
            var result = new GradeSettings();
            result.Replace(_values);
            return result;
        }

        public double this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : DefaultValue(key);
            set
            {
                if (_values.TryGetValue(key, out var cur) && cur == value) return;
                _values[key] = value;
                OnPropertyChanged("Item[]");
            }
        }

        public void Reset()
        {
            _values.Clear();
            // 通知所有索引器绑定（滑块）刷新回各自默认值
            OnPropertyChanged("Item[]");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
