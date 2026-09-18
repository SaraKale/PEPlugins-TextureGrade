using System.Collections.Generic;
using System.ComponentModel;

namespace TextureGrade.Models
{
    /// <summary>
    /// 所有调色滑块的值。用「索引器 + 字典」实现，方便动态增减滑块而无需改类。
    /// 约定：所有对称滑块以 0 为「无变化」，Reset() 全部归零。
    /// </summary>
    public class GradeSettings : INotifyPropertyChanged
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();

        public double this[string key]
        {
            get => _values.TryGetValue(key, out var v) ? v : 0.0;
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
            // 通知所有索引器绑定（滑块）刷新回 0
            OnPropertyChanged("Item[]");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
