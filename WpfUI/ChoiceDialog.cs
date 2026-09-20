using System;
using System.Drawing;
using System.Windows.Forms;
using TextureGrade.Localization;

namespace TextureGrade.WpfUI
{
    /// <summary>
    /// 单选对话框（导出 Alpha 蒙版时选「RGB 里放什么」这类场合用）。
    /// 选项少、不需要翻译键表，直接传字符串数组进来。
    /// </summary>
    public sealed class ChoiceDialog : Form
    {
        private readonly Button _btnOk, _btnCancel;
        public int SelectedIndex { get; private set; } = -1;

        public ChoiceDialog(string title, string description, string[] options, int defaultIndex)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10.5f);

            // 说明文字可能带换行，高度留够；选项多时窗口长高但封顶 560
            int height = 190 + options.Length * 34;
            Size = new Size(520, Math.Min(560, height));

            var lbl = new Label { Text = description, Left = 18, Top = 16, Width = 470, Height = 64 };
            Controls.Add(lbl);

            var radios = new RadioButton[options.Length];
            int top = 88;
            for (int i = 0; i < options.Length; i++)
            {
                var rb = new RadioButton
                {
                    Text = options[i],
                    Left = 22,
                    Top = top,
                    Width = 460,
                    Height = 28,
                    Checked = i == defaultIndex
                };
                radios[i] = rb;
                Controls.Add(rb);
                top += 30;
            }

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            _btnOk = new Button { Text = L.T("Dlg.OK"), Width = 100, Height = 34, Left = 288, Top = 10, DialogResult = DialogResult.OK };
            _btnCancel = new Button { Text = L.T("Dlg.Cancel"), Width = 100, Height = 34, Left = 396, Top = 10, DialogResult = DialogResult.Cancel };
            _btnOk.Click += (s, e) =>
            {
                for (int i = 0; i < radios.Length; i++)
                    if (radios[i].Checked) { SelectedIndex = i; break; }
            };
            bottom.Controls.Add(_btnOk);
            bottom.Controls.Add(_btnCancel);
            Controls.Add(bottom);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        /// <summary>弹出单选；返回 -1 表示取消。</summary>
        public static int Choose(IWin32Window owner, string title, string description, string[] options, int defaultIndex = 0)
        {
            using (var dlg = new ChoiceDialog(title, description, options, defaultIndex))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return -1;
                return dlg.SelectedIndex;
            }
        }
    }
}
