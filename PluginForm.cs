using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using TextureGrade.Bridge;
using TextureGrade.Localization;
using TextureGrade.WpfUI;

namespace TextureGrade
{
    /// <summary>
    /// WinForms 外壳（薄薄一层）。内部用 ElementHost 承载 WPF 的 MainPanel，
    /// 这样既能直接用 PMXEditor 的 PEPlugin 宿主（Form），又能享受 WPF 的自适应布局与折叠面板。
    /// 菜单栏用原生 WinForms MenuStrip（不用 WPF Menu —— 后者在 ElementHost 里弹层可能异常）。
    ///
    /// 切换语言时不逐个改菜单项的 Text，而是整条重建 MenуStrip ——
    /// 菜单项不多，重建比维护一堆字段映射简单，也不会漏掉子菜单。
    /// </summary>
    public class PluginForm : Form
    {
        private readonly ElementHost _host;
        private readonly MainPanel _panel;
        private readonly IPMDBridge _bridge;
        private MenuStrip _menu;
        private HelpWindow _help;

        private ToolStripMenuItem _miShowUV;
        private ToolStripMenuItem _miShowVertices;
        private ToolStripMenuItem _miGrid;
        private ToolStripMenuItem _miHistogram;
        private ToolStripMenuItem _miCompare;
        private ToolStripMenuItem _miModeSelect;
        private ToolStripMenuItem _miModePan;
        private ToolStripMenuItem _miUvBlue, _miUvRed, _miUvGreen, _miUvPurple, _miUvWhite, _miUvBlack;
        private ToolStripMenuItem _miLangEn, _miLangZhCn, _miLangZhTw, _miLangJa;

        public PluginForm(PEPlugin.IPEPluginHost host)
        {
            // 首次使用（或 data 目录被删）时，在插件目录 data 下补出四份语言说明 txt
            try { OperationManual.EnsureAll(); } catch { /* 生成失败不影响主功能 */ }

            var bridge = new PmxBridge(host);
            _bridge = bridge;
            _panel = new MainPanel(bridge);
            // 面板里的「对比原图」按钮与菜单勾选状态保持同步
            _panel.CompareChanged += SyncCompareMenu;
            _panel.ShowUVChanged += SyncUvMenu;

            Text = L.T("App.Title");

            // 初始 1200×800、最小 1000×680；若屏幕装不下则自动收敛到工作区，避免标题栏跑到屏幕外
            var wa = Screen.PrimaryScreen.WorkingArea;
            StartPosition = FormStartPosition.CenterScreen;
            Width = Math.Min(1200, wa.Width - 40);
            Height = Math.Min(800, wa.Height - 40);
            MinimumSize = new Size(Math.Min(1000, Width), Math.Min(680, Height));

            _host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = _panel,
                BackColor = Color.White
            };
            _menu = BuildMenu();

            // 注意顺序：Fill 控件先加、Dock=Top 的菜单后加，WinForms 才能正确划分顶部/填充区
            Controls.Add(_host);
            Controls.Add(_menu);
            MainMenuStrip = _menu;

            FormClosed += (s, e) =>
            {
                // 关闭时清理临时预览文件（WPF UserControl 无 Dispose，清理逻辑放在桥接层）
                _bridge.Cleanup();
                if (_help != null && !_help.IsDisposed) _help.Dispose();
                _host.Dispose();
            };
        }

        // ================= 菜单栏 =================
        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip { Font = new Font("Microsoft YaHei UI", 10.5f) };

