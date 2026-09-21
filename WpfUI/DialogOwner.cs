using System;

namespace TextureGrade.WpfUI
{
    /// <summary>
    /// 把 Win32 窗口句柄包装成 WinForms 的 IWin32Window。
    /// 用途：WPF 面板（MainPanel）里弹出的 WinForms 对话框需要 owner，
    /// 否则对话框是独立顶层窗口，可能跑到主窗口后面去、看着像"点了没反应"。
    ///
    /// 注意：IWin32Window 在 System.Windows.Forms 和 System.Windows.Interop 里各有一个，
    /// 这里两个命名空间都用到（后者要 WindowInteropHelper），所以类型名一律写全限定，别偷懒。
    /// </summary>
    internal sealed class Win32Owner : System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle { get; private set; }

        public Win32Owner(IntPtr handle) { Handle = handle; }

        /// <summary>取某个视觉元素所在 WPF 窗口的句柄；拿不到就返回 null（调用方按无 owner 处理）。</summary>
        public static System.Windows.Forms.IWin32Window From(System.Windows.Media.Visual element)
        {
            try
            {
                var w = element == null ? null : System.Windows.Window.GetWindow(element);
<<<<<<< HEAD
                if (w == null) return null;
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
=======
                var source = System.Windows.PresentationSource.FromVisual(element) as System.Windows.Interop.HwndSource;
                var h = w != null ? new System.Windows.Interop.WindowInteropHelper(w).Handle : source?.Handle ?? IntPtr.Zero;
                // ElementHost has an HWND but no parent WPF Window.
                var form = h == IntPtr.Zero ? null : System.Windows.Forms.Control.FromChildHandle(h)?.FindForm();
                if (form != null) return form;
>>>>>>> pr-1
                return h == IntPtr.Zero ? null : new Win32Owner(h);
            }
            catch
            {
                return null;   // 拿不到句柄就退回无 owner，不让它影响主流程
            }
        }
    }
}
