using System;

namespace WorkTimeWPF.Models
{
    public class Task
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public string TaskStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

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

        public string CreatedAtDisplay => CreatedAt.ToString("yyyy-MM-dd");
        public string CompletedAtDisplay => CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
    }
}
