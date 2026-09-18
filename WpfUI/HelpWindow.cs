using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TextureGrade.Localization;

namespace TextureGrade.WpfUI
{
    /// <summary>
    /// 「帮助 — 操作说明」窗口。
    ///
    /// 说明正文不写在代码里，而是从**插件同级目录 data 文件夹**里按语言读取：
    ///   data\TextureGrade_Operation_EN.txt / _SC.txt / _TC.txt / _JP.txt
    /// 切换界面语言时窗口会改读对应那份（开着就即时刷新，关着则下次打开即新语言）。
    /// 文件缺失时按当前语言自动生成一份内置说明写进去，再显示。
    /// 改说明不用重新编译插件，改完点窗口里的「重新载入」即可。
    ///
    /// 窗口可自由缩放（Sizable + 右下角把手，可最大化/最小化），并设了最小尺寸防止缩没。
    /// </summary>
    public sealed class HelpWindow : Form
    {
        private readonly TextBox _box;
        private readonly Button _btnReload;
        private readonly Button _btnFolder;
        private readonly Button _btnClose;
        private readonly Label _lblSource;
        private readonly Panel _bottom;

        public HelpWindow()
        {
            Text = L.T("Help.Title");
            StartPosition = FormStartPosition.CenterScreen;

            // 可缩放：右下角有把手，可最大化 / 最小化
            FormBorderStyle = FormBorderStyle.Sizable;
            SizeGripStyle = SizeGripStyle.Show;
            MaximizeBox = true;
            MinimizeBox = true;
            ShowInTaskbar = false;

            Size = new Size(820, 660);
            MinimumSize = new Size(460, 320);   // 再小就没法读了
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10f);

            _box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                WordWrap = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                ForeColor = Color.FromArgb(0x22, 0x28, 0x30),
                Font = new Font("Consolas", 10.5f),
                Dock = DockStyle.Fill,
                Margin = new Padding(0)
            };

            _lblSource = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                ForeColor = Color.FromArgb(0x77, 0x7E, 0x88),
                Padding = new Padding(10, 6, 10, 0),
                AutoEllipsis = true
            };

            _bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(10, 8, 10, 10) };
            _btnReload = new Button { Width = 110, Height = 32, Left = 10, Top = 8 };
            _btnFolder = new Button { Width = 130, Height = 32, Left = 130, Top = 8 };
            _btnClose = new Button { Width = 100, Height = 32, Top = 8, DialogResult = DialogResult.Cancel };
            _bottom.Controls.Add(_btnReload);
            _bottom.Controls.Add(_btnFolder);
            _bottom.Controls.Add(_btnClose);
            _bottom.Resize += (s, e) => LayoutButtons();

            Controls.Add(_box);
            Controls.Add(_lblSource);
            Controls.Add(_bottom);

            _btnReload.Click += (s, e) => LoadContent();
            _btnFolder.Click += (s, e) => OpenFolder();
            _btnClose.Click += (s, e) => Close();
            CancelButton = _btnClose;

            ApplyTexts();
            LayoutButtons();
            LoadContent();
        }

        /// <summary>关闭按钮贴右下角（窗口缩放后跟着走）。</summary>
        private void LayoutButtons()
        {
            if (_bottom == null || _btnClose == null) return;
            int w = _bottom.ClientSize.Width;
            if (w <= 0) return;
            _btnClose.Left = Math.Max(_btnFolder.Right + 8, w - _btnClose.Width - 10);
        }

        /// <summary>切换语言后刷新窗口自身的文案（并重新挑对应语言的说明文件）。</summary>
        public void ApplyTexts()
        {
            Text = L.T("Help.Title");
            _btnReload.Text = L.T("Help.Reload");
            _btnFolder.Text = L.T("Help.OpenFolder");
            _btnClose.Text = L.T("Help.Close");
        }

        public void Reload()
        {
            ApplyTexts();
            LoadContent();
        }

        private void LoadContent()
        {
            string path = OperationManual.Resolve(L.Current);
            string note = "";

            string text;
            try
            {
                if (File.Exists(path))
                {
                    text = ReadText(path);
                }
                else
                {
                    // 文件不存在（例如目录只读）：直接用内置文本显示，不强写
                    text = DefaultManual.Get(L.Current);
                    note = L.F("Help.MissingFmt", path);
                }
            }
            catch (Exception ex)
            {
                text = "";
                note = L.F("Help.LoadFailFmt", path, ex.Message);
            }

            _box.Text = note.Length > 0 ? (text + "\n\n" + note) : text;
            _box.SelectionStart = 0;
            _box.ScrollToCaret();
            _lblSource.Text = L.F("Help.SourceFmt", path);
        }

        /// <summary>读文本：自动识别 UTF-8 / UTF-8 BOM / GBK，避免中文说明乱码。</summary>
        private static string ReadText(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);

            // 非法 UTF-8 序列 -> 按系统 ANSI（中文 Windows 即 GBK）解码
            try
            {
                var strict = new UTF8Encoding(false, true);
                return strict.GetString(bytes);
            }
            catch
            {
                return Encoding.Default.GetString(bytes);
            }
        }

        private void OpenFolder()
        {
            try
            {
                string dir = OperationManual.Folder;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { /* 打开失败不影响使用 */ }
        }
    }
}
