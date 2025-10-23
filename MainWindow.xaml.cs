using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using WorkTimeWPF.Models;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;

namespace WorkTimeWPF
{
    public partial class MainWindow : Window
    {
        private DatabaseManager _databaseManager;
        private DispatcherTimer _timer;
        private DispatcherTimer _currentTimeTimer;
        private Task _selectedTask;
        private TimeRecord _activeTimer;
        private bool _timerRunning = false;

        // 图表数据属性
        public SeriesCollection TaskComparisonSeries { get; set; }
        public SeriesCollection TotalDurationSeries { get; set; }
        public string[] TaskLabels { get; set; }
        public string[] TimeLabels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            InitializeApplication();
        }

        private void InitializeApplication()
        {
            try
            {
                // 初始化数据库
                _databaseManager = new DatabaseManager();

                // 初始化图表数据
                InitializeCharts();

                // 初始化计时器
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += Timer_Tick;

                // 初始化当前时间更新计时器
                _currentTimeTimer = new DispatcherTimer();
                _currentTimeTimer.Interval = TimeSpan.FromSeconds(1);
                _currentTimeTimer.Tick += CurrentTimeTimer_Tick;
                _currentTimeTimer.Start();

                // 加载数据
                LoadTasks();
                LoadCompletedTasks();
                LoadTaskStatistics();
                CheckActiveTimer();
                UpdateCurrentTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用程序初始化失败: {ex.Message}\n\n程序将尝试修复数据库并重新启动。", 
                    "初始化错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // 尝试重新初始化数据库
                try
                {
                    _databaseManager = new DatabaseManager();
                    LoadTasks();
                    LoadCompletedTasks();
                    LoadTaskStatistics();
                    CheckActiveTimer();
                    UpdateCurrentTime();
                }
                catch (Exception retryEx)
                {
                    MessageBox.Show($"数据库修复失败: {retryEx.Message}\n\n请检查数据库文件是否损坏。", 
                        "数据库错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadTasks()
        {
            try
            {
                if (_databaseManager == null)
                {
                    if (TasksDataGrid != null)
                        TasksDataGrid.ItemsSource = new List<Task>();
                    return;
                }

                var tasks = _databaseManager.GetTasks();
                if (TasksDataGrid != null)
                {
                    TasksDataGrid.ItemsSource = tasks;

                    // 添加排序事件处理
                    foreach (var column in TasksDataGrid.Columns)
                    {
                        column.SortDirection = null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载任务列表时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCompletedTasks()
        {
            try
            {
                if (_databaseManager == null)
                {
                    if (CompletedTasksDataGrid != null)
                        CompletedTasksDataGrid.ItemsSource = new List<TaskStatistics>();
                    return;
                }

                var statistics = _databaseManager.GetTaskStatistics();
                if (statistics != null)
                {
                    var completedTasks = statistics
                        .Where(t => t.TaskStatus == "completed")
                        .OrderByDescending(t => t.CompletedAt)
                        .ToList();
                    if (CompletedTasksDataGrid != null)
                        CompletedTasksDataGrid.ItemsSource = completedTasks;
                }
                else
                {
                    if (CompletedTasksDataGrid != null)
                        CompletedTasksDataGrid.ItemsSource = new List<TaskStatistics>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载已完成任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTimeRecords(int taskId)
        {
            try
            {
                if (_databaseManager == null)
                {
                    if (TimeRecordsDataGrid != null)
                        TimeRecordsDataGrid.ItemsSource = new List<TimeRecord>();
                    if (TotalDurationLabel != null)
                        TotalDurationLabel.Text = "任务总耗时: 00:00:00";
                    return;
                }

                var records = _databaseManager.GetTimeRecords(taskId);
                if (TimeRecordsDataGrid != null)
                    TimeRecordsDataGrid.ItemsSource = records;

                // 计算总耗时
                if (records != null)
                {
                    var totalDuration = records.Where(r => r.Duration.HasValue).Sum(r => r.Duration.Value);
                    var hours = totalDuration / 3600;
                    var minutes = (totalDuration % 3600) / 60;
                    var seconds = totalDuration % 60;
                    if (TotalDurationLabel != null)
                        TotalDurationLabel.Text = $"任务总耗时: {hours:00}:{minutes:00}:{seconds:00}";
                }
                else
                {
                    if (TotalDurationLabel != null)
                        TotalDurationLabel.Text = "任务总耗时: 00:00:00";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载时间记录时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckActiveTimer()
        {
            try
            {
                if (_databaseManager == null)
                {
                    UpdateActiveTaskStatus();
                    return;
                }

                _activeTimer = _databaseManager.GetActiveTimer();
                if (_activeTimer != null)
                {
                    // 选中对应的任务
                    var tasks = _databaseManager.GetTasks();
                    if (tasks != null)
                    {
                        var task = tasks.FirstOrDefault(t => t.TaskId == _activeTimer.TaskId);
                        if (task != null && TasksDataGrid != null)
                        {
                            TasksDataGrid.SelectedItem = task;
                            _selectedTask = task;
                            UpdateTaskDetails();
                        }
                    }
                    StartTimerUpdate();
                }
                UpdateActiveTaskStatus();
                UpdateTodayTotalTime();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查活动计时器时发生错误: {ex.Message}");
                UpdateActiveTaskStatus();
            }
        }

        private void StartTimerUpdate()
        {
            _timerRunning = true;
            _timer.Start();
        }

        private void StopTimerUpdate()
        {
            _timerRunning = false;
            _timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_activeTimer != null && _timerRunning)
            {
                var elapsed = DateTime.Now - _activeTimer.StartTime;
                var hours = (int)elapsed.TotalHours;
                var minutes = elapsed.Minutes;
                var seconds = elapsed.Seconds;
                TimerDisplay.Text = $"{hours:00}:{minutes:00}:{seconds:00}";
            }
        }

        private void CurrentTimeTimer_Tick(object sender, EventArgs e)
        {
            CurrentTimeLabel.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void UpdateCurrentTime()
        {
            CurrentTimeLabel.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private void UpdateTaskDetails()
        {
            if (_selectedTask != null)
            {
                TaskNameLabel.Text = _selectedTask.TaskName;
                TaskStatusLabel.Text = $"状态: {_selectedTask.StatusDisplayName}";
                
                // 更新计时器按钮状态
                if (_selectedTask.TaskStatus == "completed" || _selectedTask.TaskStatus == "deleted")
                {
                    TimerButton.IsEnabled = false;
                    CompleteTaskButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    TimerButton.IsEnabled = true;
                    if (_selectedTask.TaskStatus == "in_progress" && _activeTimer != null && _activeTimer.TaskId == _selectedTask.TaskId)
                    {
                        TimerButton.Content = "暂停计时";
                        if (!_timerRunning)
                        {
                            StartTimerUpdate();
                        }
                    }
                    else
                    {
                        TimerButton.Content = "开始计时";
                        if (_timerRunning)
                        {
                            StopTimerUpdate();
                        }
                    }
                    
                    // 显示完成按钮（仅对非已完成任务）
                    if (_selectedTask.TaskStatus != "completed")
                    {
                        CompleteTaskButton.Visibility = Visibility.Visible;
                        CompleteTaskButton.IsEnabled = true;
                    }
                    else
                    {
                        CompleteTaskButton.Visibility = Visibility.Collapsed;
                        CompleteTaskButton.IsEnabled = false;
                    }
                }

                // 加载时间记录
                LoadTimeRecords(_selectedTask.TaskId);
            }
            else
            {
                TaskNameLabel.Text = "未选择任务";
                TaskStatusLabel.Text = "";
                TimerButton.IsEnabled = false;
                TimerButton.Content = "开始计时";
                CompleteTaskButton.Visibility = Visibility.Collapsed;
                TimerDisplay.Text = "00:00:00";
                TotalDurationLabel.Text = "任务总耗时: 00:00:00";
                TimeRecordsDataGrid.ItemsSource = null;
            }
        }

        private void UpdateActiveTaskStatus()
        {
            if (_activeTimer != null)
            {
                ActiveTaskLabel.Text = $"当前活动任务: {_activeTimer.TaskName}";
            }
            else
            {
                ActiveTaskLabel.Text = "当前活动任务: 无";
            }
        }

        private void UpdateTodayTotalTime()
        {
            try
            {
                if (_databaseManager == null)
                {
                    if (TotalTimeLabel != null)
                        TotalTimeLabel.Text = "总工作时间: 00:00:00";
                    return;
                }

                var (startDate, endDate) = GetSelectedTimeRange();
                var records = _databaseManager.GetTimeRecords(null, startDate, endDate);
                var totalDuration = records.Where(r => r.Duration.HasValue).Sum(r => r.Duration.Value);
                
                // 如果有正在进行的计时器，加上当前时间
                if (_activeTimer != null)
                {
                    totalDuration += (int)(DateTime.Now - _activeTimer.StartTime).TotalSeconds;
                }
                
                var hours = totalDuration / 3600;
                var minutes = (totalDuration % 3600) / 60;
                var seconds = totalDuration % 60;
                if (TotalTimeLabel != null)
                    TotalTimeLabel.Text = $"总工作时间: {hours:00}:{minutes:00}:{seconds:00}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新总时间时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (DateTime startDate, DateTime endDate) GetSelectedTimeRange()
        {
            var selectedPeriod = "今日";
            if (TimePeriodComboBox != null && TimePeriodComboBox.SelectedItem != null)
            {
                selectedPeriod = ((ComboBoxItem)TimePeriodComboBox.SelectedItem)?.Content?.ToString() ?? "今日";
            }
            var now = DateTime.Now;
            
            switch (selectedPeriod)
            {
                case "今日":
                    return (now.Date, now.Date.AddDays(1).AddSeconds(-1));
                case "昨日":
                    var yesterday = now.Date.AddDays(-1);
                    return (yesterday, yesterday.AddDays(1).AddSeconds(-1));
                case "本周":
                    var monday = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday);
                    return (monday, now.Date.AddDays(1).AddSeconds(-1));
                case "上周":
                    var lastMonday = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday - 7);
                    var lastSunday = lastMonday.AddDays(6);
                    return (lastMonday, lastSunday.AddDays(1).AddSeconds(-1));
                case "本月":
                    var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
                    return (firstDayOfMonth, now.Date.AddDays(1).AddSeconds(-1));
                case "上月":
                    var firstDayOfLastMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
                    var lastDayOfLastMonth = new DateTime(now.Year, now.Month, 1).AddDays(-1);
                    return (firstDayOfLastMonth, lastDayOfLastMonth.AddDays(1).AddSeconds(-1));
                case "今年":
                    var firstDayOfYear = new DateTime(now.Year, 1, 1);
                    return (firstDayOfYear, now.Date.AddDays(1).AddSeconds(-1));
                case "去年":
                    var firstDayOfLastYear = new DateTime(now.Year - 1, 1, 1);
                    var lastDayOfLastYear = new DateTime(now.Year - 1, 12, 31);
                    return (firstDayOfLastYear, lastDayOfLastYear.AddDays(1).AddSeconds(-1));
                default:
                    return (now.Date, now.Date.AddDays(1).AddSeconds(-1));
            }
        }

        private void LoadTaskStatistics()
        {
            try
            {
                // 检查数据库管理器是否已初始化
                if (_databaseManager == null)
                {
                    if (TaskStatisticsDataGrid != null)
                        TaskStatisticsDataGrid.ItemsSource = new List<TaskStatistics>();
                    if (TaskCountLabel != null)
                        TaskCountLabel.Text = "任务数量: 0";
                    if (CompletedTaskCountLabel != null)
                        CompletedTaskCountLabel.Text = "已完成: 0";
                    return;
                }

                var (startDate, endDate) = GetSelectedTimeRange();
                var statistics = _databaseManager.GetTaskStatistics();
                
                if (statistics != null)
                {
                    var filteredStatistics = statistics
                        .Where(s => s.TotalDurationSeconds > 0)
                        .OrderByDescending(s => s.TotalDurationSeconds)
                        .ToList();
                    
                    if (TaskStatisticsDataGrid != null)
                        TaskStatisticsDataGrid.ItemsSource = filteredStatistics;
                }
                else
                {
                    if (TaskStatisticsDataGrid != null)
                        TaskStatisticsDataGrid.ItemsSource = new List<TaskStatistics>();
                }
                
                // 更新任务统计信息
                try
                {
                    var totalTasks = _databaseManager.GetTasks().Count;
                    var completedTasks = _databaseManager.GetTasks("completed").Count;
                    if (TaskCountLabel != null)
                        TaskCountLabel.Text = $"任务数量: {totalTasks}";
                    if (CompletedTaskCountLabel != null)
                        CompletedTaskCountLabel.Text = $"已完成: {completedTasks}";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新任务统计信息失败: {ex.Message}");
                    if (TaskCountLabel != null)
                        TaskCountLabel.Text = "任务数量: 0";
                    if (CompletedTaskCountLabel != null)
                        CompletedTaskCountLabel.Text = "已完成: 0";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载任务统计时发生错误: {ex.Message}\n\n这可能是由于数据库结构不兼容导致的。\n程序将尝试自动修复数据库结构。", 
                    "数据库错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // 清空统计显示
                if (TaskStatisticsDataGrid != null)
                    TaskStatisticsDataGrid.ItemsSource = new List<TaskStatistics>();
                if (TaskCountLabel != null)
                    TaskCountLabel.Text = "任务数量: 0";
                if (CompletedTaskCountLabel != null)
                    CompletedTaskCountLabel.Text = "已完成: 0";
            }
        }

        private void TimePeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadTaskStatistics();
            UpdateTodayTotalTime();
            UpdateCharts();
        }

        private void ChartTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 图表类型选择变化时更新总时长统计图
            UpdateTotalDurationChart();
        }

        private void RefreshStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTaskStatistics();
            UpdateTodayTotalTime();
            UpdateCharts();
        }

        private void InitializeCharts()
        {
            // 初始化图表数据
            TaskComparisonSeries = new SeriesCollection();
            TotalDurationSeries = new SeriesCollection();
            TaskLabels = new string[0];
            TimeLabels = new string[0];
            YFormatter = value => $"{value:F1}小时";

            // 设置数据上下文
            DataContext = this;
        }

        private void UpdateCharts()
        {
            try
            {
                if (_databaseManager == null) return;
                if (TaskComparisonSeries == null || TotalDurationSeries == null) return;

                // 更新任务对比图
                UpdateTaskComparisonChart();
                
                // 更新总时长统计图
                UpdateTotalDurationChart();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新图表时发生错误: {ex.Message}");
            }
        }

        private void UpdateTaskComparisonChart()
        {
            try
            {
                var statistics = _databaseManager.GetTaskStatistics();
                if (statistics == null || !statistics.Any())
                {
                    TaskComparisonSeries.Clear();
                    return;
                }

                var chartData = statistics
                    .Where(s => s.TotalDurationSeconds > 0)
                    .OrderByDescending(s => s.TotalDurationSeconds)
                    .Take(10) // 只显示前10个任务
                    .ToList();

                if (!chartData.Any())
                {
                    TaskComparisonSeries.Clear();
                    return;
                }

                TaskLabels = chartData.Select(s => s.TaskName).ToArray();
                
                var columnSeries = new ColumnSeries
                {
                    Title = "任务耗时",
                    Values = new ChartValues<double>(chartData.Select(s => s.TotalDurationSeconds / 3600.0)),
                    Fill = System.Windows.Media.Brushes.LightBlue
                };

                if (TaskComparisonSeries != null)
                {
                    TaskComparisonSeries.Clear();
                    TaskComparisonSeries.Add(columnSeries);
                }

                // 直接设置图表控件的属性
                if (TaskComparisonChart != null)
                {
                    TaskComparisonChart.Series = TaskComparisonSeries;
                    if (TaskComparisonChart.AxisX.Count > 0)
                    {
                        TaskComparisonChart.AxisX[0].Labels = TaskLabels;
                    }
                    if (TaskComparisonChart.AxisY.Count > 0)
                    {
                        TaskComparisonChart.AxisY[0].LabelFormatter = YFormatter;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新任务对比图时发生错误: {ex.Message}");
            }
        }

        private void UpdateTotalDurationChart()
        {
            try
            {
                var (startDate, endDate) = GetSelectedTimeRange();
                var selectedPeriod = "今日";
                if (TimePeriodComboBox != null && TimePeriodComboBox.SelectedItem != null)
                {
                    selectedPeriod = ((ComboBoxItem)TimePeriodComboBox.SelectedItem)?.Content?.ToString() ?? "今日";
                }

                var chartData = new List<(string Label, double Hours)>();
                
                switch (selectedPeriod)
                {
                    case "今日":
                        // 按小时统计
                        for (int hour = 0; hour < 24; hour++)
                        {
                            var hourStart = startDate.Date.AddHours(hour);
                            var hourEnd = hourStart.AddHours(1);
                            var duration = GetDurationInRange(hourStart, hourEnd);
                            chartData.Add(($"{hour:00}:00", duration / 3600.0));
                        }
                        break;
                    case "本周":
                    case "上周":
                        // 按天统计
                        for (int day = 0; day < 7; day++)
                        {
                            var dayStart = startDate.Date.AddDays(day);
                            var dayEnd = dayStart.AddDays(1);
                            var duration = GetDurationInRange(dayStart, dayEnd);
                            chartData.Add((dayStart.ToString("MM/dd"), duration / 3600.0));
                        }
                        break;
                    case "本月":
                    case "上月":
                        // 按天统计
                        var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
                        for (int day = 1; day <= daysInMonth; day++)
                        {
                            var dayStart = new DateTime(startDate.Year, startDate.Month, day);
                            var dayEnd = dayStart.AddDays(1);
                            var duration = GetDurationInRange(dayStart, dayEnd);
                            chartData.Add((dayStart.ToString("MM/dd"), duration / 3600.0));
                        }
                        break;
                    case "今年":
                    case "去年":
                        // 按月统计
                        for (int month = 1; month <= 12; month++)
                        {
                            var monthStart = new DateTime(startDate.Year, month, 1);
                            var monthEnd = monthStart.AddMonths(1);
                            var duration = GetDurationInRange(monthStart, monthEnd);
                            chartData.Add((monthStart.ToString("MM月"), duration / 3600.0));
                        }
                        break;
                }

                TimeLabels = chartData.Select(d => d.Label).ToArray();
                
                var chartType = "柱状图";
                if (ChartTypeComboBox != null && ChartTypeComboBox.SelectedItem != null)
                {
                    chartType = ((ComboBoxItem)ChartTypeComboBox.SelectedItem)?.Content?.ToString() ?? "柱状图";
                }

                Series series;
                if (chartType == "折线图")
                {
                    series = new LineSeries
                    {
                        Title = "工作时长",
                        Values = new ChartValues<double>(chartData.Select(d => d.Hours)),
                        Fill = System.Windows.Media.Brushes.Transparent,
                        Stroke = System.Windows.Media.Brushes.Blue,
                        StrokeThickness = 2
                    };
                }
                else
                {
                    series = new ColumnSeries
                    {
                        Title = "工作时长",
                        Values = new ChartValues<double>(chartData.Select(d => d.Hours)),
                        Fill = System.Windows.Media.Brushes.LightGreen
                    };
                }

                if (TotalDurationSeries != null)
                {
                    TotalDurationSeries.Clear();
                    TotalDurationSeries.Add(series);
                }

                // 直接设置图表控件的属性
                if (TotalDurationChart != null)
                {
                    TotalDurationChart.Series = TotalDurationSeries;
                    if (TotalDurationChart.AxisX.Count > 0)
                    {
                        TotalDurationChart.AxisX[0].Labels = TimeLabels;
                    }
                    if (TotalDurationChart.AxisY.Count > 0)
                    {
                        TotalDurationChart.AxisY[0].LabelFormatter = YFormatter;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新总时长统计图时发生错误: {ex.Message}");
            }
        }

        private double GetDurationInRange(DateTime start, DateTime end)
        {
            try
            {
                if (_databaseManager == null) return 0;

                var records = _databaseManager.GetTimeRecords(null, start, end);
                return records?.Sum(r => r.Duration ?? 0) ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // 事件处理方法
        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new TaskInputDialog();
            if (inputDialog.ShowDialog() == true)
            {
                try
                {
                    var taskId = _databaseManager.AddTask(inputDialog.TaskName);
                    LoadTasks();
                    LoadCompletedTasks();
                    LoadTaskStatistics();
                    
                    // 选中新添加的任务
                    var tasks = _databaseManager.GetTasks();
                    var newTask = tasks.FirstOrDefault(t => t.TaskId == taskId);
                    if (newTask != null)
                    {
                        TasksDataGrid.SelectedItem = newTask;
                        _selectedTask = newTask;
                        UpdateTaskDetails();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadTasks();
            LoadCompletedTasks();
            LoadTaskStatistics();
            CheckActiveTimer();
        }

        private void RefreshCompletedButton_Click(object sender, RoutedEventArgs e)
        {
            LoadCompletedTasks();
        }

        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要删除任务 '{_selectedTask.TaskName}' 吗?\n注意：删除后无法恢复！", 
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 如果有正在进行的计时器，先暂停
                    if (_activeTimer != null && _activeTimer.TaskId == _selectedTask.TaskId)
                    {
                        _databaseManager.PauseTimer(_activeTimer.RecordId);
                        _activeTimer = null;
                        StopTimerUpdate();
                        TimerDisplay.Text = "00:00:00";
                    }

                    _databaseManager.DeleteTask(_selectedTask.TaskId);
                    LoadTasks();
                    LoadCompletedTasks();
                    LoadTaskStatistics();
                    UpdateActiveTaskStatus();
                    UpdateTodayTotalTime();
                    
                    // 清空任务详情
                    _selectedTask = null;
                    UpdateTaskDetails();
                    
                    MessageBox.Show("任务已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TasksDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TasksDataGrid.SelectedItem is Task task)
            {
                _selectedTask = task;
                UpdateTaskDetails();
            }
            
            // 更新删除按钮状态
            DeleteTaskButton.IsEnabled = TasksDataGrid.SelectedItem != null;
        }

        private void TasksDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // 这里可以添加自定义排序逻辑
            // 默认情况下，WPF DataGrid会自动处理排序
        }

        private void TimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_databaseManager == null)
            {
                MessageBox.Show("数据库未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                if (_activeTimer != null)
                {
                    if (_activeTimer.TaskId == _selectedTask.TaskId)
                    {
                        // 暂停当前任务
                        _databaseManager.PauseTimer(_activeTimer.RecordId);
                        _activeTimer = null;
                        if (TimerButton != null)
                            TimerButton.Content = "开始计时";
                        StopTimerUpdate();
                        if (TimerDisplay != null)
                            TimerDisplay.Text = "00:00:00";
                    }
                    else
                    {
                        // 切换到新任务
                        var result = MessageBox.Show($"当前正在计时: {_activeTimer.TaskName}\n是否要切换到新任务?", 
                            "切换任务", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            _databaseManager.PauseTimer(_activeTimer.RecordId);
                            var recordId = _databaseManager.StartTimer(_selectedTask.TaskId);
                            _activeTimer = _databaseManager.GetActiveTimer();
                            if (TimerButton != null)
                                TimerButton.Content = "暂停计时";
                            StartTimerUpdate();
                        }
                    }
                }
                else
                {
                    // 开始新的计时器
                    var recordId = _databaseManager.StartTimer(_selectedTask.TaskId);
                    _activeTimer = _databaseManager.GetActiveTimer();
                    if (TimerButton != null)
                        TimerButton.Content = "暂停计时";
                    StartTimerUpdate();
                }

                LoadTasks();
                LoadTimeRecords(_selectedTask.TaskId);
                UpdateActiveTaskStatus();
                UpdateTodayTotalTime();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作计时器时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedTask.TaskStatus == "completed")
            {
                MessageBox.Show("该任务已经完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要将任务 '{_selectedTask.TaskName}' 标记为已完成吗?\n\n这将停止当前计时并将任务移到已完成列表中。", 
                "确认完成", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 如果有正在进行的计时器，先暂停
                    if (_activeTimer != null && _activeTimer.TaskId == _selectedTask.TaskId)
                    {
                        _databaseManager.PauseTimer(_activeTimer.RecordId);
                        _activeTimer = null;
                        StopTimerUpdate();
                        TimerDisplay.Text = "00:00:00";
                    }

                    // 更新任务状态为已完成
                    _databaseManager.UpdateTaskStatus(_selectedTask.TaskId, "completed");
                    
                    // 刷新所有相关数据
                    LoadTasks();
                    LoadCompletedTasks();
                    LoadTaskStatistics();
                    UpdateCharts();
                    
                    // 更新任务详情显示
                    if (_selectedTask != null)
                    {
                        _selectedTask.TaskStatus = "completed";
                        UpdateTaskDetails();
                    }
                    
                    UpdateActiveTaskStatus();
                    UpdateTodayTotalTime();
                    
                    MessageBox.Show("任务已标记为完成并移到已完成列表", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"完成任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TimeRecordsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SaveNotesButton.IsEnabled = TimeRecordsDataGrid.SelectedItem != null;
        }

        private void SaveNotesButton_Click(object sender, RoutedEventArgs e)
        {
            if (TimeRecordsDataGrid.SelectedItem is TimeRecord record)
            {
                try
                {
                    _databaseManager.UpdateRecordNotes(record.RecordId, NotesTextBox.Text);
                    LoadTimeRecords(_selectedTask.TaskId);
                    NotesTextBox.Clear();
                    MessageBox.Show("备注已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存备注时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|所有文件 (*.*)|*.*",
                    Title = "导出数据"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var today = DateTime.Today;
                    var records = _databaseManager.GetTimeRecords(null, today, today.AddDays(1).AddSeconds(-1));
                    
                    using (var writer = new StreamWriter(saveFileDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine("任务名称,开始时间,结束时间,持续时间,备注");
                        
                        var allTasks = _databaseManager.GetTasks();
                        foreach (var record in records)
                        {
                            var task = allTasks.FirstOrDefault(t => t.TaskId == record.TaskId);
                            var taskName = task?.TaskName ?? "未知任务";
                            var endTime = record.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "进行中";
                            var duration = record.DurationDisplay;
                            var notes = record.Notes ?? "";
                            
                            writer.WriteLine($"\"{taskName}\",\"{record.StartTime:yyyy-MM-dd HH:mm:ss}\",\"{endTime}\",\"{duration}\",\"{notes}\"");
                        }
                    }
                    
                    MessageBox.Show($"数据已成功导出到: {saveFileDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出数据时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleTimeRecordsButton_Click(object sender, RoutedEventArgs e)
        {
            if (TimeRecordsScrollViewer.Visibility == Visibility.Visible)
            {
                TimeRecordsScrollViewer.Visibility = Visibility.Collapsed;
                TimeRecordsHeaderGrid.Visibility = Visibility.Collapsed;
                ToggleTimeRecordsButton.Content = "展开";
            }
            else
            {
                TimeRecordsScrollViewer.Visibility = Visibility.Visible;
                TimeRecordsHeaderGrid.Visibility = Visibility.Visible;
                ToggleTimeRecordsButton.Content = "折叠";
            }
        }

        private void ToggleStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            if (StatisticsTabControl.Visibility == Visibility.Visible)
            {
                StatisticsTabControl.Visibility = Visibility.Collapsed;
                QuickStatsGrid.Visibility = Visibility.Collapsed;
                StatisticsControlsGrid.Visibility = Visibility.Collapsed;
                ToggleStatisticsButton.Content = "展开";
            }
            else
            {
                StatisticsTabControl.Visibility = Visibility.Visible;
                QuickStatsGrid.Visibility = Visibility.Visible;
                StatisticsControlsGrid.Visibility = Visibility.Visible;
                ToggleStatisticsButton.Content = "折叠";
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 检查是否有正在进行的计时器
            if (_activeTimer != null)
            {
                var result = MessageBox.Show("有任务正在计时，确定要关闭应用程序吗?", 
                    "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _databaseManager.PauseTimer(_activeTimer.RecordId);
                }
                else
                {
                    return;
                }
            }

            _timer?.Stop();
            _currentTimeTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
