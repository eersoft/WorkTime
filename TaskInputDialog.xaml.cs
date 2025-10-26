using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace WorkTimeWPF
{
    public partial class TaskInputDialog : Window
    {
        public string TaskName { get; private set; }

        public TaskInputDialog()
        {
            InitializeComponent();
            TaskNameTextBox.Focus();
            ApplyThemeFromMainWindow();
        }

        private void ApplyThemeFromMainWindow()
        {
            try
            {
                // 获取主窗口的主题设置
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    // 通过反射获取主窗口的当前主题
                    var currentThemeField = mainWindow.GetType().GetField("_currentTheme", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (currentThemeField != null)
                    {
                        string currentTheme = currentThemeField.GetValue(mainWindow) as string;
                        if (!string.IsNullOrEmpty(currentTheme))
                        {
                            ApplyTheme(currentTheme);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题时发生错误: {ex.Message}");
            }
        }

        private void ApplyTheme(string themeName)
        {
            try
            {
                var brushConverter = new BrushConverter();
                
                // 定义主题颜色
                var themeColors = GetThemeColors(themeName);
                
                // 应用主题到对话框
                TaskDialogWindow.Background = (Brush)brushConverter.ConvertFromString(themeColors.Background);
                DialogBorder.Background = (Brush)brushConverter.ConvertFromString(themeColors.CardBackground);
                TaskNameLabel.Foreground = (Brush)brushConverter.ConvertFromString(themeColors.TextColor);
                
                // 更新按钮颜色
                UpdateButtonColors(themeColors);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用主题时发生错误: {ex.Message}");
            }
        }

        private void UpdateButtonColors(ThemeColors colors)
        {
            try
            {
                var brushConverter = new BrushConverter();
                
                // 更新确定按钮
                if (OkButton != null)
                {
                    OkButton.Foreground = (Brush)brushConverter.ConvertFromString(colors.PrimaryColor);
                }
                
                // 更新取消按钮
                if (CancelButton != null)
                {
                    CancelButton.Foreground = (Brush)brushConverter.ConvertFromString(colors.SecondaryTextColor);
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新按钮颜色时发生错误: {ex.Message}");
            }
        }

        private ThemeColors GetThemeColors(string themeName)
        {
            switch (themeName)
            {
                case "Blue":
                    return new ThemeColors
                    {
                        Background = "#f5f5f5",
                        CardBackground = "White",
                        PrimaryColor = "#1976d2",
                        TextColor = "#2c3e50",
                        SecondaryTextColor = "#6c757d"
                    };
                case "Green":
                    return new ThemeColors
                    {
                        Background = "#f5f5f5",
                        CardBackground = "White",
                        PrimaryColor = "#2e7d32",
                        TextColor = "#2c3e50",
                        SecondaryTextColor = "#6c757d"
                    };
                case "Purple":
                    return new ThemeColors
                    {
                        Background = "#f5f5f5",
                        CardBackground = "White",
                        PrimaryColor = "#7b1fa2",
                        TextColor = "#2c3e50",
                        SecondaryTextColor = "#6c757d"
                    };
                default: // Light
                    return new ThemeColors
                    {
                        Background = "#f5f5f5",
                        CardBackground = "White",
                        PrimaryColor = "#2c3e50",
                        TextColor = "#2c3e50",
                        SecondaryTextColor = "#6c757d"
                    };
            }
        }

        private class ThemeColors
        {
            public string Background { get; set; }
            public string CardBackground { get; set; }
            public string PrimaryColor { get; set; }
            public string TextColor { get; set; }
            public string SecondaryTextColor { get; set; }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TaskNameTextBox.Text))
            {
                CustomMessageBox.Show("任务名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TaskName = TaskNameTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TaskNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}