            // ---------- 文件 ----------
            var file = new ToolStripMenuItem(L.T("Menu.File"));
            file.DropDownItems.Add(Make("File.ReRead", () => _panel.ReRead()));
            file.DropDownItems.Add(Make("File.RefreshModel", () => _panel.RefreshModel()));
            file.DropDownItems.Add(Make("File.SaveNewAs", () => _panel.SaveNewAs()));
            file.DropDownItems.Add(Make("File.SavePngQuick", () => _panel.SaveNew()));
            file.DropDownItems.Add(Make("File.ExportUvLayout", () => _panel.ExportUvLayout()));
            file.DropDownItems.Add(Make("File.ExportSelectionMask", () => _panel.ExportSelectionMask()));
            file.DropDownItems.Add(Make("File.ExportSelectionAlpha", () => _panel.ExportSelectionAlpha()));
            file.DropDownItems.Add(Make("File.ExportMaterialMask", () => _panel.ExportCurrentMaterialMask()));
            file.DropDownItems.Add(Make("File.ExportAllMasks", () => _panel.ExportAllMasks()));
            file.DropDownItems.Add(Make("File.OpenPresetFolder", () => _panel.OpenPresetFolder()));
            file.DropDownItems.Add(Make("File.Revert", () => _panel.Revert()));
            file.DropDownItems.Add(Make("File.ResetParams", () => _panel.ResetParams()));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Make("File.Undo", () => _panel.Undo()));
            file.DropDownItems.Add(Make("File.Redo", () => _panel.Redo()));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Make("File.Exit", Close));

            // ---------- 编辑 ----------
            var edit = new ToolStripMenuItem(L.T("Menu.Edit"));

            _miShowUV = new ToolStripMenuItem(L.T("Edit.ShowUV")) { CheckOnClick = true, Checked = _panel.ShowUV };
            _miShowUV.CheckedChanged += (s, e) => _panel.SetShowUV(_miShowUV.Checked);
            edit.DropDownItems.Add(_miShowUV);

            _miShowVertices = new ToolStripMenuItem(L.T("Edit.ShowVertices")) { CheckOnClick = true, Checked = _panel.ShowVertices };
            _miShowVertices.CheckedChanged += (s, e) => _panel.SetShowVertices(_miShowVertices.Checked);
            edit.DropDownItems.Add(_miShowVertices);

            edit.DropDownItems.Add(BuildUvColorMenu());
            edit.DropDownItems.Add(new ToolStripSeparator());

            _miModeSelect = new ToolStripMenuItem(L.T("Edit.ModeSelect")) { Checked = !_panel.PanMode };
            _miModeSelect.Click += (s, e) => SetPanMode(false);
            _miModePan = new ToolStripMenuItem(L.T("Edit.ModePan")) { Checked = _panel.PanMode };
            _miModePan.Click += (s, e) => SetPanMode(true);
            edit.DropDownItems.Add(_miModeSelect);
            edit.DropDownItems.Add(_miModePan);

            edit.DropDownItems.Add(new ToolStripSeparator());
            edit.DropDownItems.Add(Make("Edit.SelectIsland", () => _panel.SelectConnectedIsland()));
            edit.DropDownItems.Add(Make("Edit.SelectAll", () => _panel.SelectAllUv()));
            edit.DropDownItems.Add(Make("Edit.InvertSel", () => _panel.InvertUVSelection()));
            edit.DropDownItems.Add(Make("Edit.ClearSel", () => _panel.ClearUVSelection()));

            // ---------- 视图 ----------
            var view = new ToolStripMenuItem(L.T("Menu.View"));
            view.DropDownItems.Add(Make("View.Fit", () => _panel.FitView()));
            view.DropDownItems.Add(Make("View.Actual", () => _panel.ActualSize()));
            view.DropDownItems.Add(Make("View.ZoomIn", () => _panel.ZoomIn()));
            view.DropDownItems.Add(Make("View.ZoomOut", () => _panel.ZoomOut()));
            view.DropDownItems.Add(new ToolStripSeparator());
            _miGrid = new ToolStripMenuItem(L.T("View.Grid")) { CheckOnClick = true, Checked = _panel.GridVisible };
            _miGrid.CheckedChanged += (s, e) => _panel.SetGridVisible(_miGrid.Checked);
            view.DropDownItems.Add(_miGrid);

            _miHistogram = new ToolStripMenuItem(L.T("View.Histogram")) { CheckOnClick = true, Checked = _panel.HistogramVisible };
            _miHistogram.CheckedChanged += (s, e) => _panel.SetHistogramVisible(_miHistogram.Checked);
            view.DropDownItems.Add(_miHistogram);

            _miCompare = new ToolStripMenuItem(L.T("View.Compare")) { CheckOnClick = true, Checked = _panel.ComparingOriginal };
            _miCompare.CheckedChanged += (s, e) => _panel.SetCompareOriginal(_miCompare.Checked);
            view.DropDownItems.Add(_miCompare);

            // ---------- 语言 ----------
            var lang = new ToolStripMenuItem(L.T("Menu.Language"));
            _miLangEn = MakeLang("Lang.En", Lang.En);
            _miLangZhCn = MakeLang("Lang.ZhCn", Lang.ZhCn);
            _miLangZhTw = MakeLang("Lang.ZhTw", Lang.ZhTw);
            _miLangJa = MakeLang("Lang.Ja", Lang.Ja);
            lang.DropDownItems.Add(_miLangEn);
            lang.DropDownItems.Add(_miLangZhCn);
            lang.DropDownItems.Add(_miLangZhTw);
            lang.DropDownItems.Add(_miLangJa);
            CheckOnlyLang(L.Current);

            // ---------- 帮助 ----------
            var help = new ToolStripMenuItem(L.T("Menu.Help"));
            help.DropDownItems.Add(Make("Help.Operation", ShowHelp));
            help.DropDownItems.Add(Make("Help.About", ShowAbout));

            menu.Items.Add(file);
            menu.Items.Add(edit);
            menu.Items.Add(view);
            menu.Items.Add(lang);
            menu.Items.Add(help);
            return menu;
        }

        /// <summary>「修改 UV 布局颜色」子菜单：预设 + 自定义取色器。</summary>
        private ToolStripMenuItem BuildUvColorMenu()
        {
            var root = new ToolStripMenuItem(L.T("Edit.UvColor"));

            _miUvBlue = MakeColor("Edit.UvBlue", System.Windows.Media.Color.FromRgb(0, 118, 214));
            _miUvRed = MakeColor("Edit.UvRed", System.Windows.Media.Color.FromRgb(214, 32, 32));
            _miUvGreen = MakeColor("Edit.UvGreen", System.Windows.Media.Color.FromRgb(0, 150, 60));
            _miUvPurple = MakeColor("Edit.UvPurple", System.Windows.Media.Color.FromRgb(150, 60, 214));
            _miUvWhite = MakeColor("Edit.UvWhite", System.Windows.Media.Color.FromRgb(255, 255, 255));
            _miUvBlack = MakeColor("Edit.UvBlack", System.Windows.Media.Color.FromRgb(0, 0, 0));

            root.DropDownItems.Add(_miUvBlue);
            root.DropDownItems.Add(_miUvRed);
            root.DropDownItems.Add(_miUvGreen);
            root.DropDownItems.Add(_miUvPurple);
            root.DropDownItems.Add(_miUvWhite);
            root.DropDownItems.Add(_miUvBlack);
            root.DropDownItems.Add(new ToolStripSeparator());
            root.DropDownItems.Add(Make("Edit.UvCustom", PickUvColor));

            _miUvBlue.Checked = true;
            return root;
        }

        private ToolStripMenuItem MakeColor(string key, System.Windows.Media.Color c)
        {
            // 不在文字上做文章（白色/黑色文字改成灰字会让人以为菜单项被禁用）；
            // 改为在左侧显示一个真实的颜色色块图标来区分。
            var item = new ToolStripMenuItem(L.T(key))
            {
                CheckOnClick = false,
                Image = MakeSwatch(c),
                ImageScaling = ToolStripItemImageScaling.None
            };
            item.Click += (s, e) =>
            {
                _panel.SetUvColor(c);
                CheckOnly(item);
            };
            return item;
        }

        /// <summary>生成 16×16 的颜色色块图标（带一圈浅灰描边，白色块也看得见）。</summary>
        private static System.Drawing.Bitmap MakeSwatch(System.Windows.Media.Color c)
        {
            var bmp = new System.Drawing.Bitmap(16, 16);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(255, c.R, c.G, c.B));
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(150, 90, 90, 90)))
                {
                    pen.Alignment = System.Drawing.Drawing2D.PenAlignment.Inset;
                    g.DrawRectangle(pen, 0, 0, 15, 15);
                }
            }
            return bmp;
        }

        private void CheckOnly(ToolStripMenuItem item)
        {
            foreach (var it in new[] { _miUvBlue, _miUvRed, _miUvGreen, _miUvPurple, _miUvWhite, _miUvBlack })
                if (it != null) it.Checked = it == item;
        }

        private void PickUvColor()
        {
            using (var dlg = new ColorDialog { FullOpen = true, AnyColor = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var c = dlg.Color;
                _panel.SetUvColor(System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B));
                CheckOnly(null);
            }
        }

        private void SetPanMode(bool pan)
        {
            _panel.SetPanMode(pan);
            _miModeSelect.Checked = !pan;
            _miModePan.Checked = pan;
        }

        /// <summary>面板里的「对比原图」按钮被点后，同步菜单勾选状态。</summary>
        private void SyncCompareMenu()
        {
            if (_miCompare != null) _miCompare.Checked = _panel.ComparingOriginal;
        }

        private void SyncUvMenu()
        {
            if (_miShowUV != null) _miShowUV.Checked = _panel.ShowUV;
        }

        // ================= 语言 =================
        private ToolStripMenuItem MakeLang(string key, Lang lang)
        {
            var item = new ToolStripMenuItem(L.T(key)) { CheckOnClick = false, Tag = lang };
            item.Click += (s, e) => SwitchLanguage(lang);
            return item;
        }

        private void CheckOnlyLang(Lang lang)
        {
            foreach (var it in new[] { _miLangEn, _miLangZhCn, _miLangZhTw, _miLangJa })
                if (it != null) it.Checked = it.Tag is Lang l && l == lang;
        }

        private void SwitchLanguage(Lang lang)
        {
            if (L.Current == lang) return;

            L.Set(lang);              // 写进插件目录的 lang.txt，下次启动保持
            _panel.ApplyLanguage();   // 主面板（只重取文字，不重建面板）
            RebuildMenu();            // 菜单栏整体重建
            Text = L.T("App.Title");
            CheckOnlyLang(lang);

            // 说明窗口开着的话一起刷新（会改读对应语言的 txt）
            if (_help != null && !_help.IsDisposed) _help.Reload();
        }

        private void RebuildMenu()
        {
            var old = _menu;
            _menu = BuildMenu();
            Controls.Remove(old);
            Controls.Add(_menu);   // 后加 -> Dock=Top 能正确排在填充区上方
            MainMenuStrip = _menu;
            old.Dispose();
        }

        // ================= 帮助 =================
        private void ShowHelp()
        {
            if (_help == null || _help.IsDisposed)
            {
                _help = new HelpWindow();
                _help.Show(this);      // 非模态：可以边看说明边操作
            }
            else
            {
                _help.Reload();
                _help.BringToFront();
                _help.Focus();
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(this, L.T("About.Body"), L.T("About.Title"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static ToolStripMenuItem Make(string key, Action act)
        {
            var item = new ToolStripMenuItem(L.T(key));
            item.Click += (s, e) => act();
            return item;
        }
    }
}
