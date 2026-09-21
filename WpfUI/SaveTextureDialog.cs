using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TextureGrade.Localization;
using TextureGrade.TextureIO;

namespace TextureGrade.WpfUI
{
    /// <summary>
    /// 「另存为新贴图（选格式/尺寸）…」对话框。
    ///
<<<<<<< HEAD
    /// 默认的工具栏「另存」按钮仍然是无脑存 PNG 原尺寸；这里给需要的人提供：
=======
    /// 工具栏「另存」打开此窗口；文件菜单另提供原尺寸快速 PNG 保存：
>>>>>>> pr-1
    ///   - 封装格式：PNG / JPG / BMP / GIF / TIFF / TGA / DDS（未压缩 · DXT1/3/5）
    ///   - 输出尺寸：原尺寸 / 1/2 / 1/4 / 自定义宽高
    ///   - JPG 质量
    /// 换格式时文件扩展名会自动跟着变，不会存出「内容是 JPG 却叫 .png」的文件。
    /// </summary>
    public sealed class SaveTextureDialog : Form
    {
        private readonly TextBox _txtPath;
        private readonly Button _btnBrowse;
        private readonly ComboBox _cboFormat;
        private readonly ComboBox _cboSize;
        private readonly NumericUpDown _numW, _numH, _numQuality;
        private readonly Label _lblW, _lblH, _lblQuality, _lblNote, _lblInfo;
        private readonly Button _btnOk, _btnCancel;

        private readonly int _srcW, _srcH;
        private bool _editingSize;   // 程序改尺寸框时不触发联动

        public string TargetPath { get; private set; }
        public TextureFormat Format { get; private set; }
        public int TargetWidth { get; private set; }
        public int TargetHeight { get; private set; }
        public int JpegQuality { get; private set; } = 92;

        public SaveTextureDialog(string defaultPath, int srcWidth, int srcHeight,
                                 TextureFormat lastFormat = TextureFormat.Png, int lastQuality = 92)
        {
            _srcW = srcWidth; _srcH = srcHeight;

            Text = L.T("SaveDlg.Title");
<<<<<<< HEAD
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            Size = new Size(560, 330);
=======
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 360);
>>>>>>> pr-1
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10f);

            int y = 16;

            var lblPath = NewLabel(L.T("SaveDlg.Path"), 16, y + 3);
            _txtPath = new TextBox { Left = 110, Top = y, Width = 300, Text = defaultPath ?? "" };
            _btnBrowse = new Button { Left = 418, Top = y - 2, Width = 100, Height = 28, Text = L.T("SaveDlg.Browse") };
            _btnBrowse.Click += (s, e) => Browse();
            y += 40;

