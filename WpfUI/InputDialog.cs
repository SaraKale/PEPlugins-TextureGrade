using System;
using System.Drawing;
using System.Windows.Forms;
using TextureGrade.Localization;

namespace TextureGrade.WpfUI
{
    /// <summary>
    /// 单行文字输入对话框（预设改名等场合用）。
    /// 没能依赖任何 NuGet，就是 WinForms 原生控件拼的一个小窗口。
    /// </summary>
    public sealed class InputDialog : Form
    {
        private readonly TextBox _box;
        private readonly Button _btnOk, _btnCancel;

        public string Value { get; private set; }

        public InputDialog(string title, string prompt, string defaultValue)
        {
            Text = title;
<<<<<<< HEAD
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            Size = new Size(460, 180);
=======
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 190);
>>>>>>> pr-1
            BackColor = Color.White;
            Font = new Font("Microsoft YaHei UI", 10.5f);

            var lbl = new Label { Text = prompt, Left = 18, Top = 18, Width = 410, Height = 40 };
            _box = new TextBox { Left = 18, Top = 64, Width = 410, Text = defaultValue ?? "" };
            // 构造期 SelectAll 无效（还没获得焦点），放到 Shown 里 —— 打开即全选，直接改名覆盖很顺手
            Shown += (s, e) => { _box.Focus(); _box.SelectAll(); };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            _btnOk = new Button { Text = L.T("Dlg.OK"), Width = 100, Height = 34, Left = 228, Top = 10, DialogResult = DialogResult.OK };
            _btnCancel = new Button { Text = L.T("Dlg.Cancel"), Width = 100, Height = 34, Left = 336, Top = 10, DialogResult = DialogResult.Cancel };
            _btnOk.Click += (s, e) => { Value = (_box.Text ?? "").Trim(); };
            bottom.Controls.Add(_btnOk);
            bottom.Controls.Add(_btnCancel);

            Controls.Add(lbl);
            Controls.Add(_box);
            Controls.Add(bottom);

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;
        }

        /// <summary>弹出输入框；返回 null 表示取消。</summary>
        public static string Prompt(IWin32Window owner, string title, string prompt, string defaultValue)
        {
            using (var dlg = new InputDialog(title, prompt, defaultValue))
            {
                if (dlg.ShowDialog(owner) != DialogResult.OK) return null;
                return dlg.Value;
            }
        }
    }
}
