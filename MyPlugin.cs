using System;
using System.Reflection;
using System.Windows.Forms;
using PEPlugin;
using TextureGrade.Localization;

namespace TextureGrade
{
    /// <summary>
    /// PMXEditor 插件入口。注册菜单 "Texture Grade"，点击后打开调色窗口。
    /// 参考 UVEditor 0.219 的 MyPlugin 结构（PEPluginClass + PEPluginOption + Run）。
    /// </summary>
    public class MyPlugin : PEPluginClass
    {
        private PluginForm _form;

        /// <summary>
        /// 静态构造：给整个 AppDomain 装一个「自己人兜底」的程序集解析兜底。
        ///
        /// 背景：WPF 的 UserControl.InitializeComponent() 走 Application.LoadComponent(pack URI)，
        /// 需要把 URI 里的程序集名解析成 Assembly。插件 DLL 是 PMXEditor 用 LoadFrom 从
        /// _plugin 子目录加载的，不在 AppBase 的探测路径里，而融合绑定器（fusion）在做
        /// 「按名称/版本」解析时不会去 LoadFrom 上下文里找，于是抛
        /// FileNotFoundException: 未能加载文件或程序集 "PEPlugins-TextureGrade, Version=..."。
        ///
        /// 兜底方案：绑定失败会触发 AssemblyResolve，这里直接把已经加载好的自身程序集还回去。
        /// 这样无论 URI 里带不带版本、DLL 放在哪个目录都能解析成功。
        /// （真正的问题在 csproj：不要显式写 &lt;AssemblyVersion&gt;，详见那里的注释。）
        /// </summary>
        static MyPlugin()
        {
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                var self = typeof(MyPlugin).Assembly;
                if (self == null) return null;

                string wanted = args.Name;
                int comma = wanted.IndexOf(',');
                if (comma >= 0) wanted = wanted.Substring(0, comma);   // 只比简单名，忽略版本/文化/公钥

                if (string.Equals(wanted.Trim(), self.GetName().Name, StringComparison.OrdinalIgnoreCase))
                    return self;
            }
            catch
            {
                // 解析兜底失败就交回系统处理，不要在事件里抛异常
            }
            return null;
        }

        public MyPlugin()
        {
            // bootup:false 不随 PMXEditor 启动自动运行；regMenu:true 注册到右键/菜单；菜单文字
            m_option = new PEPluginOption(bootup: false, regMenu: true, "Texture Grade");
        }

        public override void Run(IPERunArgs args)
        {
            try
            {
                if (_form == null || _form.IsDisposed)
                {
                    _form = new PluginForm(args.Host);
                }

                if (_form.Visible)
                {
                    _form.BringToFront();
                }
                else
                {
                    // 非模态：方便边调色边看 PMXEditor 的 3D 视图
                    _form.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), L.T("Err.Title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
