using System;
using System.Windows;
using System.Windows.Media;

namespace WorkTimeWPF
{
    public partial class CustomMessageBox : Window
    {
        private string _messageBoxText;
        private string _caption;
        private MessageBoxButton _button;
        private MessageBoxImage _icon;

        public static MessageBoxResult Show(string messageBoxText, string caption = "消息", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            try
            {
                var messageBox = new CustomMessageBox();
                messageBox._messageBoxText = messageBoxText;
                messageBox._caption = caption;
                messageBox._button = button;
                messageBox._icon = icon;
                
                // 设置Owner为主窗口，但要避免设置为自身
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null && mainWindow != messageBox)
                {
                    messageBox.Owner = mainWindow;
                }
                
                // 调试信息
                System.Diagnostics.Debug.WriteLine($"Show called with messageBoxText: {messageBoxText}");
                System.Diagnostics.Debug.WriteLine($"Show called with caption: {caption}");
                
                messageBox.ShowDialog();
                return messageBox.Result;
            }
            catch (Exception ex)
            {
                // 如果自定义消息框失败，退回系统默认消息框
                System.Diagnostics.Debug.WriteLine($"CustomMessageBox failed: {ex.Message}");
                try
                {
                    return System.Windows.MessageBox.Show(messageBoxText, caption, button, icon);
                }
                catch
                {
                    // 如果系统消息框也失败，返回默认结果
                    return MessageBoxResult.OK;
                }
            }
        }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.OK;

        public CustomMessageBox()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 调试信息
            System.Diagnostics.Debug.WriteLine($"Window_Loaded called with _messageBoxText: {_messageBoxText}");
            System.Diagnostics.Debug.WriteLine($"Window_Loaded called with _caption: {_caption}");
            
            // 设置窗口标题
            Title = _caption ?? "消息";
            
            // 设置消息内容
            if (MessageText != null)
            {
                string messageText = _messageBoxText ?? "这是一条测试消息";
                MessageText.Text = messageText;
                MessageText.Foreground = Brushes.Black;
                MessageText.Visibility = Visibility.Visible;
                MessageText.FontSize = 13;
                MessageText.TextWrapping = TextWrapping.Wrap;
                
                // 调试信息
                System.Diagnostics.Debug.WriteLine($"MessageText set to: {messageText}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("MessageText is null!");
            }
            
            // 设置消息类型（图标）
            SetMessageType(_icon);
            
            // 设置按钮
            SetupButtons(_button);
        }

        private void SetMessageType(MessageBoxImage icon)
        {
            if (MessageIcon == null) return;
            
            switch (icon)
            {
                case MessageBoxImage.Error:
                    MessageIcon.Text = "⚠";
                    break;
                case MessageBoxImage.Warning:
                    MessageIcon.Text = "⚠";
                    break;
                case MessageBoxImage.Question:
                    MessageIcon.Text = "?";
                    break;
                case MessageBoxImage.Information:
                default:
                    MessageIcon.Text = "i";
                    break;
            }
        }

        private void SetupButtons(MessageBoxButton button)
        {
            // 检查按钮是否存在
            if (OkButton == null || CancelButton == null || YesButton == null || NoButton == null)
                return;
                
            // 隐藏所有按钮
            OkButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;
            YesButton.Visibility = Visibility.Collapsed;
            NoButton.Visibility = Visibility.Collapsed;

            switch (button)
            {
                case MessageBoxButton.OK:
                    OkButton.Visibility = Visibility.Visible;
                    OkButton.IsDefault = true;
                    break;
                case MessageBoxButton.OKCancel:
                    OkButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    OkButton.IsDefault = true;
                    CancelButton.IsCancel = true;
                    break;
                case MessageBoxButton.YesNo:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    YesButton.IsDefault = true;
                    break;
                case MessageBoxButton.YesNoCancel:
                    YesButton.Visibility = Visibility.Visible;
                    NoButton.Visibility = Visibility.Visible;
                    CancelButton.Visibility = Visibility.Visible;
                    YesButton.IsDefault = true;
                    CancelButton.IsCancel = true;
                    break;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            DialogResult = false;
            Close();
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            DialogResult = false;
            Close();
        }
    }
}