using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureGrade.Bridge;
using TextureGrade.ColorGrade;
using TextureGrade.Localization;
using TextureGrade.Models;
using TextureGrade.TextureIO;
using Shapes = System.Windows.Shapes;

namespace TextureGrade.WpfUI
{
    /// <summary>
    /// 材质列表的一行（贴图缩略图 + ID·名称 + 已修改徽章）。
    /// 缩略图在后台线程生成后回填（Thumbnail 变化时通知 UI）。
    /// </summary>
    public sealed class MaterialRow : INotifyPropertyChanged
    {
        public MaterialInfo Info { get; }
        public string Display => Info.Display;
        public Brush Swatch { get; }
        public Brush SwatchBorder => Brushes.Transparent;

        /// <summary>贴图缩略图；生成完成前为 null（此时只显示漫反射底色）。</summary>
        private ImageSource _thumbnail;
        public ImageSource Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (ReferenceEquals(_thumbnail, value)) return;
                _thumbnail = value;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        private bool _modified;
        public bool Modified
        {
            get => _modified;
            set
            {
                if (_modified == value) return;
                _modified = value;
                OnPropertyChanged(nameof(Modified));
                OnPropertyChanged(nameof(BadgeVisibility));
            }
        }
        public Visibility BadgeVisibility => _modified ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>「已修改」徽章文字（跟随界面语言）。</summary>
        public string BadgeText => Localization.L.T("Badge.Modified");

        public MaterialRow(MaterialInfo info)
        {
            Info = info;
            Swatch = new SolidColorBrush(Color.FromRgb(info.DiffuseR, info.DiffuseG, info.DiffuseB));
            Swatch.Freeze();
        }

        /// <summary>语言切换后让徽章文字重新取一次。</summary>
        public void RefreshTexts() => OnPropertyChanged(nameof(BadgeText));

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>
    /// 插件主界面（WPF）。
    /// 左 = 可缩放/平移的贴图预览 + UV 线框 + UV 选面/框选；右 = 材质列表 + Camera Raw 折叠组。
    /// 滑块变动 -> 从原图重跑调色管线 -> 写 WriteableBitmap（内存，非破坏）。
    /// 「刷新模型」才把结果写临时文件并推给 PMXEditor。
    /// </summary>
    public partial class MainPanel : UserControl
    {
        private readonly IPMDBridge _bridge;
        private readonly GradeSettings Settings = new GradeSettings();
        private readonly GradePipeline _pipeline = new GradePipeline();
        private readonly System.Windows.Threading.DispatcherTimer _debounce;

        private readonly ListBox _materialList;
        private readonly List<MaterialRow> _rows = new List<MaterialRow>();
        private TextBlock _status;
        private ListBox _presetList;
        private TextBox _presetNameBox;

        /// <summary>
        /// 界面文案的「重新取词」动作集合。
        /// 每处需要在换语言时刷新的文字都登记一个 lambda，切换语言时全部跑一遍即可 ——
        /// 不必重建整个右栏（重建会丢掉折叠状态和滚动位置）。
        /// </summary>
        private readonly List<Action> _locActions = new List<Action>();

        private bool _showUV = true;
        private bool _showVertices = true;
        private bool _showGrid;

        private byte[] _originalBytes;
        private int _w, _h;
        private int _currentMatIndex = -1;
        private string _currentAbsOriginal;
        private int _previewCounter;
        private WriteableBitmap _wb;

        // 「对比原图」：_wbOriginal 是原图位图；_compareMode 为 true 时预览显示原图
        private WriteableBitmap _wbOriginal;
        private byte[] _originalBgra;
        private bool _compareMode;

        // 直方图：256 级 RGB 计数（原图 / 当前调色结果各一份）
        private int[] _histOriginal, _histGraded;
        private bool _showHistogram = true;

        // 分材质保存的调色参数：材质索引 -> 参数快照
        private readonly Dictionary<int, Dictionary<string, double>> _matParams
            = new Dictionary<int, Dictionary<string, double>>();

        // 连通 UV 块（把共享 UV 顶点的三角面聚成块，用于「像 Blender 按 L 选一整块」）
        private int[] _triIsland = new int[0];
        private int _islandCount;
        private int _lastPickedTri = -1;

        // 材质缩略图：按「贴图绝对路径」缓存（同一张贴图被多个材质共用时只解一次）+ 后台生成任务
        private readonly Dictionary<string, ImageSource> _thumbCache = new Dictionary<string, ImageSource>();
        private int _thumbGeneration;
        private const int ThumbSize = 40;

        // Lab 取色环（勾选「锁定亮度」时，只换色相/彩度，像素自身的 L* 一点不动）
        private LabWheel _labWheel;
        private TextBlock _labInfo;
        private CheckBox _labLockBox;
        private bool _syncLab;                 // 防止「色环 -> 设置 -> 色环」自激
        private double _refL = 55;             // 画面平均亮度 L*：锁定亮度时色环的绘制锚点

        // 视图变换（缩放/平移）
        private double _zoom = 1, _panX, _panY;
        private bool _autoFit = true;
        private const double MinZoom = 0.02, MaxZoom = 40;

        // 交互模式与拖拽状态
        private enum ToolMode { Select, Pan }
        private ToolMode _mode = ToolMode.Select;
        private bool _panning, _banding, _bandMoved;
        private int _clickCount;
        private Point _panLast, _bandStart;

        // UV 三角面与选区
        private List<UvTri> _tris = new List<UvTri>();
        private readonly HashSet<int> _selectedTris = new HashSet<int>();
        private bool[] _maskCache;

        // UV 几何缓存（未选中的面 + 顶点记号），只在换材质/改选区时重建，
        // 这样缩放时只改线宽，不必重新构建上千个三角面。
        private StreamGeometry _geoAllCache, _geoDotsCache;
        private double _geoDotsZoom;
        private bool _geoDirty = true;

        /// <summary>选区/材质变化后让 UV 几何缓存失效（下次绘制时重建）。</summary>
        private void InvalidateGeo()
        {
            _geoDirty = true;
            _geoDotsCache = null;
            _geoDotsZoom = 0;
        }

        // UV 显示颜色
        private Color _uvLine = Color.FromArgb(195, 0, 118, 214);
        private Color _uvVertex = Color.FromArgb(255, 30, 30, 30);
        private const int DotTriangleLimit = 8000;

        // 撤销 / 重做（针对调色参数）
        private readonly List<Dictionary<string, double>> _undo = new List<Dictionary<string, double>>();
        private readonly List<Dictionary<string, double>> _redo = new List<Dictionary<string, double>>();
        private Dictionary<string, double> _prevSnapshot = new Dictionary<string, double>();
        private bool _suppressHistory, _sessionActive;
        private const int MaxHistory = 60;

        // 「已修改」徽章
        private readonly HashSet<int> _pushedMats = new HashSet<int>();

        public MainPanel(IPMDBridge bridge)
        {
            InitializeComponent();
            _bridge = bridge;
            _materialList = MaterialList;

            _debounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _debounce.Tick += (s, e) => { _debounce.Stop(); RunGradeAsync(); };
            Settings.PropertyChanged += (s, e) =>
            {
                MarkCurrentModified();
                if (!_syncLab) SyncLabFromSettings();   // 撤销/预设/重置后把色环拉回同步
                _debounce.Stop();
                _debounce.Start();
            };

            Loaded += (s, e) => { if (_autoFit) FitView(); UpdateHint(); DrawHistogram(); };
            ViewRoot.SizeChanged += (s, e) => { if (_autoFit) FitView(); };
            HistogramCanvas.SizeChanged += (s, e) => DrawHistogram();

            ViewRoot.MouseWheel += ViewRoot_MouseWheel;
            ViewRoot.MouseLeftButtonDown += ViewRoot_MouseLeftButtonDown;
            ViewRoot.MouseLeftButtonUp += ViewRoot_MouseLeftButtonUp;
            ViewRoot.MouseRightButtonDown += ViewRoot_MouseRightButtonDown;
            ViewRoot.MouseRightButtonUp += ViewRoot_MouseRightButtonUp;
            ViewRoot.MouseMove += ViewRoot_MouseMove;
            ViewRoot.MouseLeave += (s, e) => { if (!_panning && !_banding) SetReadout(null); };

            BtnReRead.Click += (s, e) => ReRead();
            BtnRefresh.Click += (s, e) => RefreshModel();
            BtnSave.Click += (s, e) => SaveNew();
            BtnRevert.Click += (s, e) => Revert();
            BtnReset.Click += (s, e) => ResetParams();
            BtnUndo.Click += (s, e) => Undo();
            BtnRedo.Click += (s, e) => Redo();

            BtnFit.Click += (s, e) => FitView();
            BtnActual.Click += (s, e) => ActualSize();
            BtnGrid.Click += (s, e) => SetGridVisible(!_showGrid);
            BtnMode.Click += (s, e) => SetPanMode(_mode != ToolMode.Pan);
            BtnIsland.Click += (s, e) => SelectConnectedIsland();
            BtnClearSel.Click += (s, e) => ClearUVSelection();
            // 对比原图：用「切换」而不是「按住」，避免按住时与画布操作互相干扰
            BtnCompare.Click += (s, e) => SetCompareOriginal(!_compareMode);

            BuildRightPanel();
            RefreshPresetList();
            ApplyLanguage();      // 按当前语言填一遍全部文案（XAML 里的中文只是兜底默认值）
            ReRead();
            _prevSnapshot = Snapshot();
            UpdateUndoButtons();
        }

        // ================= 多语言 =================
        /// <summary>登记一段「取词 -> 显示」的动作，供切换语言时重跑。</summary>
        private void LocText(TextBlock tb, string key)
        {
            tb.Text = L.T(key);
            _locActions.Add(() => tb.Text = L.T(key));
        }

        /// <summary>按钮等 ContentControl 的文案。</summary>
        private void LocContent(ContentControl cc, string key)
        {
            cc.Content = L.T(key);
            _locActions.Add(() => cc.Content = L.T(key));
        }

        /// <summary>折叠组标题（Expander 是 HeaderedContentControl）。</summary>
        private void LocHeader(HeaderedContentControl hc, string key)
        {
            hc.Header = L.T(key);
            _locActions.Add(() => hc.Header = L.T(key));
        }

        /// <summary>带占位符的文案（每次取词都要重新算参数，例如预设文件夹路径）。</summary>
        private void LocFormat(TextBlock tb, string key, Func<object[]> args)
        {
            tb.Text = L.F(key, args());
            _locActions.Add(() => tb.Text = L.F(key, args()));
        }

        /// <summary>提示气泡。</summary>
        private void LocTip(FrameworkElement fe, string key)
        {
            fe.ToolTip = L.T(key);
            _locActions.Add(() => fe.ToolTip = L.T(key));
        }

        /// <summary>
        /// 切换语言后刷新全部界面文案：跑一遍登记的取词动作 + 更新 XAML 里的固定文字。
        /// 不重建面板，所以折叠状态、滚动位置、当前选中的材质都不会丢。
        /// </summary>
        public void ApplyLanguage()
        {
            foreach (var act in _locActions) act();

            if (TxtPreviewTitle != null) TxtPreviewTitle.Text = L.T("Title.Preview");
            if (TxtMaterialTitle != null) TxtMaterialTitle.Text = L.T("Title.Materials");
            if (TxtAdjustTitle != null) TxtAdjustTitle.Text = L.T("Title.Adjust");
            if (TxtHistLabel != null) TxtHistLabel.Text = L.T("Hist.Label");

            // XAML 里写死的按钮文字（中文只是设计时的默认值）
            BtnReRead.Content = L.T("Btn.ReRead");
            BtnRefresh.Content = L.T("Btn.RefreshModel");
            BtnSave.Content = L.T("Btn.SaveNew");
            BtnRevert.Content = L.T("Btn.Revert");
            BtnReset.Content = L.T("Btn.ResetParams");
            BtnUndo.Content = L.T("Btn.Undo");
            BtnRedo.Content = L.T("Btn.Redo");
            BtnFit.Content = L.T("Btn.Fit");
            BtnActual.Content = L.T("Btn.Actual");
            BtnGrid.Content = L.T("Btn.Grid");
            BtnIsland.Content = L.T("Btn.Island");
            BtnClearSel.Content = L.T("Btn.ClearSel");
            BtnCompare.Content = L.T("Btn.Compare");

            SetPanMode(_mode == ToolMode.Pan);   // 模式按钮上是「下一个模式」的名字，也要跟着换
            SetReadout(null);
            UpdateHint();

            foreach (var r in _rows) r.RefreshTexts();   // 材质行的「已修改」徽章

            Status(L.T("St.LangChanged"));
        }

