using System;

namespace WorkTimeWPF.Models
{
    public class TimeRecord
    {
        public int RecordId { get; set; }
        public int TaskId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Duration { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string TaskName { get; set; } // 用于显示

        public string StartTimeDisplay => StartTime.ToString("yyyy-MM-dd HH:mm");
        public string EndTimeDisplay => EndTime?.ToString("yyyy-MM-dd HH:mm") ?? "进行中";
        
        public string DurationDisplay
        {
            get
            {
                if (Duration.HasValue)
                {
                    var hours = Duration.Value / 3600;
                    var minutes = (Duration.Value % 3600) / 60;
                    var seconds = Duration.Value % 60;
                    return $"{hours:00}:{minutes:00}:{seconds:00}";
                }
                return "计算中";
            }
        }

        public string TotalDurationDisplay
        {
            get
            {
                if (Duration.HasValue)
                {
                    var totalSeconds = Duration.Value;
                    var hours = totalSeconds / 3600;
                    var minutes = (totalSeconds % 3600) / 60;
                    var seconds = totalSeconds % 60;
                    return $"{hours:00}:{minutes:00}:{seconds:00}";
                }
                return "00:00:00";
            }
        }
    }
}
