using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace WorkTimeWPF
{
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        protected override void OnStartup(StartupEventArgs e)
        {
            // 启用DPI感知，提高文本渲染清晰度
            if (Environment.OSVersion.Version.Major >= 6)
            {
                SetProcessDPIAware();
            }
            
            base.OnStartup(e);
        }
    }
}