        // ================= 对外公开 API（供菜单栏调用） =================
        public bool ShowUV => _showUV;
        public bool ShowVertices => _showVertices;
        public bool GridVisible => _showGrid;
        public bool PanMode => _mode == ToolMode.Pan;
        public int SelectedTriangleCount => _selectedTris.Count;

        public void SetShowUV(bool value)
        {
            _showUV = value;
            DrawUV(_currentMatIndex);
            UpdateHint();
        }

        public void SetShowVertices(bool value)
        {
            _showVertices = value;
            InvalidateGeo();
            DrawUV(_currentMatIndex);
        }

        public void SetGridVisible(bool value)
        {
            _showGrid = value;
            BtnGrid.Tag = value ? "on" : null;
            DrawUV(_currentMatIndex);
        }

        /// <summary>直方图是否显示（视图菜单）。</summary>
        public bool HistogramVisible => _showHistogram;

        public void SetHistogramVisible(bool value)
        {
            _showHistogram = value;
            if (HistogramBox != null) HistogramBox.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            if (value) DrawHistogram();
        }

        /// <summary>是否正在显示原图（对比模式）。</summary>
        public bool ComparingOriginal => _compareMode;

        /// <summary>对比状态变化通知（供菜单勾选状态与按钮保持一致）。</summary>
        public event Action CompareChanged;

        /// <summary>切换「对比原图」：显示调色结果的原始贴图（非破坏，仅切换预览源）。</summary>
        public void SetCompareOriginal(bool value)
        {
            if (_compareMode == value) return;
            _compareMode = value;
            BtnCompare.Tag = value ? "on" : null;
            ApplyPreviewSource();
            DrawHistogram();
            Status(value ? L.T("St.CompareOn") : L.T("St.CompareOff"));
            if (CompareChanged != null) CompareChanged();
        }

        private void ApplyPreviewSource()
        {
            if (PreviewImage == null) return;
            PreviewImage.Source = _compareMode ? (ImageSource)_wbOriginal : _wb;
        }

        /// <summary>设置 UV 线框/顶点颜色（菜单「修改UV布局颜色」）。</summary>
        public void SetUvColor(Color c)
        {
            _uvLine = Color.FromArgb(195, c.R, c.G, c.B);
            byte vr = (byte)(c.R * 0.35), vg = (byte)(c.G * 0.35), vb = (byte)(c.B * 0.35);
            _uvVertex = Color.FromArgb(255, vr, vg, vb);
            DrawUV(_currentMatIndex);
            Status(L.F("St.UvColorFmt", c.R, c.G, c.B));
        }

        public void SetPanMode(bool pan)
        {
            _mode = pan ? ToolMode.Pan : ToolMode.Select;
            // 按钮上写的是「切过去会是哪个模式」，所以这里取反显示
            BtnMode.Content = pan ? L.T("Btn.ModeToSelect") : L.T("Btn.ModeToPan");
            BtnMode.Tag = pan ? "on" : null;
            UpdateHint();
        }

        public void ClearUVSelection()
        {
            if (_selectedTris.Count == 0) { Status(L.T("St.NoSelection")); return; }
            _selectedTris.Clear();
            _lastPickedTri = -1;
            AfterSelectionChanged(L.T("St.Cleared"));
        }

        public void SelectAllUv()
        {
            if (_tris.Count == 0) { Status(L.T("St.NoTris")); return; }
            _selectedTris.Clear();
            for (int i = 0; i < _tris.Count; i++) _selectedTris.Add(i);
            AfterSelectionChanged(L.F("St.SelectedAll", _tris.Count));
        }

        public void FitView()
        {
            double vw = ViewRoot.ActualWidth, vh = ViewRoot.ActualHeight;
            if (_w <= 0 || _h <= 0 || vw <= 1 || vh <= 1) return;
            _zoom = Math.Min(vw / _w, vh / _h);
            if (_zoom <= 0 || double.IsInfinity(_zoom)) _zoom = 1;
            _autoFit = true;
            CenterScene(vw, vh);
            ApplyTransform();
            DrawUV(_currentMatIndex);
            UpdateHint();
        }

        public void ActualSize()
        {
            if (_w <= 0 || _h <= 0) return;
            _zoom = 1;
            _autoFit = false;
            CenterScene(ViewRoot.ActualWidth, ViewRoot.ActualHeight);
            ApplyTransform();
            DrawUV(_currentMatIndex);
            UpdateHint();
        }

        public void ResetView() => FitView();
        public void ZoomIn() => ZoomBy(1.25);
        public void ZoomOut() => ZoomBy(1 / 1.25);

        public void Undo() => StepHistory(_undo, _redo, true);
        public void Redo() => StepHistory(_redo, _undo, false);

        // ================= 右栏构建 =================
        private void BuildRightPanel()
        {
            // 第一组：预设 / 收藏（不是滑块组，单独构建）
            RightPanel.Children.Add(BuildPresetGroup());

            foreach (var g in _groups)
            {
                var exp = new Expander
                {
                    IsExpanded = g.Expanded,
                    HeaderTemplate = (DataTemplate)FindResource("GroupHeader")
                };
                LocHeader(exp, g.TitleKey);
                var stack = new StackPanel();
                string lastSection = null;
                foreach (var sl in g.Sliders)
                {
                    if (sl.Hidden) continue;   // 只登记、不渲染（由色环等控件驱动）
                    if (!string.IsNullOrEmpty(sl.Section) && sl.Section != lastSection)
                    {
                        lastSection = sl.Section;
                        stack.Children.Add(MakeSectionHeader(sl.Section));
                    }
                    stack.Children.Add(MakeSliderRow(sl));
                }
                exp.Content = stack;
                RightPanel.Children.Add(exp);

                // 「Lab 取色环」紧跟「色彩」组：它属于色彩工具，但控件形态与滑块组不同，单独构建。
                if (g.TitleKey == "Grp.Color") RightPanel.Children.Add(BuildLabGroup());
            }

            _status = new TextBlock
            {
                Margin = new Thickness(4, 14, 4, 6),
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x7E, 0x88)),
                FontSize = 13,
                LineHeight = 20
            };
            RightPanel.Children.Add(_status);
        }