            var lblFormat = NewLabel(L.T("SaveDlg.Format"), 16, y + 3);
            _cboFormat = new ComboBox
            {
                Left = 110, Top = y, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (TextureFormat f in Enum.GetValues(typeof(TextureFormat)))
                _cboFormat.Items.Add(new FormatItem(f));
            _cboFormat.SelectedIndex = IndexOf(lastFormat);
            _cboFormat.SelectedIndexChanged += (s, e) => OnFormatChanged();
            y += 40;

            var lblSize = NewLabel(L.T("SaveDlg.Size"), 16, y + 3);
            _cboSize = new ComboBox { Left = 110, Top = y, Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboSize.Items.Add(L.T("SaveDlg.SizeOriginal"));
            _cboSize.Items.Add(L.T("SaveDlg.SizeHalf"));
            _cboSize.Items.Add(L.T("SaveDlg.SizeQuarter"));
            _cboSize.Items.Add(L.T("SaveDlg.SizeCustom"));
            _cboSize.SelectedIndex = 0;
            _cboSize.SelectedIndexChanged += (s, e) => OnSizeModeChanged();

            _lblW = NewLabel(L.T("SaveDlg.Width"), 282, y + 3, 40);
            _numW = NewNum(326, y, 80, 1, 16384, Math.Max(1, srcWidth));
            _lblH = NewLabel(L.T("SaveDlg.Height"), 412, y + 3, 40);
            _numH = NewNum(452, y, 80, 1, 16384, Math.Max(1, srcHeight));
            _numW.ValueChanged += (s, e) => OnCustomSizeTyped();
            _numH.ValueChanged += (s, e) => OnCustomSizeTyped();
            y += 40;

            _lblQuality = NewLabel(L.T("SaveDlg.Quality"), 16, y + 3);
            _numQuality = NewNum(110, y, 80, 1, 100, Math.Max(1, Math.Min(100, lastQuality)));
            y += 38;

            _lblNote = new Label
            {
                Left = 16, Top = y, Width = 500, Height = 20,
                ForeColor = Color.FromArgb(0x77, 0x7E, 0x88), AutoEllipsis = true
            };
            y += 26;

            _lblInfo = new Label
            {
                Left = 16, Top = y, Width = 500, Height = 20,
                ForeColor = Color.FromArgb(0x77, 0x7E, 0x88), AutoEllipsis = true
            };
            y += 30;

            Controls.AddRange(new Control[]
            {
                lblPath, _txtPath, _btnBrowse,
                lblFormat, _cboFormat,
                lblSize, _cboSize, _lblW, _numW, _lblH, _numH,
                _lblQuality, _numQuality, _lblNote, _lblInfo
            });

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            _btnOk = new Button { Text = L.T("SaveDlg.Save"), Width = 110, Height = 34, Left = 320, Top = 10 };
            _btnCancel = new Button { Text = L.T("SaveDlg.Cancel"), Width = 100, Height = 34, Left = 440, Top = 10, DialogResult = DialogResult.Cancel };
            _btnOk.Click += (s, e) => OnOk();
            bottom.Controls.Add(_btnOk);
            bottom.Controls.Add(_btnCancel);
            Controls.Add(bottom);

            CancelButton = _btnCancel;
            AcceptButton = _btnOk;

            OnFormatChanged();
<<<<<<< HEAD
=======
            OnSizeModeChanged();
>>>>>>> pr-1
        }

        private Label NewLabel(string text, int left, int top, int width = 90)
        {
            return new Label { Text = text, Left = left, Top = top, Width = width, Height = 22 };
        }

        private NumericUpDown NewNum(int left, int top, int width, int min, int max, int value)
        {
            return new NumericUpDown
            {
                Left = left, Top = top, Width = width, Minimum = min, Maximum = max,
                Value = Math.Max(min, Math.Min(max, value))
            };
        }

        private int IndexOf(TextureFormat f)
        {
            for (int i = 0; i < _cboFormat.Items.Count; i++)
                if (((FormatItem)_cboFormat.Items[i]).Value == f) return i;
            return 0;
        }

        private void Browse()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = L.T("SaveDlg.Title");
                dlg.Filter = L.T("SaveDlg.FilterAll");
<<<<<<< HEAD
=======
                dlg.OverwritePrompt = false; // Confirm the final, format-normalized filename in OnOk.
>>>>>>> pr-1
                dlg.FileName = Path.GetFileName(_txtPath.Text);
                try { dlg.InitialDirectory = Path.GetDirectoryName(_txtPath.Text) ?? ""; }
                catch { /* 路径非法就用默认目录 */ }
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _txtPath.Text = dlg.FileName;
                    // 用户手选了扩展名 -> 反推格式，避免格式和后缀打架
                    var guessed = GuessFormat(dlg.FileName);
                    if (guessed.HasValue) { _cboFormat.SelectedIndex = IndexOf(guessed.Value); OnFormatChanged(); }
                }
            }
        }

        private static TextureFormat? GuessFormat(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".png": return TextureFormat.Png;
                case ".jpg": case ".jpeg": return TextureFormat.Jpg;
                case ".bmp": return TextureFormat.Bmp;
                case ".gif": return TextureFormat.Gif;
                case ".tif": case ".tiff": return TextureFormat.Tiff;
                case ".tga": return TextureFormat.Tga;
                default: return null;   // .dds 有多种内部格式，不猜
            }
        }

        private void OnFormatChanged()
        {
            var f = ((FormatItem)_cboFormat.SelectedItem).Value;
            _numQuality.Enabled = f == TextureFormat.Jpg;
            _lblQuality.Enabled = f == TextureFormat.Jpg;

            string notes = "";
<<<<<<< HEAD
            if (!TextureWriter.KeepsAlpha(f)) notes += L.T("SaveDlg.NoAlpha") + "  ";
=======
            if (f == TextureFormat.DdsDxt1) notes += L.T("SaveDlg.BinaryAlpha") + "  ";
            else if (!TextureWriter.KeepsAlpha(f)) notes += L.T("SaveDlg.NoAlpha") + "  ";
>>>>>>> pr-1
            if (TextureWriter.IsLossy(f)) notes += L.T("SaveDlg.Lossy") + "  ";
            if (f == TextureFormat.DdsRaw || f == TextureFormat.DdsDxt1 ||
                f == TextureFormat.DdsDxt3 || f == TextureFormat.DdsDxt5)
                notes += L.T("SaveDlg.DdsNote");
            _lblNote.Text = notes.Trim();

            ApplyExt(f);
            UpdateInfo();
        }

        /// <summary>换格式时同步改扩展名，保证文件名和内容一致。</summary>
        private void ApplyExt(TextureFormat f)
        {
            string p = _txtPath.Text;
            if (string.IsNullOrWhiteSpace(p)) return;
            try
            {
                string dir = Path.GetDirectoryName(p) ?? "";
                string name = Path.GetFileNameWithoutExtension(p);
                if (string.IsNullOrEmpty(name)) return;
                _txtPath.Text = Path.Combine(dir, name + TextureWriter.Extension(f));
            }
            catch { /* 路径有怪字符就不动 */ }
        }

        private void OnSizeModeChanged()
        {
            if (_editingSize) return;
            int mode = _cboSize.SelectedIndex;
            bool custom = mode == 3;
            _numW.Enabled = custom; _numH.Enabled = custom;
            _lblW.Enabled = custom; _lblH.Enabled = custom;
            if (!custom)
            {
                int div = mode == 1 ? 2 : mode == 2 ? 4 : 1;
                int w = Math.Max(1, _srcW / div), h = Math.Max(1, _srcH / div);
                _editingSize = true;
                _numW.Value = Math.Min(_numW.Maximum, Math.Max(_numW.Minimum, w));
                _numH.Value = Math.Min(_numH.Maximum, Math.Max(_numH.Minimum, h));
                _editingSize = false;
            }
            UpdateInfo();
        }

