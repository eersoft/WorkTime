using System;

namespace WorkTimeWPF.Models
{
    public class TaskStatistics
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public string TaskStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int SessionCount { get; set; }
        public long TotalDurationSeconds { get; set; }
        public long? TaskLifetimeSeconds { get; set; }

        public string StatusDisplayName
        {
            get
            {
                return TaskStatus switch
                {
                    "pending" => "待处理",
                    "in_progress" => "进行中",
                    "completed" => "已完成",
                    "deleted" => "已删除",
                    _ => TaskStatus
                };
            }
        }

        public string TotalDurationDisplay
        {
            get
            {
                var hours = TotalDurationSeconds / 3600;
                var minutes = (TotalDurationSeconds % 3600) / 60;
                var seconds = TotalDurationSeconds % 60;
                return $"{hours:00}:{minutes:00}:{seconds:00}";
            }
        }

        public string TaskLifetimeDisplay
        {
            get
            {
                if (TaskLifetimeSeconds.HasValue)
                {
                    var days = TaskLifetimeSeconds.Value / (24 * 3600);
                    var hours = (TaskLifetimeSeconds.Value % (24 * 3600)) / 3600;
                    return $"{days}天{hours}小时";
                }
                return "";
            }
        }

        public string CreatedAtDisplay => CreatedAt.ToString("yyyy-MM-dd");
        public string CompletedAtDisplay => CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
    }
}