        // ================= 预设 / 收藏 =================
        /// <summary>
        /// 右栏顶部的「预设 / 收藏」折叠组。
        /// 预设列表用 ListBox 而不是 ComboBox —— ComboBox 的弹层在 ElementHost 里会点不到（之前踩过）。
        /// 双击列表项即可套用。
        /// </summary>
        private UIElement BuildPresetGroup()
        {
            var root = new StackPanel();

            // 保存行：名称输入 + 保存按钮
            var saveRow = new Grid { Margin = new Thickness(0, 2, 0, 8) };
            saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBox = new TextBox
            {
                FontSize = 14,
                Padding = new Thickness(8, 6, 8, 6),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _presetNameBox = nameBox;
            LocTip(nameBox, "Preset.NameTip");
            Grid.SetColumn(nameBox, 0);

            var saveBtn = MakeSmallButtonLoc("Preset.Save", SavePresetFromBox);
            saveBtn.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(saveBtn, 1);

            saveRow.Children.Add(nameBox);
            saveRow.Children.Add(saveBtn);
            root.Children.Add(saveRow);

            // 预设列表
            _presetList = new ListBox
            {
                Height = 132,
                FontSize = 14,
                ItemContainerStyle = (Style)FindResource("MaterialItem")
            };
            // 附加属性不能在对象初始化器里赋值，必须用 SetStatic 方法
            ScrollViewer.SetHorizontalScrollBarVisibility(_presetList, ScrollBarVisibility.Disabled);
            _presetList.MouseDoubleClick += (s, e) => ApplySelectedPreset();
            root.Children.Add(_presetList);

            // 操作按钮
            var btnRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            btnRow.Children.Add(MakeSmallButtonLoc("Preset.Apply", ApplySelectedPreset));
            btnRow.Children.Add(MakeSmallButtonLoc("Preset.Delete", DeleteSelectedPreset));
            btnRow.Children.Add(MakeSmallButtonLoc("Preset.Refresh", RefreshPresetList));
            btnRow.Children.Add(MakeSmallButtonLoc("Preset.OpenFolder", OpenPresetFolder));
            root.Children.Add(btnRow);

            var hint = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0x9A)),
                Margin = new Thickness(2, 8, 2, 0),
                LineHeight = 18
            };
            LocFormat(hint, "Preset.Hint", () => new object[] { PresetStore.Folder });
            root.Children.Add(hint);

            var presetExp = new Expander
            {
                IsExpanded = false,
                HeaderTemplate = (DataTemplate)FindResource("GroupHeader"),
                Content = root
            };
            LocHeader(presetExp, "Grp.Preset");
            return presetExp;
        }

        private Button MakeSmallButton(string text, Action act)
        {
            var b = new Button
            {
                Content = text,
                Style = (Style)FindResource("TgButton"),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 13
            };
            b.Click += (s, e) => act();
            return b;
        }

        /// <summary>同 MakeSmallButton，但文字按 key 取词并登记换语言时刷新。</summary>
        private Button MakeSmallButtonLoc(string key, Action act)
        {
            var b = MakeSmallButton(L.T(key), act);
            _locActions.Add(() => b.Content = L.T(key));
            return b;
        }

        private void RefreshPresetList()
        {
            if (_presetList == null) return;
            var keep = _presetList.SelectedItem as string;
            var names = PresetStore.List();
            _presetList.ItemsSource = null;
            _presetList.ItemsSource = names;
            if (!string.IsNullOrEmpty(keep) && names.Contains(keep)) _presetList.SelectedItem = keep;
        }

        private void SavePresetFromBox()
        {
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }
            if (!HasNonDefaultParams()) { Status(L.T("Preset.AllDefault")); return; }

            string name = (_presetNameBox == null ? "" : _presetNameBox.Text).Trim();
            bool autoNamed = false;
            if (string.IsNullOrEmpty(name)) { name = SuggestPresetName(); autoNamed = true; }

            if (PresetStore.Exists(name))
            {
                var r = System.Windows.Forms.MessageBox.Show(
                    L.F("Preset.OverwriteBody", name), L.T("Preset.OverwriteTitle"),
                    System.Windows.Forms.MessageBoxButtons.YesNo,
                    System.Windows.Forms.MessageBoxIcon.Question);
                if (r != System.Windows.Forms.DialogResult.Yes) return;
            }

            PresetStore.Save(name, Snapshot());
            if (_presetNameBox != null) _presetNameBox.Text = "";
            RefreshPresetList();
            if (_presetList != null) _presetList.SelectedItem = name;
            Status(L.F(autoNamed ? "Preset.SavedAuto" : "Preset.Saved", name));
        }

        private string SuggestPresetName()
        {
            string matName = "";
            foreach (var r in _rows)
                if (r.Info.Index == _currentMatIndex) { matName = r.Info.Name; break; }
            return string.IsNullOrEmpty(matName) ? L.T("Preset.DefaultName") : matName + L.T("Preset.Suffix");
        }

        private void ApplySelectedPreset()
        {
            var name = _presetList == null ? null : _presetList.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) { Status(L.T("Preset.SelectFirst")); return; }
            var values = PresetStore.Load(name);
            if (values.Count == 0) { Status(L.F("Preset.LoadFail", name)); return; }
            ApplyParams(values);
            Status(L.F("Preset.Applied", name, values.Count));
        }

        private void DeleteSelectedPreset()
        {
            var name = _presetList == null ? null : _presetList.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) { Status(L.T("Preset.SelectFirst")); return; }
            var r = System.Windows.Forms.MessageBox.Show(
                L.F("Preset.DeleteBody", name), L.T("Preset.DeleteTitle"),
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Warning);
            if (r != System.Windows.Forms.DialogResult.Yes) return;
            PresetStore.Delete(name);
            RefreshPresetList();
            Status(L.F("Preset.Deleted", name));
        }

        public void OpenPresetFolder()
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + PresetStore.Folder + "\"");
            }
            catch (Exception ex) { Status(L.F("Preset.OpenFail", ex.Message)); }
        }

        /// <summary>折叠组内的小节标题（如 HSL 的「红 Red」）。text 是词条键。</summary>
        private UIElement MakeSectionHeader(string key)
        {
            var tb = new TextBlock
            {
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0x54, 0x60))
            };
            LocText(tb, key);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xF8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE2, 0xE6, 0xEB)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 5, 4, 5),
                Margin = new Thickness(0, 10, 0, 2),
                Child = tb
            };
        }

        /// <summary>
        /// 滑块标签取哪个词条：
        /// HSL 分通道的 24 个键（HslRedH / HslBlueS …）共享「色相 / 饱和度 / 明度」三个词，
        /// 按末位字母归并，省掉 24 条重复词条。
        /// </summary>
        private static string LabelKeyFor(SliderDef def)
        {
            var k = def.Key;
            if (k != null && k.Length > 4 && k.StartsWith("Hsl", StringComparison.Ordinal))
            {
                char last = k[k.Length - 1];
                if (last == 'H') return "Hsl.Hue";
                if (last == 'S') return "Hsl.Sat";
                if (last == 'L') return "Hsl.Lum";
            }
            return k;
        }

        private UIElement MakeSliderRow(SliderDef def)
        {
            var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

            var label = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 15,
                Margin = new Thickness(4, 0, 14, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x2A, 0x31, 0x3A))
            };
            LocText(label, LabelKeyFor(def));
            Grid.SetColumn(label, 0);

            var slider = new Slider
            {
                Minimum = def.Min,
                Maximum = def.Max,
                VerticalAlignment = VerticalAlignment.Center,
                MinHeight = 26,
                Margin = new Thickness(0, 0, 6, 0)
            };
            slider.SetBinding(Slider.ValueProperty, new Binding($"[{def.Key}]") { Source = Settings, Mode = BindingMode.TwoWay });
            slider.PreviewMouseLeftButtonDown += (s, e) => BeginHistorySession();
            slider.PreviewMouseLeftButtonUp += (s, e) => CommitHistorySession();
            slider.LostMouseCapture += (s, e) => CommitHistorySession();
            slider.ValueChanged += (s, e) => OnSliderValueChanged();
            Grid.SetColumn(slider, 1);

            var val = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontSize = 14,
                Margin = new Thickness(14, 0, 12, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x3A, 0x42, 0x4C))
            };
            val.SetBinding(TextBlock.TextProperty, new Binding($"[{def.Key}]") { Source = Settings, StringFormat = "{0:F0}" });
            Grid.SetColumn(val, 2);

            grid.Children.Add(label);
            grid.Children.Add(slider);
            grid.Children.Add(val);
            return grid;
        }

        // ================= Lab 取色环 =================
        /// <summary>
        /// 「Lab 取色环」折叠组。
        ///
        /// 需求来源：要在传统色环上取色的手感，但「亮度不变」。
        /// 做法是把三维（色相 / 彩度 / 亮度）里的亮度约束掉：
        ///   · 外环 = 色相（等亮度色相环，环上每个角度都在同一个 L* 上）
        ///   · 内圈 = 该色相在同一个 L* 下的彩度线（灰 -> 最高彩度）
        /// 于是 SV 平面只剩一条径向的线，选色只影响色相与彩度，像素自身的 L* 一动不动。
        /// 取消勾选「锁定亮度」则连亮度也一起朝目标靠拢。
        /// </summary>
        private UIElement BuildLabGroup()
        {
            var root = new StackPanel();

            _labWheel = new LabWheel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 8)
            };
            LocTip(_labWheel, "Lab.WheelTip");
            _labWheel.Changed += OnLabWheelChanged;
            root.Children.Add(_labWheel);

            _labInfo = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x64, 0x72))
            };
            root.Children.Add(_labInfo);

            _labLockBox = new CheckBox
            {
                FontSize = 13.5,
                Margin = new Thickness(2, 0, 0, 4),
                IsChecked = true
            };
            _locActions.Add(() => _labLockBox.Content = L.T("Lab.Lock"));
            _labLockBox.Content = L.T("Lab.Lock");
            LocTip(_labLockBox, "Lab.LockTip");
            _labLockBox.Checked += (s, e) => SetLabUnlock(false);
            _labLockBox.Unchecked += (s, e) => SetLabUnlock(true);
            root.Children.Add(_labLockBox);

            // 目标亮度 L*：锁定亮度时它就是「画面参考亮度」，可由「取画面亮度」按钮刷新
            root.Children.Add(MakeSliderRow(new SliderDef { Label = "LabTargetL", Min = 0, Max = 100, Key = "LabTargetL" }));
            // 强度
            root.Children.Add(MakeSliderRow(new SliderDef { Label = "LabAmount", Min = 0, Max = 100, Key = "LabAmount" }));

            var btnRow = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            btnRow.Children.Add(MakeSmallButtonLoc("Lab.PickL", PickReferenceL));
            btnRow.Children.Add(MakeSmallButtonLoc("Lab.PickHue", PickReferenceHue));
            btnRow.Children.Add(MakeSmallButtonLoc("Lab.Reset", ResetLab));
            root.Children.Add(btnRow);

            var note = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x90, 0x9A)),
                Margin = new Thickness(2, 8, 2, 0),
                LineHeight = 18
            };
            LocText(note, "Lab.Note");
            root.Children.Add(note);

            var labExp = new Expander
            {
                IsExpanded = false,
                HeaderTemplate = (DataTemplate)FindResource("GroupHeader"),
                Content = root
            };
            LocHeader(labExp, "Grp.Lab");
            return labExp;
        }

        /// <summary>色环被拖动：把选中的 LCh 写回成目标 a*/b*。</summary>
        private void OnLabWheelChanged()
        {
            if (_syncLab || _labWheel == null) return;

            double a, b;
            LabColor.LchToLab(_labWheel.DrawL, _labWheel.Chroma, _labWheel.HueDeg, out a, out b);

            _syncLab = true;
            try
            {
                Settings["LabTargetA"] = a;
                Settings["LabTargetB"] = b;
                // 第一次取色时自动给个强度，否则用户选了颜色却发现画面没反应
                if (Settings["LabAmount"] <= 0) Settings["LabAmount"] = 60;
            }
            finally { _syncLab = false; }

            UpdateLabInfo();
        }

        /// <summary>勾选框：unlock=true 表示解除亮度锁定（亮度也一起变）。</summary>
        private void SetLabUnlock(bool unlock)
        {
            if (_syncLab) return;   // 由 SyncLabFromSettings 程序化改勾选状态时，不当作"用户操作"

            _syncLab = true;
            try { Settings["LabUnlock"] = unlock ? 1 : 0; }
            finally { _syncLab = false; }

            SyncLabFromSettings();
            Status(unlock ? L.T("Lab.LockOff") : L.T("Lab.LockOn"));
        }

        /// <summary>从设置回填色环（撤销 / 预设 / 换材质 / 改锁定状态时调用）。</summary>
        private void SyncLabFromSettings()
        {
            if (_labWheel == null || _syncLab) return;

            _syncLab = true;
            try
            {
                bool locked = Settings["LabUnlock"] < 0.5;
                double tl = Settings["LabTargetL"];
                if (tl <= 0) { tl = _refL; Settings["LabTargetL"] = _refL; }   // 未设置 -> 用画面参考亮度
                double drawL = locked ? _refL : tl;
                _labWheel.SetState(drawL, locked, Settings["LabTargetA"], Settings["LabTargetB"]);
                if (_labLockBox != null) _labLockBox.IsChecked = locked;
            }
            finally { _syncLab = false; }

            UpdateLabInfo();
        }

        private void UpdateLabInfo()
        {
            if (_labInfo == null || _labWheel == null) return;

            byte r, g, b;
            _labWheel.GetRgb(out r, out g, out b);
            bool locked = Settings["LabUnlock"] < 0.5;
            double a = Settings["LabTargetA"], bb = Settings["LabTargetB"];
            double c = Math.Sqrt(a * a + bb * bb);

            _labInfo.Text = L.F("Lab.InfoFmt", r, g, b, c, _labWheel.HueDeg,
                                locked ? L.T("Lab.Locked") : L.T("Lab.Unlocked"),
                                Settings["LabAmount"]);
        }

        /// <summary>取当前选区（无选区则整张图）的平均亮度 L* 作为色环锚点。</summary>
        private void PickReferenceL()
        {
            // 局部变量名不能叫 L —— 会遮住多语言类 L
            double luma, a, b;
            if (!AverageLab(out luma, out a, out b)) { Status(L.T("St.NeedTextureForLab")); return; }

            _refL = luma;
            _syncLab = true;
            try { Settings["LabTargetL"] = luma; }
            finally { _syncLab = false; }

            SyncLabFromSettings();
            Status(L.F("Lab.PickedLFmt", ScopeName(), luma));
        }

        /// <summary>「选区」还是「整张图」——Lab 取色的统计范围说明。</summary>
        private string ScopeName() => L.T(_selectedTris.Count > 0 ? "Lab.ScopeSelection" : "Lab.ScopeWhole");

        /// <summary>取当前选区的平均色相/彩度作为取色环的起点（便于在原色上做偏移）。</summary>
        private void PickReferenceHue()
        {
            double luma, a, b;
            if (!AverageLab(out luma, out a, out b)) { Status(L.T("St.NeedTextureForLab")); return; }

            _syncLab = true;
            try
            {
                Settings["LabTargetA"] = a;
                Settings["LabTargetB"] = b;
                if (Settings["LabAmount"] <= 0) Settings["LabAmount"] = 60;
            }
            finally { _syncLab = false; }

            SyncLabFromSettings();
            Status(L.F("Lab.PickedHueFmt", ScopeName(), a, b));
        }

        private void ResetLab()
        {
            _syncLab = true;
            try
            {
                Settings["LabAmount"] = 0;
                Settings["LabTargetA"] = 0;
                Settings["LabTargetB"] = 0;
                Settings["LabUnlock"] = 0;
                Settings["LabTargetL"] = _refL;
            }
            finally { _syncLab = false; }

            SyncLabFromSettings();
            Status(L.T("Lab.ResetDone"));
        }

        /// <summary>
        /// 统计当前选区（无选区则整张图）的平均 Lab。按步长抽样，最多约 2 万点 ——
        /// 够稳定，又不会因为 4096² 的贴图卡住界面。
        /// </summary>
        private bool AverageLab(out double avgL, out double avgA, out double avgB)
        {
            avgL = avgA = avgB = 0;
            if (_originalBytes == null || _w <= 0 || _h <= 0) return false;

            bool[] mask = null;
            if (_selectedTris.Count > 0) mask = _maskCache ?? (_maskCache = BuildMask());

            int w = _w, h = _h;
            int step = Math.Max(1, (int)Math.Sqrt((double)w * h / 20000.0));

            double sl = 0, sa = 0, sb = 0;
            long n = 0;
            for (int y = 0; y < h; y += step)
            {
                for (int x = 0; x < w; x += step)
                {
                    int p = y * w + x;
                    if (mask != null && (p >= mask.Length || !mask[p])) continue;
                    int o = p * 4;
                    if (_originalBytes[o + 3] == 0) continue;

                    double L, a, b;
                    LabColor.RgbToLab(_originalBytes[o], _originalBytes[o + 1], _originalBytes[o + 2], out L, out a, out b);
                    sl += L; sa += a; sb += b; n++;
                }
            }

            if (n == 0) return false;
            avgL = sl / n; avgA = sa / n; avgB = sb / n;
            return true;
        }

        /// <summary>贴图载入后刷新「画面参考亮度」（锁定亮度时色环按它绘制）。</summary>
        private void ComputeReferenceL()
        {
            double L, a, b;
            if (!AverageLab(out L, out a, out b)) { _refL = 55; return; }
            _refL = L;
        }

        // ================= 撤销 / 重做 =================
        private Dictionary<string, double> Snapshot()
        {
            var d = new Dictionary<string, double>();
            foreach (var g in _groups)
                foreach (var s in g.Sliders)
                    d[s.Key] = Settings[s.Key];
            return d;
        }

        private void Restore(Dictionary<string, double> snap)
        {
            _suppressHistory = true;
            _syncLab = true;
            try
            {
                foreach (var g in _groups)
                    foreach (var s in g.Sliders)
                        Settings[s.Key] = snap.TryGetValue(s.Key, out var v) ? v : 0.0;
            }
            finally { _suppressHistory = false; _syncLab = false; }
            SyncLabFromSettings();
            RunGradeAsync();
        }

        private void BeginHistorySession()
        {
            if (_suppressHistory) return;
            _sessionActive = true;
        }

        private void CommitHistorySession()
        {
            if (!_sessionActive) return;
            _sessionActive = false;
            var now = Snapshot();
            if (SameSnapshot(now, _prevSnapshot)) return;
            PushHistory(_prevSnapshot);
            _prevSnapshot = now;
        }

        private void OnSliderValueChanged()
        {
            if (_suppressHistory || _sessionActive) return;
            var now = Snapshot();
            if (SameSnapshot(now, _prevSnapshot)) return;
            PushHistory(_prevSnapshot);
            _prevSnapshot = now;
            UpdateUndoButtons();
        }

        private static bool SameSnapshot(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
                if (!b.TryGetValue(kv.Key, out var v) || Math.Abs(v - kv.Value) > 1e-9) return false;
            return true;
        }

        private void PushHistory(Dictionary<string, double> snap)
        {
            _undo.Add(new Dictionary<string, double>(snap));
            if (_undo.Count > MaxHistory) _undo.RemoveAt(0);
            _redo.Clear();
            UpdateUndoButtons();
        }

        private void StepHistory(List<Dictionary<string, double>> from, List<Dictionary<string, double>> to, bool isUndo)
        {
            if (from.Count == 0) { Status(L.T(isUndo ? "St.NothingUndo" : "St.NothingRedo")); return; }
            var target = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(new Dictionary<string, double>(_prevSnapshot));
            _prevSnapshot = new Dictionary<string, double>(target);
            Restore(target);
            UpdateUndoButtons();
            Status(L.F(isUndo ? "St.UndoFmt" : "St.RedoFmt", _undo.Count, _redo.Count));
        }

        private void UpdateUndoButtons()
        {
            if (BtnUndo != null) BtnUndo.IsEnabled = _undo.Count > 0;
            if (BtnRedo != null) BtnRedo.IsEnabled = _redo.Count > 0;
        }

        private void ResetHistory()
        {
            _undo.Clear();
            _redo.Clear();
            _prevSnapshot = Snapshot();
            UpdateUndoButtons();
        }

        // ================= 数据流程 =================
        public void ReRead()
        {
            var snap = _bridge.LoadCurrent();

            // 重新读取后尽量选回原来那个材质
            int keep = _currentMatIndex;

            _rows.Clear();
            foreach (var m in snap.Materials)
            {
                var row = new MaterialRow(m) { Modified = IsMaterialModified(m.Index) };
                if (!string.IsNullOrEmpty(m.TexAbsPath) &&
                    _thumbCache.TryGetValue(m.TexAbsPath, out var cached))
                    row.Thumbnail = cached;
                _rows.Add(row);
            }

            _materialList.ItemsSource = null;
            _materialList.ItemsSource = _rows;

            if (_rows.Count == 0) { Status(L.T("St.NoMaterialInModel")); ClearImage(); return; }

            int idx = 0;
            if (keep >= 0)
                for (int i = 0; i < _rows.Count; i++)
                    if (_rows[i].Info.Index == keep) { idx = i; break; }
            _materialList.SelectedIndex = idx;   // 从 -1 变过来必然触发 SelectionChanged -> OnMaterialSelected

            StartThumbnailGeneration();
        }

        /// <summary>该材质是否被改过（有非默认参数，或已推送到模型）。</summary>
        private bool IsMaterialModified(int matIndex)
        {
            if (_pushedMats.Contains(matIndex)) return true;
            return _matParams.TryGetValue(matIndex, out var d) && !IsAllZero(d);
        }

        private bool IsAllZero(Dictionary<string, double> snap)
        {
            foreach (var kv in snap)
                if (Math.Abs(kv.Value) > 1e-9) return false;
            return true;
        }

        /// <summary>
        /// 后台串行生成材质缩略图。
        /// 用代次号 _thumbGeneration 做失效控制：重新读取/换模型时递增，旧任务发现代次不符会自动放弃，
        /// 不会把上一份列表的缩略图回填到新列表上。
        /// 串行（一次一张）是为了避免几十张大贴图同时解码把磁盘/CPU 打满。
        /// </summary>
        private void StartThumbnailGeneration()
        {
            int gen = ++_thumbGeneration;

            var targets = new List<string>();          // 贴图绝对路径，去重（同一贴图被多个材质共用只解一次）
            foreach (var r in _rows)
            {
                var p = r.Info.TexAbsPath;
                if (string.IsNullOrEmpty(p) || !r.Info.HasTexture) continue;
                if (_thumbCache.ContainsKey(p) || targets.Contains(p)) continue;
                targets.Add(p);
            }
            if (targets.Count == 0) return;

            System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var p in targets)
                {
                    if (gen != _thumbGeneration) return;
                    var src = ThumbnailFactory.Create(p, ThumbSize);
                    if (src == null) continue;
                    if (gen != _thumbGeneration) return;
                    Dispatcher.Invoke(() =>
                    {
                        if (gen != _thumbGeneration) return;
                        _thumbCache[p] = src;
                        foreach (var r in _rows)
                            if (r.Info.TexAbsPath == p) r.Thumbnail = src;
                    });
                }
            });
        }

        private void MaterialList_SelectionChanged(object sender, SelectionChangedEventArgs e) => OnMaterialSelected();

        private void OnMaterialSelected()
        {
            if (!(_materialList.SelectedItem is MaterialRow row)) return;

            // 切换前先把「上一个材质」的滑块值存起来（每个材质独立保存参数）
            SaveCurrentParams();

            var m = row.Info;
            _currentMatIndex = m.Index;
            _currentAbsOriginal = m.TexAbsPath;
            _selectedTris.Clear();
            _lastPickedTri = -1;
            _maskCache = null;
            _tris = new List<UvTri>();
            foreach (var t in _bridge.GetUVTriangles(m.Index))
                _tris.Add(new UvTri(t.u1, t.v1, t.u2, t.v2, t.u3, t.v3));
            InvalidateGeo();
            BuildIslands();

            if (m.HasTexture && m.TexAbsPath != null)
            {
                try
                {
                    var (rgba, w, h) = TextureLoader.Load(m.TexAbsPath);
                    _originalBytes = rgba; _w = w; _h = h;
                    LayoutScene();
                    PrepareBitmaps(rgba, w, h);   // 建立 原图/调色 两张位图 + 原图直方图
                    ComputeReferenceL();          // 画面参考亮度：Lab 取色环锁定亮度时按它绘制
                    LoadParamsFor(m.Index);       // 恢复该材质自己保存的参数
                    ResetHistory();
                    FitView();
                    RunGradeAsync();
                    Status(L.F("St.MatInfoFmt", m.Display, w, h, _tris.Count, _islandCount));
                }
                catch (Exception ex)
                {
                    _originalBytes = null; PreviewImage.Source = null; _w = _h = 0;
                    Status(L.F("St.TextureLoadFail", ex.Message));
                }
            }
            else
            {
                _originalBytes = null; _w = _h = 0; PreviewImage.Source = null;
                UvCanvas.Children.Clear(); GridCanvas.Children.Clear();
                HistogramCanvas?.Children.Clear();
                LoadParamsFor(m.Index);
                ResetHistory();
                Status(m.HasTexture ? L.F("St.TextureMissing", m.TexAbsPath ?? "") : L.T("St.NoTexture"));
            }
            UpdateHint();
        }

        // ================= 分材质保存调色参数 =================
        /// <summary>把当前滑块值存到当前材质名下（全 0 则从表里删掉，保持表干净）。</summary>
        private void SaveCurrentParams()
        {
            if (_currentMatIndex < 0) return;
            var snap = Snapshot();
            if (IsAllZero(snap)) _matParams.Remove(_currentMatIndex);
            else _matParams[_currentMatIndex] = snap;
        }

        /// <summary>把某材质之前保存的参数恢复到滑块上（没有记录则全部归零）。</summary>
        private void LoadParamsFor(int matIndex)
        {
            _suppressHistory = true;
            _syncLab = true;
            try
            {
                Settings.Reset();
                if (_matParams.TryGetValue(matIndex, out var d))
                    foreach (var kv in d) Settings[kv.Key] = kv.Value;
            }
            finally { _suppressHistory = false; _syncLab = false; }
            SyncLabFromSettings();
        }

        /// <summary>当前材质是否保存过非默认参数。</summary>
        private bool CurrentHasParams()
            => _currentMatIndex >= 0 && _matParams.TryGetValue(_currentMatIndex, out var d) && !IsAllZero(d);

        /// <summary>把一份参数快照应用到当前材质（预设用）。</summary>
        private void ApplyParams(Dictionary<string, double> values)
        {
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }
            PushHistory(_prevSnapshot);
            _suppressHistory = true;
            _syncLab = true;
            try
            {
                Settings.Reset();
                if (values != null)
                    foreach (var kv in values)
                        if (HasSlider(kv.Key)) Settings[kv.Key] = kv.Value;
            }
            finally { _suppressHistory = false; _syncLab = false; }
            _prevSnapshot = Snapshot();
            UpdateUndoButtons();
            SyncLabFromSettings();
            RunGradeAsync();
            MarkCurrentModified();
        }

        private bool HasSlider(string key)
        {
            foreach (var g in _groups)
                foreach (var s in g.Sliders)
                    if (s.Key == key) return true;
            return false;
        }

        private void ClearImage()
        {
            PreviewImage.Source = null; UvCanvas.Children.Clear(); GridCanvas.Children.Clear();
            HistogramCanvas?.Children.Clear();
            _originalBytes = null; _originalBgra = null; _wb = null; _wbOriginal = null;
            _histOriginal = _histGraded = null;
            _w = _h = 0; _tris.Clear(); _selectedTris.Clear(); _maskCache = null;
            _triIsland = new int[0]; _islandCount = 0; _lastPickedTri = -1;
            InvalidateGeo();
            UpdateHint();
        }

        // ================= 原图 / 直方图 数据准备 =================
        /// <summary>
        /// 建立两张位图（原图 + 调色结果，初值与原图相同）并统计原图直方图。
        /// 「对比原图」只是切换 PreviewImage.Source 指向哪一张，因此切换是零成本的。
        /// </summary>
        private void PrepareBitmaps(byte[] rgba, int w, int h)
        {
            _originalBgra = RgbaToBgra(rgba, w, h);
            var rect = new Int32Rect(0, 0, w, h);

            _wbOriginal = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            _wbOriginal.WritePixels(rect, _originalBgra, w * 4, 0);

            _wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            _wb.WritePixels(rect, _originalBgra, w * 4, 0);

            _histOriginal = ComputeHistogram(rgba);
            _histGraded = _histOriginal;
            ApplyPreviewSource();
            DrawHistogram();
        }

        /// <summary>统计 256 级 RGB 直方图（跳过全透明像素，避免 MMD 贴图的空白区把分布拉偏）。</summary>
        private static int[] ComputeHistogram(byte[] rgba)
        {
            var bins = new int[256 * 3];
            if (rgba == null) return bins;
            for (int i = 0; i + 3 < rgba.Length; i += 4)
            {
                if (rgba[i + 3] == 0) continue;
                bins[rgba[i]]++;
                bins[256 + rgba[i + 1]]++;
                bins[512 + rgba[i + 2]]++;
            }
            return bins;
        }

        public void ResetParams()
        {
            if (!HasNonDefaultParams()) { Status(L.T("St.AlreadyDefault")); return; }
            PushHistory(_prevSnapshot);
            _suppressHistory = true;
            try
            {
                Settings.Reset();
                _matParams.Remove(_currentMatIndex);   // 该材质保存的参数也一并清掉
            }
            finally { _suppressHistory = false; }
            _prevSnapshot = Snapshot();
            UpdateUndoButtons();
            RunGradeAsync();
            MarkCurrentModified();
            Status(L.T("St.ResetDone"));
        }

        /// <summary>是否存在非 0（非默认）的调色参数。</summary>
        private bool HasNonDefaultParams()
        {
            foreach (var g in _groups)
                foreach (var s in g.Sliders)
                    if (Math.Abs(Settings[s.Key]) > 1e-9) return true;
            return false;
        }

        /// <summary>跑调色管线并合成遮罩，返回最终 RGBA（选区外保持原图）。</summary>
        private byte[] ComputeGraded()
        {
            if (_originalBytes == null) return null;
            byte[] grad = (byte[])_originalBytes.Clone();
            _pipeline.Run(grad, _w, _h, Settings);

            if (_maskCache == null && _selectedTris.Count > 0) _maskCache = BuildMask();
            var mask = _maskCache;
            if (mask != null)
            {
                for (int p = 0; p < mask.Length; p++)
                {
                    if (mask[p]) continue;
                    int o = p * 4;
                    grad[o] = _originalBytes[o];
                    grad[o + 1] = _originalBytes[o + 1];
                    grad[o + 2] = _originalBytes[o + 2];
                    grad[o + 3] = _originalBytes[o + 3];
                }
            }
            return grad;
        }

        private void RunGradeAsync()
        {
            if (_originalBytes == null) return;
            int w = _w, h = _h;
            var settings = Settings;
            var pipeline = _pipeline;
            byte[] original = _originalBytes;
            bool[] mask = _maskCache;
            if (mask == null && _selectedTris.Count > 0) { mask = BuildMask(); _maskCache = mask; }

            System.Threading.Tasks.Task.Run(() =>
            {
                byte[] grad = (byte[])original.Clone();
                pipeline.Run(grad, w, h, settings);
                if (mask != null)
                {
                    for (int p = 0; p < mask.Length; p++)
                    {
                        if (mask[p]) continue;
                        int o = p * 4;
                        grad[o] = original[o]; grad[o + 1] = original[o + 1];
                        grad[o + 2] = original[o + 2]; grad[o + 3] = original[o + 3];
                    }
                }
                // 直方图在同一次遍历里算出来，不额外多扫一遍像素
                return new { Bgra = RgbaToBgra(grad, w, h), Hist = ComputeHistogram(grad) };
            }).ContinueWith(t =>
            {
                if (t.IsFaulted || t.Result == null) return;
                Dispatcher.Invoke(() =>
                {
                    if (_wb == null || _wb.PixelWidth != w || _wb.PixelHeight != h)
                    {
                        _wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                        if (_wbOriginal == null || _wbOriginal.PixelWidth != w || _wbOriginal.PixelHeight != h)
                        {
                            _wbOriginal = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                            if (_originalBgra != null)
                                _wbOriginal.WritePixels(new Int32Rect(0, 0, w, h), _originalBgra, w * 4, 0);
                        }
                    }
                    _wb.WritePixels(new Int32Rect(0, 0, w, h), t.Result.Bgra, w * 4, 0);
                    _histGraded = t.Result.Hist;
                    ApplyPreviewSource();
                    DrawHistogram();
                });
            });
        }

        /// <summary>
        /// 画 RGB 三通道叠加直方图。
        /// 归一化用「最高单级计数」——直方图只是辅助判断曝光/色彩分布，够用且零额外开销。
        /// 全透明像素已在统计时跳过，所以 MMD 贴图的空白区不会影响形状。
        /// </summary>
        private void DrawHistogram()
        {
            var canvas = HistogramCanvas;
            if (canvas == null) return;
            canvas.Children.Clear();
            if (!_showHistogram) return;

            var bins = _compareMode ? _histOriginal : _histGraded;
            if (bins == null) bins = _histOriginal;
            if (bins == null) return;

            double w = canvas.ActualWidth, h = canvas.ActualHeight;
            if (w <= 1 || h <= 1) return;

            double peak = 0;
            for (int i = 0; i < bins.Length; i++) if (bins[i] > peak) peak = bins[i];
            if (peak <= 0) return;

            // 25% / 50% / 75% 亮度参考线
            var gridBrush = new SolidColorBrush(Color.FromArgb(38, 70, 90, 120));
            for (int k = 1; k <= 3; k++)
            {
                double gx = w * k / 4.0;
                canvas.Children.Add(new Shapes.Line
                {
                    X1 = gx, X2 = gx, Y1 = 0, Y2 = h,
                    Stroke = gridBrush, StrokeThickness = 1
                });
            }

            var colors = new[]
            {
                Color.FromArgb(145, 226, 56, 56),   // R
                Color.FromArgb(145, 46, 190, 84),   // G
                Color.FromArgb(145, 56, 116, 226)   // B
            };

            for (int ch = 0; ch < 3; ch++)
            {
                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(0, h), true, true);
                    for (int i = 0; i < 256; i++)
                    {
                        double x = i * w / 255.0;
                        double y = h - Math.Min(1.0, bins[ch * 256 + i] / peak) * h;
                        ctx.LineTo(new Point(x, y), true, false);
                    }
                    ctx.LineTo(new Point(w, h), true, false);
                }
                geo.Freeze();
                canvas.Children.Add(new Shapes.Path { Data = geo, Fill = new SolidColorBrush(colors[ch]) });
            }
        }

        public void RefreshModel()
        {
            if (_originalBytes == null || _currentMatIndex < 0 || _currentAbsOriginal == null)
            { Status(L.T("St.NeedTexturedMaterial")); return; }
            byte[] grad = ComputeGraded();
            string tmp = TextureNaming.PreviewPath(_currentAbsOriginal, ++_previewCounter);
            TextureLoader.SavePng(tmp, grad, _w, _h);
            _bridge.ApplyPreview(_currentMatIndex, tmp);
            _pushedMats.Add(_currentMatIndex);
            MarkCurrentModified();
            RefreshCurrentThumbnail(tmp);   // 列表缩略图同步成推送到模型的样子
            Status(L.F("St.RefreshedFmt", System.IO.Path.GetFileName(tmp)));
        }

        public void SaveNew()
        {
            if (_originalBytes == null || _currentMatIndex < 0 || _currentAbsOriginal == null)
            { Status(L.T("St.NeedTexturedMaterial")); return; }
            byte[] grad = ComputeGraded();
            string newPath = TextureNaming.NewSavePath(_currentAbsOriginal);
            TextureLoader.SavePng(newPath, grad, _w, _h);
            _bridge.ApplySaved(_currentMatIndex, newPath);
            _pushedMats.Add(_currentMatIndex);
            MarkCurrentModified();
            RefreshCurrentThumbnail(newPath);
            Status(L.F("St.SavedNewFmt", System.IO.Path.GetFileName(newPath)));
        }

        /// <summary>
        /// 用指定贴图文件重新生成「当前材质」的列表缩略图。
        /// 刷新模型/另存为之后调用 —— 让列表里立刻看到改后的效果，而不是仍显示原图。
        /// 刻意不动 _thumbGeneration（那会打断批量生成），只需保证只更新这一行即可。
        /// </summary>
        private void RefreshCurrentThumbnail(string absPath)
        {
            if (string.IsNullOrEmpty(absPath) || _currentMatIndex < 0) return;
            int matIndex = _currentMatIndex;
            System.Threading.Tasks.Task.Run(() =>
            {
                var src = ThumbnailFactory.Create(absPath, ThumbSize);
                if (src == null) return;
                Dispatcher.Invoke(() =>
                {
                    _thumbCache[absPath] = src;
                    foreach (var r in _rows)
                        if (r.Info.Index == matIndex) { r.Thumbnail = src; break; }
                });
            });
        }

        /// <summary>
        /// 导出 UV 布局图（参照 UVEditor 的「导出UV布局图」）：
        /// 透明背景 PNG + 黑色半透明线框，边按坐标去重（共享边只画一次，否则重复描边会变粗）。
        /// 尺寸默认取当前贴图的实际宽高 —— 这样导出的布局图可以直接叠在贴图上对齐；
        /// 若当前材质没有贴图则退回 2048×2048。
        /// </summary>
        public void ExportUvLayout()
        {
            if (_currentMatIndex < 0 || _tris.Count == 0) { Status(L.T("St.NeedUvMaterial")); return; }

            int resW = _w > 0 ? _w : 2048;
            int resH = _h > 0 ? _h : 2048;

            string matName = "material";
            foreach (var r in _rows)
                if (r.Info.Index == _currentMatIndex) { matName = r.Info.Name; break; }
            matName = Sanitize(matName);

            string path;
            using (var dlg = new System.Windows.Forms.SaveFileDialog())
            {
                dlg.Title = L.T("Dlg.ExportUvTitle");
                dlg.Filter = L.T("Dlg.FilterPng");
                dlg.FileName = matName + "_UVLayout.png";
                dlg.InitialDirectory = _bridge.PmxDirectory ?? "";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                path = dlg.FileName;
            }

            try
            {
                using (var bmp = new System.Drawing.Bitmap(resW, resH, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = System.Drawing.Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.Transparent);
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(180, 0, 0, 0), 1.5f))
                        {
                            var seen = new HashSet<string>();
                            foreach (var t in _tris)
                            {
                                double x1 = t.u1 * resW, y1 = t.v1 * resH;
                                double x2 = t.u2 * resW, y2 = t.v2 * resH;
                                double x3 = t.u3 * resW, y3 = t.v3 * resH;
                                DrawEdgeOnce(g, pen, seen, x1, y1, x2, y2);
                                DrawEdgeOnce(g, pen, seen, x2, y2, x3, y3);
                                DrawEdgeOnce(g, pen, seen, x3, y3, x1, y1);
                            }
                        }
                    }
                    bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                }
                Status(L.F("St.UvExportedFmt", System.IO.Path.GetFileName(path), resW, resH));
                System.Windows.Forms.MessageBox.Show(
                    L.F("Dlg.ExportUvBody", path, resW, resH),
                    L.T("Dlg.ExportUvTitle"), System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Status(L.F("St.ExportFail", ex.Message));
                System.Windows.Forms.MessageBox.Show(L.F("St.ExportFail", ex.Message), L.T("Dlg.ExportUvTitle"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>共享边去重后画一条线。</summary>
        private static void DrawEdgeOnce(System.Drawing.Graphics g, System.Drawing.Pen pen, HashSet<string> seen,
                                         double x1, double y1, double x2, double y2)
        {
            int a1 = (int)Math.Round(x1 * 100), b1 = (int)Math.Round(y1 * 100);
            int a2 = (int)Math.Round(x2 * 100), b2 = (int)Math.Round(y2 * 100);
            bool flip = a1 > a2 || (a1 == a2 && b1 > b2);
            string key = flip ? $"{a2},{b2}-{a1},{b1}" : $"{a1},{b1}-{a2},{b2}";
            if (!seen.Add(key)) return;
            g.DrawLine(pen, (float)x1, (float)y1, (float)x2, (float)y2);
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "material";
            var bad = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char ch in name)
                sb.Append(Array.IndexOf(bad, ch) >= 0 ? '_' : ch);
            return sb.ToString();
        }

        // ================= 导出蒙版（拿去 PS 用） =================
        /// <summary>
        /// 导出「当前 UV 选区」的蒙版：白 = 选中的 UV 面，黑 = 其余，纯黑白不透明，
        /// 尺寸取该材质贴图的真实尺寸（所以能和贴图像素严丝合缝对齐）。
        /// </summary>
        public void ExportSelectionMask()
        {
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }
            if (_selectedTris.Count == 0)
            {
                Status(L.T("St.NoSelection"));
                System.Windows.Forms.MessageBox.Show(
                    L.T("Dlg.NoSelMaskBody"),
                    L.T("Dlg.MaskTitle"), System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
                return;
            }

            var info = FindMaterialInfo(_currentMatIndex);
            int w, h;
            if (!ResolveMaskSize(info, out w, out h)) return;

            string matName = Sanitize(info != null ? info.Name : "material");
            string fileName = matName + "_mask.png";

            string path;
            using (var dlg = new System.Windows.Forms.SaveFileDialog())
            {
                dlg.Title = L.T("Dlg.MaskSelTitle");
                dlg.Filter = L.T("Dlg.FilterPng");
                dlg.FileName = fileName;
                dlg.InitialDirectory = _bridge.PmxDirectory ?? "";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                path = dlg.FileName;
            }

            try
            {
                var mask = new bool[w * h];
                foreach (int ti in _selectedTris)
                    if (ti >= 0 && ti < _tris.Count) RasterizeTri(mask, w, h, _tris[ti]);

                MaskWriter.SaveBlackWhitePng(path, mask, w, h);

                Status(L.F("St.SelMaskExportedFmt", System.IO.Path.GetFileName(path), w, h, _selectedTris.Count));
                System.Windows.Forms.MessageBox.Show(
                    L.F("Dlg.SelMaskBodyFmt", path, w, h, _selectedTris.Count),
                    L.T("Dlg.MaskTitle"), System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Status(L.F("St.MaskFail", ex.Message));
                System.Windows.Forms.MessageBox.Show(L.F("St.MaskFail", ex.Message), L.T("Dlg.MaskTitle"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出「当前材质」的蒙版：白 = 该材质所有 UV 面覆盖的区域，黑 = 其余。
        /// 与「导出选区蒙版」的区别是不需要先手动选面 —— 一次拿到整份材质的区域，
        /// 适合在 PS 里按材质分层调色。
        /// </summary>
        public void ExportCurrentMaterialMask()
        {
            if (_currentMatIndex < 0) { Status(L.T("St.NeedMaterial")); return; }

            var info = FindMaterialInfo(_currentMatIndex);
            var uvTris = _bridge.GetUVTriangles(_currentMatIndex);
            if (uvTris == null || uvTris.Count == 0)
            {
                Status(L.T("St.MatNoUv"));
                System.Windows.Forms.MessageBox.Show(L.T("St.MatNoUv"), L.T("Dlg.MaskTitle"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                return;
            }

            int w, h;
            if (!ResolveMaskSize(info, out w, out h)) return;

            string fileName = Sanitize(info != null ? info.Name : "material") + "_mask.png";

            string path;
            using (var dlg = new System.Windows.Forms.SaveFileDialog())
            {
                dlg.Title = L.T("Dlg.MaskMatTitle");
                dlg.Filter = L.T("Dlg.FilterPng");
                dlg.FileName = fileName;
                dlg.InitialDirectory = _bridge.PmxDirectory ?? "";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                path = dlg.FileName;
            }

            try
            {
                var mask = new bool[w * h];
                foreach (var t in uvTris)
                    RasterizeTri(mask, w, h, new UvTri(t.u1, t.v1, t.u2, t.v2, t.u3, t.v3));

                MaskWriter.SaveBlackWhitePng(path, mask, w, h);

                Status(L.F("St.MatMaskExportedFmt", System.IO.Path.GetFileName(path), w, h, uvTris.Count));
                System.Windows.Forms.MessageBox.Show(
                    L.F("Dlg.MatMaskBodyFmt", path, w, h, uvTris.Count),
                    L.T("Dlg.MaskTitle"), System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Status(L.F("St.MaskFail", ex.Message));
                System.Windows.Forms.MessageBox.Show(L.F("St.MaskFail", ex.Message), L.T("Dlg.MaskTitle"),
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出「全部材质」的蒙版：每个材质一张 PNG（该材质所有 UV 面为白），
        /// 输出到同一个文件夹，文件名形如 <c>03_皮肤_mask.png</c>。
        /// </summary>
        public void ExportAllMasks()
        {
            if (_rows.Count == 0) { Status(L.T("St.NoMaterialToExport")); return; }

            string dir;
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = L.T("Dlg.MaskAllDesc");
                dlg.SelectedPath = _bridge.PmxDirectory ?? "";
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                dir = dlg.SelectedPath;
            }

            var oldCursor = System.Windows.Forms.Cursor.Current;
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;

            int ok = 0, noUv = 0, failed = 0;
            string lastName = "";
            try
            {
                foreach (var r in _rows)
                {
                    var tris = _bridge.GetUVTriangles(r.Info.Index);
                    if (tris == null || tris.Count == 0) { noUv++; continue; }

                    int mw, mh;
                    if (!ResolveMaskSize(r.Info, out mw, out mh)) { failed++; continue; }

                    try
                    {
                        var mask = new bool[mw * mh];
                        foreach (var t in tris)
                            RasterizeTri(mask, mw, mh, new UvTri(t.u1, t.v1, t.u2, t.v2, t.u3, t.v3));

                        string file = $"{r.Info.Index:D2}_{Sanitize(r.Info.Name)}_mask.png";
                        MaskWriter.SaveBlackWhitePng(System.IO.Path.Combine(dir, file), mask, mw, mh);
                        ok++;
                        lastName = file;
                    }
                    catch { failed++; }
                }
            }
            finally { System.Windows.Forms.Cursor.Current = oldCursor; }

            Status(L.F("St.AllMaskDoneFmt", ok, dir,
                noUv > 0 ? L.F("St.AllMaskSkipFmt", noUv) : "",
                failed > 0 ? L.F("St.AllMaskFailFmt", failed) : ""));

            System.Windows.Forms.MessageBox.Show(
                L.F("Dlg.AllMaskBodyFmt", ok, dir,
                    ok > 0 ? L.F("Dlg.AllMaskNamedFmt", lastName) : "",
                    noUv > 0 ? L.F("Dlg.AllMaskSkipFmt", noUv) : "",
                    failed > 0 ? L.F("Dlg.AllMaskFailFmt", failed) : ""),
                L.T("Dlg.AllMaskTitle"), System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }

        private MaterialInfo FindMaterialInfo(int index)
        {
            foreach (var r in _rows)
                if (r.Info.Index == index) return r.Info;
            return null;
        }

        /// <summary>
        /// 蒙版尺寸：优先取该材质贴图的真实尺寸（只读文件头，不解码像素）；
        /// 无贴图读不到时退回当前预览尺寸，再不行用 2048×2048。
        /// </summary>
        private bool ResolveMaskSize(MaterialInfo info, out int w, out int h)
        {
            w = h = 0;
            string tex = info != null ? info.TexAbsPath : _currentAbsOriginal;

            if (!string.IsNullOrEmpty(tex))
                TextureSize.TryRead(tex, out w, out h);

            if (w <= 0 || h <= 0)
            {
                if (_w > 0 && _h > 0) { w = _w; h = _h; }
                else { w = 2048; h = 2048; }
            }

            // 4096² 已是 16M 像素；再大就别做 bool[] + 位图了，内存吃不消
            if ((long)w * h > 200000000L)
            {
                Status(L.F("St.TooLargeFmt", w, h));
                return false;
            }
            return true;
        }

        public void Revert()
        {
            if (_currentMatIndex < 0) return;
            _bridge.Revert(_currentMatIndex);
            _pushedMats.Remove(_currentMatIndex);
            ReRead();   // 重新读取，让材质行的贴图路径回到真实状态
            Status(L.T("St.Reverted"));
        }

        private void MarkCurrentModified()
        {
            if (_currentMatIndex < 0) return;
            bool mod = _pushedMats.Contains(_currentMatIndex)
                       || HasNonDefaultParams()
                       || CurrentHasParams();
            foreach (var r in _rows)
                if (r.Info.Index == _currentMatIndex) { r.Modified = mod; break; }
        }

        // ================= 视图缩放 / 平移 =================
        private void LayoutScene()
        {
            if (_w <= 0 || _h <= 0) return;
            CheckerRect.Width = _w; CheckerRect.Height = _h;
            PreviewImage.Width = _w; PreviewImage.Height = _h;
            GridCanvas.Width = _w; GridCanvas.Height = _h;
            UvCanvas.Width = _w; UvCanvas.Height = _h;
        }

        private void CenterScene(double vw, double vh)
        {
            _panX = (vw - _w * _zoom) / 2;
            _panY = (vh - _h * _zoom) / 2;
        }

        private void ApplyTransform()
        {
            var m = new Matrix();
            m.Scale(_zoom, _zoom);
            m.Translate(_panX, _panY);
            SceneTransform.Matrix = m;
        }

        /// <summary>
        /// 视图坐标 -> 贴图像素坐标。
        /// 用 SceneTransform 的逆矩阵换算，保证与渲染所用变换严格互逆（不再手推公式，
        /// 这是之前「缩放/平移后选不中 UV」的根因：手推的逆变换与渲染变换不一致）。
        /// </summary>
        private bool TryScenePoint(Point viewPt, out Point scenePt)
        {
            scenePt = new Point();
            var m = SceneTransform.Matrix;
            if (!m.HasInverse) return false;
            m.Invert();
            scenePt = m.Transform(viewPt);
            return true;
        }

        private void ZoomBy(double factor)
        {
            if (_w <= 0 || _h <= 0) return;
            ZoomAt(new Point(ViewRoot.ActualWidth / 2, ViewRoot.ActualHeight / 2), factor);
        }

        private void ZoomAt(Point viewPt, double factor)
        {
            double nz = Math.Max(MinZoom, Math.Min(MaxZoom, _zoom * factor));
            if (Math.Abs(nz - _zoom) < 1e-9) return;
            if (!TryScenePoint(viewPt, out var anchor)) return;   // 与命中换算同源，保证锚点不漂移
            _zoom = nz;
            _panX = viewPt.X - anchor.X * _zoom;
            _panY = viewPt.Y - anchor.Y * _zoom;
            _autoFit = false;
            ApplyTransform();
            DrawUV(_currentMatIndex); // 线宽随缩放调整（几何体有缓存，重绘很轻）
            UpdateHint();
        }

        private void ViewRoot_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_w <= 0 || _h <= 0) return;
            ZoomAt(e.GetPosition(ViewRoot), e.Delta > 0 ? 1.1 : 1 / 1.1);
            e.Handled = true;
        }

        // ---------- 鼠标交互：选面模式（拖动=框选、单击=点选） / 平移模式 ----------
        private void ViewRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_w <= 0 || _h <= 0) return;
            ViewRoot.Focus();
            _clickCount = e.ClickCount;      // 桌面双击就是两次按下，靠它识别「双击选整块」
            var p = e.GetPosition(ViewRoot);
            if (_mode == ToolMode.Pan) StartPan(p);
            else StartBand(p);
            e.Handled = true;
        }

        private void ViewRoot_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_w <= 0 || _h <= 0) return;
            StartPan(e.GetPosition(ViewRoot));   // 右键随时平移，选面模式下也方便
            e.Handled = true;
        }

        private void ViewRoot_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_panning) { EndPan(); e.Handled = true; }
        }

        private void StartPan(Point p)
        {
            _panning = true;
            _panLast = p;
            ViewRoot.CaptureMouse();
            ViewRoot.Cursor = Cursors.SizeAll;
        }

        private void EndPan()
        {
            _panning = false;
            ViewRoot.ReleaseMouseCapture();
            ViewRoot.Cursor = null;
        }

        private void StartBand(Point p)
        {
            _banding = true;
            _bandMoved = false;
            _bandStart = p;
            ViewRoot.CaptureMouse();
        }

        private void ViewRoot_MouseMove(object sender, MouseEventArgs e)
        {
            var p = e.GetPosition(ViewRoot);
            SetReadout(p);

            if (_panning)
            {
                _panX += p.X - _panLast.X;
                _panY += p.Y - _panLast.Y;
                _panLast = p;
                _autoFit = false;
                ApplyTransform();   // UV 线框在 Scene 内部，随变换一起移动，无需重绘
                return;
            }

            if (_banding)
            {
                if (!_bandMoved && (Math.Abs(p.X - _bandStart.X) + Math.Abs(p.Y - _bandStart.Y)) > 4)
                    _bandMoved = true;
                if (_bandMoved) ShowBand(_bandStart, p);
            }
        }

        private void ViewRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_panning) { EndPan(); e.Handled = true; return; }
            if (_banding) { EndBand(e.GetPosition(ViewRoot)); e.Handled = true; }
        }

        private void ShowBand(Point a, Point b)
        {
            double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
            double w = Math.Abs(a.X - b.X), h = Math.Abs(a.Y - b.Y);
            Canvas.SetLeft(BandRect, x); Canvas.SetTop(BandRect, y);
            BandRect.Width = w; BandRect.Height = h;
            BandRect.Visibility = Visibility.Visible;
        }

        private void EndBand(Point endPt)
        {
            _banding = false;
            ViewRoot.ReleaseMouseCapture();
            BandRect.Visibility = Visibility.Collapsed;

            if (!_bandMoved)
            {
                // 双击 -> 选中整块连通 UV 岛（类 Blender 按 L 的手感）；单击 -> 点选单个三角面
                if (_clickCount >= 2) SelectIslandAt(endPt);
                else PickTriangle(endPt);
                return;
            }
            if (!TryScenePoint(_bandStart, out var s0) || !TryScenePoint(endPt, out var s1)) return;

            double x0 = Math.Min(s0.X, s1.X), x1 = Math.Max(s0.X, s1.X);
            double y0 = Math.Min(s0.Y, s1.Y), y1 = Math.Max(s0.Y, s1.Y);

            bool add = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool sub = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (!add && !sub) _selectedTris.Clear();

            int hits = 0;
            // 先按「三个顶点都在框内」（对应 UVEditor 的"矩形内顶点全选"语义）
            for (int i = 0; i < _tris.Count; i++)
                if (TriangleInsideRect(_tris[i], x0, y0, x1, y1)) ApplyHit(i, sub, ref hits);

            // 一个都没框到就放宽为「重心在框内」，避免小框选空
            if (hits == 0)
                for (int i = 0; i < _tris.Count; i++)
                    if (CentroidInsideRect(_tris[i], x0, y0, x1, y1)) ApplyHit(i, sub, ref hits);

            AfterSelectionChanged(hits > 0
                ? L.F("St.BandSelectedFmt", hits, _selectedTris.Count)
                : L.T("St.BandEmpty"));
        }

        private void ApplyHit(int triIndex, bool subtract, ref int hits)
        {
            if (subtract) _selectedTris.Remove(triIndex);
            else _selectedTris.Add(triIndex);
            hits++;
        }

        // ================= UV 选区 =================
        /// <summary>视图坐标 -> 命中的三角面索引（精确重心命中；失败退化为「近邻容差」）。-1 = 没命中。</summary>
        private int HitTestTriangle(Point viewPt)
        {
            if (_w <= 0 || _h <= 0 || _tris.Count == 0) return -1;
            if (!TryScenePoint(viewPt, out var sp)) return -1;
            if (sp.X < 0 || sp.Y < 0 || sp.X > _w || sp.Y > _h) return -1;

            float u = (float)(sp.X / _w);
            float v = (float)(sp.Y / _h);   // V=0 = 贴图顶行（MMD DirectX 约定）
            int hit = -1;
            for (int i = 0; i < _tris.Count; i++)
                if (PointInTriangle(u, v, _tris[i])) hit = i;   // 取最上层命中的面

            // 精确命中失败 -> 用 UVEditor 式的「近邻容差」再试一次（放大时光标很难精确落在面内）
            if (hit < 0) hit = NearestTriangle(sp.X, sp.Y, 12.0 / Math.Max(_zoom, 1e-6));
            return hit;
        }

        private void PickTriangle(Point viewPt)
        {
            if (_w <= 0 || _h <= 0) return;
            if (_tris.Count == 0) { Status(L.T("St.NoTris")); return; }
            int hit = HitTestTriangle(viewPt);
            if (hit < 0) { Status(L.T("St.NoTriHere")); return; }

            _lastPickedTri = hit;
            bool add = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool sub = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (add) _selectedTris.Add(hit);
            else if (sub) _selectedTris.Remove(hit);
            else if (!_selectedTris.Add(hit)) _selectedTris.Remove(hit);   // 普通点击：再点一次取消

            AfterSelectionChanged(_selectedTris.Count > 0
                ? L.F("St.SelectedFmt", _selectedTris.Count)
                : L.T("St.NoneSelected"));
        }

        // ================= 连通 UV 块（类 Blender 按 L 选一整块） =================
        /// <summary>双击某处 -> 选中它所在的整块连通 UV 岛。</summary>
        private void SelectIslandAt(Point viewPt)
        {
            if (_tris.Count == 0) { Status(L.T("St.NoTris")); return; }
            int hit = HitTestTriangle(viewPt);
            if (hit < 0) { Status(L.T("St.DoubleClickMiss")); return; }
            SelectIslandOf(hit);
        }

        /// <summary>
        /// 选中整块连通 UV（工具按钮「选连通块」/ 编辑菜单入口）。
        /// 已有选区时把**选区涉及到的每一块**都补全成完整连通块（可以同时补多块）；
        /// 没有选区时以最后点过的面为种子。始终只加不减，不会清掉已有选区。
        /// </summary>
        public void SelectConnectedIsland()
        {
            if (_tris.Count == 0) { Status(L.T("St.NoTris")); return; }
            if (_triIsland.Length != _tris.Count) BuildIslands();

            // 种子 = 当前选区涉及的所有连通块（这样一次就能把多块同时补全）
            var islands = new HashSet<int>();
            foreach (int i in _selectedTris)
                if (i >= 0 && i < _triIsland.Length) islands.Add(_triIsland[i]);

            // 没有任何选区 -> 退回「最后点过的那个面」所在的块
            if (islands.Count == 0)
            {
                if (_lastPickedTri < 0 || _lastPickedTri >= _triIsland.Length)
                { Status(L.T("St.IslandHint")); return; }
                islands.Add(_triIsland[_lastPickedTri]);
            }

            int added = 0;
            for (int i = 0; i < _triIsland.Length; i++)
                if (islands.Contains(_triIsland[i]) && _selectedTris.Add(i)) added++;

            AfterSelectionChanged(L.F("St.IslandGrownFmt", islands.Count, added, _selectedTris.Count));
        }

        /// <summary>
        /// 选中（或取消）triIndex 所在的整块连通 UV。
        /// 默认累加：连续双击不同的块可以多选；整块已在选区里时再双击一次则取消它。
        /// Ctrl+双击 = 强制取消该块；Shift+双击 = 强制加选。
        /// </summary>
        private void SelectIslandOf(int triIndex)
        {
            if (triIndex < 0 || triIndex >= _tris.Count) return;
            if (_triIsland.Length != _tris.Count) BuildIslands();

            int island = _triIsland[triIndex];

            var members = new List<int>();
            for (int i = 0; i < _triIsland.Length; i++)
                if (_triIsland[i] == island) members.Add(i);

            bool add = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool sub = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            // 整块都已在选区里 -> 视为「想取消它」
            bool allIn = members.Count > 0;
            foreach (int m in members)
                if (!_selectedTris.Contains(m)) { allIn = false; break; }

            _lastPickedTri = triIndex;

            if (sub || (allIn && !add))
            {
                foreach (int m in members) _selectedTris.Remove(m);
                AfterSelectionChanged(L.F("St.IslandRemovedFmt", island + 1, _islandCount, _selectedTris.Count));
            }
            else
            {
                foreach (int m in members) _selectedTris.Add(m);
                AfterSelectionChanged(L.F("St.IslandAddedFmt", island + 1, _islandCount, members.Count, _selectedTris.Count));
            }
        }

        /// <summary>选区变化后的统一收尾：遮罩失效 -> 重绘 UV -> 重跑调色。</summary>
        private void AfterSelectionChanged(string status)
        {
            _maskCache = null;
            InvalidateGeo();
            DrawUV(_currentMatIndex);
            RunGradeAsync();
            Status(status);
            UpdateHint();
        }

        /// <summary>
        /// 把三角面按「共享 UV 顶点」聚成连通块（并查集）。
        /// UV 坐标先按 1e-4 量化再比较，避免浮点误差把本该连续的块切碎。
        /// 判据是「共享边」（两个顶点都落在同一量化位置），与 UVEditor 里按顶点归属判断邻接的语义一致。
        /// </summary>
        private void BuildIslands()
        {
            int n = _tris.Count;
            _triIsland = new int[n];
            _islandCount = 0;
            if (n == 0) return;

            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            var verts = new Dictionary<long, int>(n * 3);
            var vids = new int[n * 3];
            for (int i = 0; i < n; i++)
            {
                var t = _tris[i];
                vids[i * 3] = VertId(verts, t.u1, t.v1);
                vids[i * 3 + 1] = VertId(verts, t.u2, t.v2);
                vids[i * 3 + 2] = VertId(verts, t.u3, t.v3);
            }

            var edges = new Dictionary<long, int>(n * 3);
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int a = vids[i * 3 + k];
                    int b = vids[i * 3 + (k + 1) % 3];
                    if (a == b) continue;
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int other;
                    if (edges.TryGetValue(key, out other)) Union(parent, i, other);
                    else edges[key] = i;
                }
            }

            var map = new Dictionary<int, int>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                int id;
                if (!map.TryGetValue(root, out id)) { id = map.Count; map[root] = id; }
                _triIsland[i] = id;
            }
            _islandCount = map.Count;
        }

        private static int VertId(Dictionary<long, int> map, float u, float v)
        {
            long qx = (long)Math.Round(u * 10000.0);
            long qy = (long)Math.Round(v * 10000.0);
            long key = (qx << 32) ^ (qy & 0xFFFFFFFFL);
            int id;
            if (map.TryGetValue(key, out id)) return id;
            id = map.Count;
            map[key] = id;
            return id;
        }

        private static int Find(int[] parent, int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        private static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a); b = Find(parent, b);
            if (a != b) parent[b] = a;
        }

        /// <summary>在贴图像素坐标系里找离点击点最近、且在容差内的三角面（点/边距离）。</summary>
        private int NearestTriangle(double px, double py, double tolPx)
        {
            int best = -1;
            double bestD = tolPx;
            for (int i = 0; i < _tris.Count; i++)
            {
                var t = _tris[i];
                double x1 = t.u1 * _w, y1 = t.v1 * _h;
                double x2 = t.u2 * _w, y2 = t.v2 * _h;
                double x3 = t.u3 * _w, y3 = t.v3 * _h;

                double d = Dist(px, py, x1, y1);
                d = Math.Min(d, Dist(px, py, x2, y2));
                d = Math.Min(d, Dist(px, py, x3, y3));
                d = Math.Min(d, DistSeg(px, py, x1, y1, x2, y2));
                d = Math.Min(d, DistSeg(px, py, x2, y2, x3, y3));
                d = Math.Min(d, DistSeg(px, py, x3, y3, x1, y1));
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static double Dist(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double DistSeg(double px, double py, double ax, double ay, double bx, double by)
        {
            double vx = bx - ax, vy = by - ay;
            double len2 = vx * vx + vy * vy;
            if (len2 < 1e-12) return Dist(px, py, ax, ay);
            double t = ((px - ax) * vx + (py - ay) * vy) / len2;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return Dist(px, py, ax + t * vx, ay + t * vy);
        }

        private bool TriangleInsideRect(UvTri t, double x0, double y0, double x1, double y1)
        {
            return InRect(t.u1, t.v1, x0, y0, x1, y1)
                && InRect(t.u2, t.v2, x0, y0, x1, y1)
                && InRect(t.u3, t.v3, x0, y0, x1, y1);
        }

        private bool CentroidInsideRect(UvTri t, double x0, double y0, double x1, double y1)
        {
            double cu = (t.u1 + t.u2 + t.u3) / 3.0;
            double cv = (t.v1 + t.v2 + t.v3) / 3.0;
            return InRect(cu, cv, x0, y0, x1, y1);
        }

        private bool InRect(double u, double v, double x0, double y0, double x1, double y1)
        {
            if (_w <= 0 || _h <= 0) return false;
            double px = u * _w, py = v * _h;   // V=0 在顶行
            return px >= x0 && px <= x1 && py >= y0 && py <= y1;
        }

        private bool[] BuildMask()
        {
            if (_selectedTris.Count == 0 || _w <= 0 || _h <= 0) return null;
            var mask = new bool[_w * _h];
            foreach (int ti in _selectedTris)
            {
                if (ti < 0 || ti >= _tris.Count) continue;
                RasterizeTriangle(mask, _tris[ti]);
            }
            return mask;
        }

        private void RasterizeTriangle(bool[] mask, UvTri t) => RasterizeTri(mask, _w, _h, t);

        /// <summary>把三角形栅格化进蒙版（像素中心落在三角形内即算选中）。w/h 可指定为任意目标尺寸。</summary>
        private static void RasterizeTri(bool[] mask, int w, int h, UvTri t)
        {
            if (w <= 0 || h <= 0) return;
            double x1 = t.u1 * w, y1 = t.v1 * h;
            double x2 = t.u2 * w, y2 = t.v2 * h;
            double x3 = t.u3 * w, y3 = t.v3 * h;

            int minX = Clamp((int)Math.Floor(Math.Min(x1, Math.Min(x2, x3))), 0, w - 1);
            int maxX = Clamp((int)Math.Ceiling(Math.Max(x1, Math.Max(x2, x3))), 0, w - 1);
            int minY = Clamp((int)Math.Floor(Math.Min(y1, Math.Min(y2, y3))), 0, h - 1);
            int maxY = Clamp((int)Math.Ceiling(Math.Max(y1, Math.Max(y2, y3))), 0, h - 1);

            double d = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3);
            if (Math.Abs(d) < 1e-9) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    double px = x + 0.5, py = y + 0.5;
                    double a = ((y2 - y3) * (px - x3) + (x3 - x2) * (py - y3)) / d;
                    double b = ((y3 - y1) * (px - x3) + (x1 - x3) * (py - y3)) / d;
                    double c = 1 - a - b;
                    if (a >= -1e-6 && b >= -1e-6 && c >= -1e-6) mask[y * w + x] = true;
                }
            }
        }

        private static bool PointInTriangle(float px, float py, UvTri t)
        {
            double d1 = Sign(px, py, t.u1, t.v1, t.u2, t.v2);
            double d2 = Sign(px, py, t.u2, t.v2, t.u3, t.v3);
            double d3 = Sign(px, py, t.u3, t.v3, t.u1, t.v1);
            bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
            bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(hasNeg && hasPos);
        }

        private static double Sign(float px, float py, float ax, float ay, float bx, float by)
            => (px - bx) * (ay - by) - (ax - bx) * (py - by);

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        // ================= UV 线框绘制（贴图像素坐标系） =================
        private void DrawUV(int matIndex)
        {
            if (UvCanvas == null) return;
            UvCanvas.Children.Clear();
            DrawGrid();
            if (!_showUV || matIndex < 0 || _w <= 0 || _h <= 0) return;

            double th = Math.Max(0.4, 1.15 / Math.Max(_zoom, 1e-6));
            var lineBrush = new SolidColorBrush(_uvLine);
            var dotBrush = new SolidColorBrush(_uvVertex);
            var selStroke = new SolidColorBrush(Color.FromArgb(240, 0, 168, 60));
            var selFill = new SolidColorBrush(Color.FromArgb(90, 0, 200, 80));

            // 1) 未选中的三角面合并进一个几何体（上千个 Path 太卡）—— 有缓存
            if (_geoDirty || _geoAllCache == null)
            {
                var geoAll = new StreamGeometry();
                using (var ctx = geoAll.Open())
                {
                    for (int i = 0; i < _tris.Count; i++)
                    {
                        if (_selectedTris.Contains(i)) continue;
                        AppendTriangle(ctx, _tris[i]);
                    }
                }
                geoAll.Freeze();
                _geoAllCache = geoAll;
                _geoDirty = false;
            }

            // 顶点记号：大小与缩放相关，量化后就重建（避免滚轮每一格都重建几万个菱形）
            if (_showVertices && _tris.Count <= DotTriangleLimit)
            {
                bool needDots = _geoDotsCache == null
                                || _geoDotsZoom <= 0
                                || Math.Abs(Math.Log(_zoom / _geoDotsZoom)) > 0.22;
                if (needDots)
                {
                    double r = Math.Max(0.9, 2.4 / Math.Max(_zoom, 1e-6));
                    var geoDots = new StreamGeometry();
                    using (var ctx = geoDots.Open())
                    {
                        for (int i = 0; i < _tris.Count; i++)
                        {
                            Dot(ctx, _tris[i].u1, _tris[i].v1, r);
                            Dot(ctx, _tris[i].u2, _tris[i].v2, r);
                            Dot(ctx, _tris[i].u3, _tris[i].v3, r);
                        }
                    }
                    geoDots.Freeze();
                    _geoDotsCache = geoDots;
                    _geoDotsZoom = _zoom;
                }
            }
            else { _geoDotsCache = null; _geoDotsZoom = 0; }

            UvCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = _geoAllCache,
                Stroke = lineBrush,
                StrokeThickness = th
            });

            // 2) 选中的三角面：单独绘制并填充高亮
            foreach (int i in _selectedTris)
            {
                if (i < 0 || i >= _tris.Count) continue;
                var geo = new StreamGeometry();
                using (var ctx = geo.Open()) AppendTriangle(ctx, _tris[i]);
                geo.Freeze();
                UvCanvas.Children.Add(new System.Windows.Shapes.Path
                {
                    Data = geo,
                    Stroke = selStroke,
                    StrokeThickness = Math.Max(th, 1.0 / Math.Max(_zoom, 1e-6)),
                    Fill = selFill
                });
            }

            // 3) UV 顶点记号
            if (_geoDotsCache != null)
                UvCanvas.Children.Add(new System.Windows.Shapes.Path { Data = _geoDotsCache, Fill = dotBrush });
        }

        private void AppendTriangle(StreamGeometryContext ctx, UvTri t)
        {
            ctx.BeginFigure(new Point(t.u1 * _w, t.v1 * _h), false, true);
            ctx.LineTo(new Point(t.u2 * _w, t.v2 * _h), true, false);
            ctx.LineTo(new Point(t.u3 * _w, t.v3 * _h), true, false);
        }

        private void Dot(StreamGeometryContext ctx, double u, double v, double r)
        {
            double x = u * _w, y = v * _h;
            ctx.BeginFigure(new Point(x, y - r), true, true);
            ctx.LineTo(new Point(x + r, y), true, false);
            ctx.LineTo(new Point(x, y + r), true, false);
            ctx.LineTo(new Point(x - r, y), true, false);
        }

        private void DrawGrid()
        {
            if (GridCanvas == null) return;
            GridCanvas.Children.Clear();
            if (!_showGrid || _w <= 0 || _h <= 0) return;

            double step = Math.Max(64.0, Math.Max(_w, _h) / 8.0);
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                for (double x = 0; x <= _w + 0.5; x += step)
                {
                    ctx.BeginFigure(new Point(x, 0), false, false);
                    ctx.LineTo(new Point(x, _h), true, false);
                }
                for (double y = 0; y <= _h + 0.5; y += step)
                {
                    ctx.BeginFigure(new Point(0, y), false, false);
                    ctx.LineTo(new Point(_w, y), true, false);
                }
            }
            geo.Freeze();
            GridCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = geo,
                Stroke = new SolidColorBrush(Color.FromArgb(70, 90, 120, 160)),
                StrokeThickness = Math.Max(0.4, 1.0 / Math.Max(_zoom, 1e-6))
            });
        }

        // ================= 工具 =================
        private static byte[] RgbaToBgra(byte[] rgba, int w, int h)
        {
            var outp = new byte[w * h * 4];
            for (int i = 0, j = 0; i < rgba.Length; i += 4, j += 4)
            {
                outp[j] = rgba[i + 2];
                outp[j + 1] = rgba[i + 1];
                outp[j + 2] = rgba[i];
                outp[j + 3] = rgba[i + 3];
            }
            return outp;
        }

        private void Status(string text) { if (_status != null) _status.Text = text; }

        private void SetReadout(Point? viewPt)
        {
            if (UvReadout == null) return;
            string empty = L.T("Readout.Empty");
            if (viewPt == null || _w <= 0 || _h <= 0) { UvReadout.Text = empty; return; }
            if (!TryScenePoint(viewPt.Value, out var sp)) { UvReadout.Text = empty; return; }
            if (sp.X < 0 || sp.Y < 0 || sp.X > _w || sp.Y > _h) { UvReadout.Text = empty; return; }
            double u = sp.X / _w, v = sp.Y / _h;
            UvReadout.Text = L.F("Readout.Fmt", u, v, (int)sp.X, (int)sp.Y);
        }

        private void UpdateHint()
        {
            if (ViewHint == null) return;
            if (_w <= 0) { ViewHint.Text = L.T("Hint.PickMaterial"); return; }
            string sel = _selectedTris.Count > 0
                ? L.F("Hint.SelCount", _selectedTris.Count)
                : L.T("Hint.SelNone");
            string mode = _mode == ToolMode.Pan ? L.T("Hint.ModePan") : L.T("Hint.ModeSelect");
            string cmp = _compareMode ? L.T("Hint.Comparing") : "";
            ViewHint.Text = L.F("Hint.Zoom", _zoom * 100.0) + "   ·   " + sel
                            + "   ·   " + L.T("Hint.Wheel")
                            + "   ·   " + mode
                            + "   ·   " + L.T("Hint.RightPan") + cmp;
        }

        private struct UvTri
        {
            public readonly float u1, v1, u2, v2, u3, v3;
            public UvTri(float u1, float v1, float u2, float v2, float u3, float v3)
            { this.u1 = u1; this.v1 = v1; this.u2 = u2; this.v2 = v2; this.u3 = u3; this.v3 = v3; }
        }

        // ================= 滑块定义 =================
        private class SliderDef
        {
            public string Label; public double Min; public double Max; public string Key;
            /// <summary>可选的小节标题：同一组内做视觉分栏（如 HSL 的每种颜色）。</summary>
            public string Section;
            /// <summary>
            /// 只登记、不渲染成滑块行。用于「由控件而非滑块驱动」的参数
            /// （如 Lab 取色环的目标 a*/b*）——登记后即可自动获得
            /// 撤销/重做、分材质保存、预设存取这些能力。
            /// </summary>
            public bool Hidden;
        }
        private class GroupDef { public string TitleKey; public bool Expanded; public SliderDef[] Sliders; }
        private static SliderDef S(string l, double min, double max, string k, string section = null)
            => new SliderDef { Label = l, Min = min, Max = max, Key = k, Section = section };
        /// <summary>只登记、不渲染的参数（见 SliderDef.Hidden）。</summary>
        private static SliderDef SH(string k) => new SliderDef { Label = k, Min = 0, Max = 1, Key = k, Hidden = true };

        private readonly GroupDef[] _groups = new[]
        {
            new GroupDef { TitleKey = "Grp.Basic", Expanded = true, Sliders = new[]
            {
                S("Exposure", -2, 2, "Exposure"), S("Contrast", -100, 100, "Contrast"),
                S("Highlights", -100, 100, "Highlights"), S("Shadows", -100, 100, "Shadows"),
                S("Whites", -100, 100, "Whites"), S("Blacks", -100, 100, "Blacks")
            }},
            new GroupDef { TitleKey = "Grp.Color", Expanded = false, Sliders = new[]
            {
                S("Temperature", -100, 100, "Temperature"), S("Tint", -100, 100, "Tint"),
                S("Vibrance", -100, 100, "Vibrance"), S("Saturation", -100, 100, "Saturation"),
                S("Hue", -180, 180, "Hue"),
                S("ColorBalanceR", -100, 100, "ColorBalanceR"), S("ColorBalanceG", -100, 100, "ColorBalanceG"), S("ColorBalanceB", -100, 100, "ColorBalanceB"),
                // Lab 取色环的参数：由色环/勾选框直接驱动，不出滑块行，
                // 但登记在这里才能进撤销重做、分材质保存和预设。
                SH("LabAmount"), SH("LabTargetL"), SH("LabTargetA"), SH("LabTargetB"), SH("LabUnlock")
            }},
            // HSL 分通道：8 个色带各自独立的 色相 / 饱和度 / 明度（Lightroom 的 HSL 面板）
            new GroupDef { TitleKey = "Grp.Hsl", Expanded = false, Sliders = new[]
            {
                S("HslRedH", -100, 100, "HslRedH", "Band.Red"),         S("HslRedS", -100, 100, "HslRedS", "Band.Red"),         S("HslRedL", -100, 100, "HslRedL", "Band.Red"),
                S("HslOrangeH", -100, 100, "HslOrangeH", "Band.Orange"),   S("HslOrangeS", -100, 100, "HslOrangeS", "Band.Orange"),   S("HslOrangeL", -100, 100, "HslOrangeL", "Band.Orange"),
                S("HslYellowH", -100, 100, "HslYellowH", "Band.Yellow"),   S("HslYellowS", -100, 100, "HslYellowS", "Band.Yellow"),   S("HslYellowL", -100, 100, "HslYellowL", "Band.Yellow"),
                S("HslGreenH", -100, 100, "HslGreenH", "Band.Green"),     S("HslGreenS", -100, 100, "HslGreenS", "Band.Green"),     S("HslGreenL", -100, 100, "HslGreenL", "Band.Green"),
                S("HslAquaH", -100, 100, "HslAquaH", "Band.Aqua"),       S("HslAquaS", -100, 100, "HslAquaS", "Band.Aqua"),       S("HslAquaL", -100, 100, "HslAquaL", "Band.Aqua"),
                S("HslBlueH", -100, 100, "HslBlueH", "Band.Blue"),       S("HslBlueS", -100, 100, "HslBlueS", "Band.Blue"),       S("HslBlueL", -100, 100, "HslBlueL", "Band.Blue"),
                S("HslPurpleH", -100, 100, "HslPurpleH", "Band.Purple"),   S("HslPurpleS", -100, 100, "HslPurpleS", "Band.Purple"),   S("HslPurpleL", -100, 100, "HslPurpleL", "Band.Purple"),
                S("HslMagentaH", -100, 100, "HslMagentaH", "Band.Magenta"), S("HslMagentaS", -100, 100, "HslMagentaS", "Band.Magenta"), S("HslMagentaL", -100, 100, "HslMagentaL", "Band.Magenta")
            }},
            new GroupDef { TitleKey = "Grp.Curve", Expanded = false, Sliders = new[]
            {
                S("Curve", -100, 100, "Curve"),
                S("LevelsBlack", -100, 100, "LevelsBlack"), S("LevelsWhite", -100, 100, "LevelsWhite"), S("LevelsGamma", -100, 100, "LevelsGamma"),
                S("RgbR", -100, 100, "RgbR"), S("RgbG", -100, 100, "RgbG"), S("RgbB", -100, 100, "RgbB"),
                S("HsvValue", -100, 100, "HsvValue")
            }},
            new GroupDef { TitleKey = "Grp.Detail", Expanded = false, Sliders = new[]
            {
                S("Clarity", -100, 100, "Clarity"), S("Sharpen", -100, 100, "Sharpen")
            }},
            new GroupDef { TitleKey = "Grp.Fx", Expanded = false, Sliders = new[]
            {
                S("Gradient", 0, 100, "Gradient"), S("Grayscale", 0, 100, "Grayscale"),
                S("Invert", 0, 1, "Invert"), S("Threshold", 0, 1, "Threshold")
            }}
        };
    }
}