<<<<<<< HEAD
        /// <summary>用户手改宽高 -> 自动切到「自定义」，并等比联动另一边（没按 Ctrl 时）。</summary>
=======
        /// <summary>用户手改宽高时切到「自定义」，两边可独立设置。</summary>
>>>>>>> pr-1
        private void OnCustomSizeTyped()
        {
            UpdateInfo();
            if (_editingSize) return;
            if (_cboSize.SelectedIndex != 3)
            {
                _editingSize = true;
                _cboSize.SelectedIndex = 3;
                _numW.Enabled = _numH.Enabled = _lblW.Enabled = _lblH.Enabled = true;
                _editingSize = false;
            }
        }

        private void UpdateInfo()
        {
            int w = (int)_numW.Value, h = (int)_numH.Value;
            _lblInfo.Text = L.F("SaveDlg.InfoFmt", _srcW, _srcH, w, h);
        }

        private void OnOk()
        {
<<<<<<< HEAD
=======
            ApplyExt(((FormatItem)_cboFormat.SelectedItem).Value);
>>>>>>> pr-1
            string p = (_txtPath.Text ?? "").Trim();
            if (string.IsNullOrEmpty(p))
            {
                MessageBox.Show(this, L.T("SaveDlg.NeedPath"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Format = ((FormatItem)_cboFormat.SelectedItem).Value;
            TargetWidth = (int)_numW.Value;
            TargetHeight = (int)_numH.Value;
            JpegQuality = (int)_numQuality.Value;
<<<<<<< HEAD
            TargetPath = p;
=======
            try { TargetPath = Path.GetFullPath(p); }
            catch { MessageBox.Show(this, L.T("SaveDlg.NeedPath"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if ((long)TargetWidth * TargetHeight > TextureWriter.MaxPixels)
            { MessageBox.Show(this, L.T("SaveDlg.TooLarge"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (File.Exists(TargetPath) && MessageBox.Show(this, L.F("SaveDlg.Overwrite", TargetPath), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
>>>>>>> pr-1

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class FormatItem
        {
            public TextureFormat Value { get; private set; }
            private readonly string _text;
            public FormatItem(TextureFormat f)
            {
                Value = f;
                switch (f)
                {
                    case TextureFormat.Png: _text = "PNG"; break;
                    case TextureFormat.Jpg: _text = "JPEG"; break;
                    case TextureFormat.Bmp: _text = "BMP"; break;
                    case TextureFormat.Gif: _text = "GIF"; break;
                    case TextureFormat.Tiff: _text = "TIFF"; break;
                    case TextureFormat.Tga: _text = "TGA"; break;
                    // 格式缩写是通用名，不翻译；括号里的说明也用英文免得混排
                    case TextureFormat.DdsRaw: _text = "DDS (Raw / uncompressed)"; break;
                    case TextureFormat.DdsDxt1: _text = "DDS (DXT1)"; break;
                    case TextureFormat.DdsDxt3: _text = "DDS (DXT3)"; break;
                    default: _text = "DDS (DXT5)"; break;
                }
            }
            public override string ToString() => _text;
        }
    }
}
