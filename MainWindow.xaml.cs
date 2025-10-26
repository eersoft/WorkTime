using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
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
        // 常量定义
        private const string WUAI_POJIE_URL = "http://www.eersoft.top";
        
        private DatabaseManager _databaseManager;
        private DispatcherTimer _timer;
        private DispatcherTimer _currentTimeTimer;
        private Task _selectedTask;
        private TimeRecord _activeTimer;
        private bool _timerRunning = false;
        private string _currentTheme = "Light";

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

                // 初始化主题
                InitializeTheme();

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
                RefreshAllData();
                CheckActiveTimer();
                UpdateCurrentTime();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"应用程序初始化失败: {ex.Message}\n\n程序将尝试修复数据库并重新启动。", 
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
                    CustomMessageBox.Show($"数据库修复失败: {retryEx.Message}\n\n请检查数据库文件是否损坏。", 
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

                var allTasks = _databaseManager.GetTasks();
                // 只显示未完成的任务（状态不是"completed"的任务）
                var activeTasks = allTasks.Where(t => t.TaskStatus != "completed").ToList();
                
                if (TasksDataGrid != null)
                {
                    TasksDataGrid.ItemsSource = activeTasks;

                    // 添加排序事件处理
                    foreach (var column in TasksDataGrid.Columns)
                    {
                        column.SortDirection = null;
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"加载任务列表时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                CustomMessageBox.Show($"加载已完成任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                CustomMessageBox.Show($"加载时间记录时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        UpdateTimerButtonContent("暂停计时");
                        if (!_timerRunning)
                        {
                            StartTimerUpdate();
                        }
                    }
                    else
                    {
                        UpdateTimerButtonContent("开始计时");
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
                UpdateTimerButtonContent("开始计时");
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
                CustomMessageBox.Show($"更新总时间时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新所有UI数据和控件
        /// </summary>
        private void RefreshAllData()
        {
            try
            {
                // 刷新任务列表
                LoadTasks();
                LoadCompletedTasks();
                LoadTaskStatistics();
                
                // 刷新时间记录（如果有选中的任务）
                if (_selectedTask != null)
                {
                    LoadTimeRecords(_selectedTask.TaskId);
                }
                
                // 刷新统计信息
                UpdateTodayTotalTime();
                UpdateCharts();
                
                // 刷新任务状态
                UpdateActiveTaskStatus();
            }
            catch (Exception ex)
            {
                // 静默处理刷新错误，避免影响用户体验
                System.Diagnostics.Debug.WriteLine($"刷新数据时发生错误: {ex.Message}");
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
                    // 计算本周一的日期
                    var daysFromMonday = ((int)now.DayOfWeek + 6) % 7; // 星期日=0, 星期一=0, 星期二=1, ..., 星期六=6
                    var monday = now.Date.AddDays(-daysFromMonday);
                    return (monday, now.Date.AddDays(1).AddSeconds(-1));
                case "上周":
                    // 计算上周一的日期
                    var daysFromLastMonday = ((int)now.DayOfWeek + 6) % 7 + 7; // 加上7天得到上周一
                    var lastMonday = now.Date.AddDays(-daysFromLastMonday);
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
                CustomMessageBox.Show($"加载任务统计时发生错误: {ex.Message}\n\n这可能是由于数据库结构不兼容导致的。\n程序将尝试自动修复数据库结构。", 
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
            inputDialog.Owner = this;
            if (inputDialog.ShowDialog() == true)
            {
                try
                {
                    var taskId = _databaseManager.AddTask(inputDialog.TaskName);
                    RefreshAllData();
                    
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
                    CustomMessageBox.Show($"添加任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                CustomMessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = CustomMessageBox.Show($"确定要删除任务 '{_selectedTask.TaskName}' 吗?\n注意：删除后无法恢复！", 
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
                    RefreshAllData();
                    
                    // 清空任务详情
                    _selectedTask = null;
                    UpdateTaskDetails();
                    
                    CustomMessageBox.Show("任务已删除", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"删除任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                CustomMessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_databaseManager == null)
            {
                CustomMessageBox.Show("数据库未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                            UpdateTimerButtonContent("开始计时");
                        StopTimerUpdate();
                        if (TimerDisplay != null)
                            TimerDisplay.Text = "00:00:00";
                    }
                    else
                    {
                        // 切换到新任务
                        var result = CustomMessageBox.Show($"当前正在计时: {_activeTimer.TaskName}\n是否要切换到新任务?", 
                            "切换任务", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            _databaseManager.PauseTimer(_activeTimer.RecordId);
                            var recordId = _databaseManager.StartTimer(_selectedTask.TaskId);
                            _activeTimer = _databaseManager.GetActiveTimer();
                            if (TimerButton != null)
                                UpdateTimerButtonContent("暂停计时");
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
                        UpdateTimerButtonContent("暂停计时");
                    StartTimerUpdate();
                }

                // 自动刷新所有数据
                RefreshAllData();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"操作计时器时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CompleteTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                CustomMessageBox.Show("请先选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedTask.TaskStatus == "completed")
            {
                CustomMessageBox.Show("该任务已经完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = CustomMessageBox.Show($"确定要将任务 '{_selectedTask.TaskName}' 标记为已完成吗?\n\n这将停止当前计时并将任务移到已完成列表中。", 
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
                    RefreshAllData();
                    
                    // 更新任务详情显示
                    if (_selectedTask != null)
                    {
                        _selectedTask.TaskStatus = "completed";
                        UpdateTaskDetails();
                    }
                    
                    UpdateActiveTaskStatus();
                    UpdateTodayTotalTime();
                    
                    CustomMessageBox.Show("任务已标记为完成并移到已完成列表", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"完成任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    CustomMessageBox.Show("备注已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"保存备注时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    
                    CustomMessageBox.Show($"数据已成功导出到: {saveFileDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"导出数据时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleTimeRecordsButton_Click(object sender, RoutedEventArgs e)
        {
            if (TimeRecordsScrollViewer.Visibility == Visibility.Visible)
            {
                TimeRecordsScrollViewer.Visibility = Visibility.Collapsed;
                TimeRecordsHeaderGrid.Visibility = Visibility.Collapsed;
                UpdateToggleButtonContent(ToggleTimeRecordsButton, "📋", "▶");
            }
            else
            {
                TimeRecordsScrollViewer.Visibility = Visibility.Visible;
                TimeRecordsHeaderGrid.Visibility = Visibility.Visible;
                UpdateToggleButtonContent(ToggleTimeRecordsButton, "📋", "▼");
            }
        }

        private void ToggleStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            if (StatisticsTabControl.Visibility == Visibility.Visible)
            {
                StatisticsTabControl.Visibility = Visibility.Collapsed;
                StatisticsControlsGrid.Visibility = Visibility.Collapsed;
                UpdateToggleButtonContent(ToggleStatisticsButton, "📊", "▶");
            }
            else
            {
                StatisticsTabControl.Visibility = Visibility.Visible;
                StatisticsControlsGrid.Visibility = Visibility.Visible;
                UpdateToggleButtonContent(ToggleStatisticsButton, "📊", "▼");
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 检查是否有正在进行的计时器
            if (_activeTimer != null)
            {
                var result = CustomMessageBox.Show("有任务正在计时，确定要关闭应用程序吗?\n\n关闭后，计时将继续在后台进行。", 
                    "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // 用户确认关闭，不暂停计时，让计时继续在后台进行
                    System.Diagnostics.Debug.WriteLine($"关闭窗口，计时继续: {_activeTimer.TaskName}");
                }
                else
                {
                    // 取消关闭操作
                    e.Cancel = true;
                    return;
                }
            }

            // 停止定时器
            _timer?.Stop();
            _currentTimeTimer?.Stop();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 清理资源
            _timer?.Stop();
            _currentTimeTimer?.Stop();
            base.OnClosed(e);
        }

        #region 主题切换功能

        private void InitializeTheme()
        {
            // 从配置文件加载主题设置
            var config = ConfigManager.GetConfig();
            ApplyTheme(config.Theme);
            UpdateThemeButtonStates();
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagValue)
            {
                string themeName;
                
                // 如果Tag是"Selected"，说明这是当前已选中的主题按钮
                // 连续点击同一个主题按钮时，不需要重复应用主题
                if (tagValue == "Selected")
                {
                    return; // 直接返回，不执行任何操作
                }
                
                themeName = tagValue;
                
                ApplyTheme(themeName);
                UpdateThemeButtonStates();
                
                // 保存主题设置到配置文件
                ConfigManager.UpdateTheme(themeName);
            }
        }

        private void ApplyTheme(string themeName)
        {
            _currentTheme = themeName;
            
            // 定义各主题的颜色
            var themes = new Dictionary<string, ThemeColors>
            {
                ["Light"] = new ThemeColors
                {
                    Background = "#f5f5f5",
                    HeaderBackground = "#2c3e50",
                    CardBackground = "White",
                    StatusBarBackground = "#34495e",
                    PrimaryColor = "#2c3e50",
                    SuccessColor = "#28a745",
                    DangerColor = "#dc3545",
                    TextColor = "#2c3e50",
                    SecondaryTextColor = "#6c757d"
                },
                ["Blue"] = new ThemeColors
                {
                    Background = "#e3f2fd",
                    HeaderBackground = "#1976d2",
                    CardBackground = "#ffffff",
                    StatusBarBackground = "#1565c0",
                    PrimaryColor = "#1976d2",
                    SuccessColor = "#388e3c",
                    DangerColor = "#d32f2f",
                    TextColor = "#1565c0",
                    SecondaryTextColor = "#1976d2"
                },
                ["Green"] = new ThemeColors
                {
                    Background = "#e8f5e8",
                    HeaderBackground = "#2e7d32",
                    CardBackground = "#ffffff",
                    StatusBarBackground = "#1b5e20",
                    PrimaryColor = "#2e7d32",
                    SuccessColor = "#388e3c",
                    DangerColor = "#d32f2f",
                    TextColor = "#1b5e20",
                    SecondaryTextColor = "#2e7d32"
                },
                ["Purple"] = new ThemeColors
                {
                    Background = "#f3e5f5",
                    HeaderBackground = "#7b1fa2",
                    CardBackground = "#ffffff",
                    StatusBarBackground = "#4a148c",
                    PrimaryColor = "#7b1fa2",
                    SuccessColor = "#388e3c",
                    DangerColor = "#d32f2f",
                    TextColor = "#4a148c",
                    SecondaryTextColor = "#7b1fa2"
                }
            };

            if (themes.TryGetValue(themeName, out var colors))
            {
                ApplyColorsToUI(colors);
                UpdateButtonColors(colors);
            }
        }

        private void ApplyColorsToUI(ThemeColors colors)
        {
            // 应用主窗口背景色
            this.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(colors.Background);

            // 应用顶部标题栏背景色
            var headerBorder = FindName("HeaderBorder") as Border;
            if (headerBorder != null)
            {
                headerBorder.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(colors.HeaderBackground);
            }

            // 应用状态栏背景色
            var statusBarBorder = FindName("StatusBarBorder") as Border;
            if (statusBarBorder != null)
            {
                statusBarBorder.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(colors.StatusBarBackground);
            }

            // 更新按钮样式中的颜色
            UpdateButtonColors(colors);
        }


        private void UpdateThemeButtonStates()
        {
            // 重置所有按钮的选中状态
            LightThemeButton.Tag = LightThemeButton.Tag?.ToString() == "Selected" ? "Light" : LightThemeButton.Tag;
            BlueThemeButton.Tag = BlueThemeButton.Tag?.ToString() == "Selected" ? "Blue" : BlueThemeButton.Tag;
            GreenThemeButton.Tag = GreenThemeButton.Tag?.ToString() == "Selected" ? "Green" : GreenThemeButton.Tag;
            PurpleThemeButton.Tag = PurpleThemeButton.Tag?.ToString() == "Selected" ? "Purple" : PurpleThemeButton.Tag;

            // 设置当前主题按钮为选中状态
            switch (_currentTheme)
            {
                case "Light":
                    LightThemeButton.Tag = "Selected";
                    break;
                case "Blue":
                    BlueThemeButton.Tag = "Selected";
                    break;
                case "Green":
                    GreenThemeButton.Tag = "Selected";
                    break;
                case "Purple":
                    PurpleThemeButton.Tag = "Selected";
                    break;
            }
        }

        private void UpdateTimerButtonContent(string text)
        {
            if (TimerButton.Content is StackPanel stackPanel && stackPanel.Children.Count >= 2)
            {
                if (stackPanel.Children[1] is TextBlock textBlock)
                {
                    textBlock.Text = text;
                }
            }
            else
            {
                // 如果结构被破坏，重新创建
                TimerButton.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = "⏱️", FontSize = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
                    }
                };
            }
        }

        private void UpdateToggleButtonContent(Button button, string icon, string arrow)
        {
            if (button.Content is StackPanel stackPanel && stackPanel.Children.Count >= 2)
            {
                if (stackPanel.Children[0] is TextBlock iconBlock)
                {
                    iconBlock.Text = icon;
                }
                if (stackPanel.Children[1] is TextBlock arrowBlock)
                {
                    arrowBlock.Text = arrow;
                }
            }
            else
            {
                // 如果结构被破坏，重新创建
                button.Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children =
                    {
                        new TextBlock { Text = icon, FontSize = 12, Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock { Text = arrow, VerticalAlignment = VerticalAlignment.Center }
                    }
                };
            }
        }

        private void UpdateButtonColors(ThemeColors colors)
        {
            try
            {
                var brushConverter = new BrushConverter();
                
                // 更新计时器按钮颜色 - 文本按钮，悬停时显示80%亮度背景色
                if (TimerButton != null)
                {
                    TimerButton.Foreground = (Brush)brushConverter.ConvertFromString(colors.PrimaryColor);
                    
                    // 创建80%亮度的背景色
                    var hoverBackgroundColor = LightenColor(colors.PrimaryColor, 0.8); // 提高80%亮度
                    var pressedBackgroundColor = LightenColor(colors.PrimaryColor, 0.6); // 提高60%亮度
                    
                    // 设置触发器样式
                    var style = new Style(typeof(Button), TimerButton.Style);
                    style.Setters.Add(new Setter(Button.ForegroundProperty, (Brush)brushConverter.ConvertFromString(colors.PrimaryColor)));
                    
                    var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                    hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, (Brush)brushConverter.ConvertFromString(hoverBackgroundColor)));
                    style.Triggers.Add(hoverTrigger);
                    
                    var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
                    pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, (Brush)brushConverter.ConvertFromString(pressedBackgroundColor)));
                    style.Triggers.Add(pressedTrigger);
                    
                    TimerButton.Style = style;
                }
                
                // 更新添加任务按钮颜色 - 文本按钮，悬停时显示80%亮度背景色
                if (AddTaskButton != null)
                {
                    AddTaskButton.Foreground = (Brush)brushConverter.ConvertFromString(colors.PrimaryColor);
                    
                    // 创建80%亮度的背景色
                    var hoverBackgroundColor = LightenColor(colors.PrimaryColor, 0.8); // 提高80%亮度
                    var pressedBackgroundColor = LightenColor(colors.PrimaryColor, 0.6); // 提高60%亮度
                    
                    // 设置触发器样式
                    var style = new Style(typeof(LayUI.Wpf.Controls.LayButton), AddTaskButton.Style);
                    style.Setters.Add(new Setter(LayUI.Wpf.Controls.LayButton.ForegroundProperty, (Brush)brushConverter.ConvertFromString(colors.PrimaryColor)));
                    
                    var hoverTrigger = new Trigger { Property = LayUI.Wpf.Controls.LayButton.IsMouseOverProperty, Value = true };
                    hoverTrigger.Setters.Add(new Setter(LayUI.Wpf.Controls.LayButton.BackgroundProperty, (Brush)brushConverter.ConvertFromString(hoverBackgroundColor)));
                    style.Triggers.Add(hoverTrigger);
                    
                    var pressedTrigger = new Trigger { Property = LayUI.Wpf.Controls.LayButton.IsPressedProperty, Value = true };
                    pressedTrigger.Setters.Add(new Setter(LayUI.Wpf.Controls.LayButton.BackgroundProperty, (Brush)brushConverter.ConvertFromString(pressedBackgroundColor)));
                    style.Triggers.Add(pressedTrigger);
                    
                    AddTaskButton.Style = style;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新按钮颜色时发生错误: {ex.Message}");
            }
        }

        private string LightenColor(string hexColor, double factor)
        {
            try
            {
                // 移除#号
                hexColor = hexColor.TrimStart('#');
                
                // 解析RGB值
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                
                // 计算亮色版本（向白色混合）
                r = (int)(r + (255 - r) * factor);
                g = (int)(g + (255 - g) * factor);
                b = (int)(b + (255 - b) * factor);
                
                // 确保值在0-255范围内
                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));
                b = Math.Max(0, Math.Min(255, b));
                
                // 转换回十六进制
                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return hexColor; // 如果转换失败，返回原颜色
            }
        }

        private string DarkenColor(string hexColor, double factor)
        {
            try
            {
                // 移除#号
                hexColor = hexColor.TrimStart('#');
                
                // 解析RGB值
                int r = Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = Convert.ToInt32(hexColor.Substring(4, 2), 16);
                
                // 计算深色版本
                r = (int)(r * (1 - factor));
                g = (int)(g * (1 - factor));
                b = (int)(b * (1 - factor));
                
                // 确保值在0-255范围内
                r = Math.Max(0, Math.Min(255, r));
                g = Math.Max(0, Math.Min(255, g));
                b = Math.Max(0, Math.Min(255, b));
                
                // 转换回十六进制
                return $"#{r:X2}{g:X2}{b:X2}";
            }
            catch
            {
                return hexColor; // 如果转换失败，返回原颜色
            }
        }

        private void WuaiPojieLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用默认浏览器打开链接
                Process.Start(new ProcessStartInfo
                {
                    FileName = WUAI_POJIE_URL,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取help.html文件的完整路径
                string helpFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "help.html");
                
                // 检查文件是否存在
                if (File.Exists(helpFilePath))
                {
                    // 使用默认浏览器打开帮助文档
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = helpFilePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    CustomMessageBox.Show("帮助文档文件不存在，请确保help.html文件在程序目录中。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"无法打开帮助文档: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    // 主题颜色定义类
    public class ThemeColors
    {
        public string Background { get; set; }
        public string HeaderBackground { get; set; }
        public string CardBackground { get; set; }
        public string StatusBarBackground { get; set; }
        public string PrimaryColor { get; set; }
        public string SuccessColor { get; set; }
        public string DangerColor { get; set; }
        public string TextColor { get; set; }
        public string SecondaryTextColor { get; set; }
    }
}
