using System.Windows;
using System.Windows.Input;

namespace WorkTimeWPF
{
    public partial class TaskInputDialog : Window
    {
        public string TaskName { get; private set; }

        public TaskInputDialog()
        {
            InitializeComponent();
            TaskNameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TaskNameTextBox.Text))
            {
                MessageBox.Show("任务名称不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
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
