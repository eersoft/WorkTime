using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace WorkTimeWPF.Models
{
    public class DatabaseManager
    {
        private string _connectionString;
        private string _dbPath;
        private static readonly object _lockObject = new object();

        public DatabaseManager(string dbPath = "grindstone.db")
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            bool isNewDatabase = !File.Exists(_dbPath);
            bool needsMigration = false;
            
            if (!isNewDatabase)
            {
                // 检查数据库结构是否需要迁移
                needsMigration = CheckDatabaseStructure();
            }
            
            if (isNewDatabase)
            {
                SQLiteConnection.CreateFile(_dbPath);
            }
            else if (needsMigration)
            {
                // 备份原数据库
                BackupDatabase();
            }

            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                
                // 创建任务表
                var createTasksTable = @"
                    CREATE TABLE IF NOT EXISTS tasks (
                        task_id INTEGER PRIMARY KEY AUTOINCREMENT,
                        task_name TEXT NOT NULL,
                        task_status TEXT NOT NULL DEFAULT 'pending',
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        completed_at TIMESTAMP NULL
                    )";

                // 创建时间记录表
                var createTimeRecordsTable = @"
                    CREATE TABLE IF NOT EXISTS time_records (
                        record_id INTEGER PRIMARY KEY AUTOINCREMENT,
                        task_id INTEGER NOT NULL,
                        start_time TIMESTAMP NOT NULL,
                        end_time TIMESTAMP,
                        duration INTEGER,
                        notes TEXT,
                        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                        FOREIGN KEY (task_id) REFERENCES tasks (task_id)
                    )";

                using (var command = new SQLiteCommand(createTasksTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new SQLiteCommand(createTimeRecordsTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                // 如果需要迁移，执行数据迁移
                if (needsMigration)
                {
                    MigrateDatabase(connection);
                }
            }
        }

        private bool CheckDatabaseStructure()
        {
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    
                    // 检查是否存在completed_at字段
                    var checkColumnSql = "PRAGMA table_info(tasks)";
                    using (var command = new SQLiteCommand(checkColumnSql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            bool hasCompletedAt = false;
                            while (reader.Read())
                            {
                                if (reader.GetString("name") == "completed_at")
                                {
                                    hasCompletedAt = true;
                                    break;
                                }
                            }
                            return !hasCompletedAt; // 如果没有completed_at字段，需要迁移
                        }
                    }
                }
            }
            catch
            {
                // 如果检查失败，假设需要迁移
                return true;
            }
        }

        private void BackupDatabase()
        {
            try
            {
                var backupPath = _dbPath + ".backup." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                File.Copy(_dbPath, backupPath);
            }
            catch (Exception ex)
            {
                // 备份失败，记录日志但不阻止程序运行
                System.Diagnostics.Debug.WriteLine($"数据库备份失败: {ex.Message}");
            }
        }

        private void MigrateDatabase(SQLiteConnection connection)
        {
            try
            {
                // 添加completed_at字段（如果不存在）
                var addColumnSql = "ALTER TABLE tasks ADD COLUMN completed_at TIMESTAMP NULL";
                using (var command = new SQLiteCommand(addColumnSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (SQLiteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // 字段已存在，忽略错误
            }
            catch (Exception ex)
            {
                // 迁移失败，记录日志
                System.Diagnostics.Debug.WriteLine($"数据库迁移失败: {ex.Message}");
            }
        }

        // 任务管理方法
        public int AddTask(string taskName)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                var sql = "INSERT INTO tasks (task_name, task_status) VALUES (@taskName, 'pending')";
                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@taskName", taskName);
                    command.ExecuteNonQuery();
                    return (int)connection.LastInsertRowId;
                }
            }
        }

        public List<Task> GetTasks(string status = null)
        {
            var tasks = new List<Task>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                string sql;
                if (string.IsNullOrEmpty(status))
                {
                    sql = "SELECT * FROM tasks WHERE task_status != 'deleted' ORDER BY updated_at DESC";
                }
                else
                {
                    sql = "SELECT * FROM tasks WHERE task_status = @status ORDER BY updated_at DESC";
                }

                using (var command = new SQLiteCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(status))
                    {
                        command.Parameters.AddWithValue("@status", status);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tasks.Add(new Task
                            {
                                TaskId = reader.GetInt32("task_id"),
                                TaskName = reader.GetString("task_name"),
                                TaskStatus = reader.GetString("task_status"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                UpdatedAt = reader.GetDateTime("updated_at"),
                                CompletedAt = reader.IsDBNull("completed_at") ? null : reader.GetDateTime("completed_at")
                            });
                        }
                    }
                }
            }
            return tasks;
        }

        public List<TaskStatistics> GetTaskStatistics()
        {
            var statistics = new List<TaskStatistics>();
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    var sql = @"
                        SELECT 
                            t.task_id,
                            t.task_name,
                            t.task_status,
                            t.created_at,
                            t.completed_at,
                            COUNT(tr.record_id) as session_count,
                            COALESCE(SUM(tr.duration), 0) as total_duration_seconds
                        FROM tasks t
                        LEFT JOIN time_records tr ON t.task_id = tr.task_id AND tr.duration IS NOT NULL
                        GROUP BY t.task_id, t.task_name, t.task_status, t.created_at, t.completed_at
                        ORDER BY total_duration_seconds DESC";

                    using (var command = new SQLiteCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                statistics.Add(new TaskStatistics
                                {
                                    TaskId = reader.GetInt32("task_id"),
                                    TaskName = reader.GetString("task_name"),
                                    TaskStatus = reader.GetString("task_status"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    CompletedAt = reader.IsDBNull("completed_at") ? null : reader.GetDateTime("completed_at"),
                                    SessionCount = reader.GetInt32("session_count"),
                                    TotalDurationSeconds = reader.GetInt64("total_duration_seconds"),
                                    TaskLifetimeSeconds = null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果查询失败，返回空列表而不是null
                System.Diagnostics.Debug.WriteLine($"获取任务统计失败: {ex.Message}");
                return new List<TaskStatistics>();
            }
            return statistics;
        }

        public void UpdateTaskStatus(int taskId, string status)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                var sql = "UPDATE tasks SET task_status = @status, updated_at = CURRENT_TIMESTAMP";
                
                if (status == "completed")
                {
                    sql += ", completed_at = CURRENT_TIMESTAMP";
                }
                
                sql += " WHERE task_id = @taskId";

                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@status", status);
                    command.Parameters.AddWithValue("@taskId", taskId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteTask(int taskId)
        {
            UpdateTaskStatus(taskId, "deleted");
        }

        // 时间记录管理方法
        public int StartTimer(int taskId)
        {
            lock (_lockObject)
            {
                try
                {
                    // 先暂停其他正在进行的计时器
                    PauseAllActiveTimers();

                    // 更新任务状态
                    UpdateTaskStatus(taskId, "in_progress");

                    using (var connection = new SQLiteConnection(_connectionString))
                    {
                        connection.Open();
                        var sql = "INSERT INTO time_records (task_id, start_time) VALUES (@taskId, @startTime)";
                        using (var command = new SQLiteCommand(sql, connection))
                        {
                            command.Parameters.AddWithValue("@taskId", taskId);
                            command.Parameters.AddWithValue("@startTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            command.ExecuteNonQuery();
                            return (int)connection.LastInsertRowId;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"StartTimer错误: {ex.Message}");
                    throw;
                }
            }
        }

        public void PauseTimer(int recordId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                
                // 获取开始时间
                var getStartTimeSql = "SELECT start_time, task_id FROM time_records WHERE record_id = @recordId";
                DateTime startTime;
                int taskId;
                
                using (var command = new SQLiteCommand(getStartTimeSql, connection))
                {
                    command.Parameters.AddWithValue("@recordId", recordId);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            startTime = reader.GetDateTime("start_time");
                            taskId = reader.GetInt32("task_id");
                        }
                        else
                        {
                            return;
                        }
                    }
                }

                // 计算持续时间
                var endTime = DateTime.Now;
                var duration = (int)(endTime - startTime).TotalSeconds;

                // 更新时间记录
                var updateSql = "UPDATE time_records SET end_time = @endTime, duration = @duration WHERE record_id = @recordId";
                using (var command = new SQLiteCommand(updateSql, connection))
                {
                    command.Parameters.AddWithValue("@endTime", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    command.Parameters.AddWithValue("@duration", duration);
                    command.Parameters.AddWithValue("@recordId", recordId);
                    command.ExecuteNonQuery();
                }

                // 检查该任务是否还有其他进行中的记录
                var checkActiveSql = "SELECT COUNT(*) FROM time_records WHERE task_id = @taskId AND end_time IS NULL";
                using (var command = new SQLiteCommand(checkActiveSql, connection))
                {
                    command.Parameters.AddWithValue("@taskId", taskId);
                    var activeCount = Convert.ToInt32(command.ExecuteScalar());
                    
                    if (activeCount == 0)
                    {
                        UpdateTaskStatus(taskId, "pending");
                    }
                }
            }
        }

        private void PauseAllActiveTimers()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                var sql = "SELECT record_id FROM time_records WHERE end_time IS NULL";
                using (var command = new SQLiteCommand(sql, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            PauseTimer(reader.GetInt32("record_id"));
                        }
                    }
                }
            }
        }

        public List<TimeRecord> GetTimeRecords(int? taskId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            var records = new List<TimeRecord>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                
                var sql = "SELECT * FROM time_records WHERE 1=1";
                var parameters = new List<SQLiteParameter>();

                if (taskId.HasValue)
                {
                    sql += " AND task_id = @taskId";
                    parameters.Add(new SQLiteParameter("@taskId", DbType.Int32) { Value = taskId.Value });
                }

                if (startDate.HasValue)
                {
                    sql += " AND start_time >= @startDate";
                    parameters.Add(new SQLiteParameter("@startDate", DbType.String) { Value = startDate.Value.ToString("yyyy-MM-dd HH:mm:ss") });
                }

                if (endDate.HasValue)
                {
                    sql += " AND start_time <= @endDate";
                    parameters.Add(new SQLiteParameter("@endDate", DbType.String) { Value = endDate.Value.ToString("yyyy-MM-dd HH:mm:ss") });
                }

                sql += " ORDER BY start_time DESC";

                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddRange(parameters.ToArray());
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            records.Add(new TimeRecord
                            {
                                RecordId = reader.GetInt32("record_id"),
                                TaskId = reader.GetInt32("task_id"),
                                StartTime = reader.GetDateTime("start_time"),
                                EndTime = reader.IsDBNull("end_time") ? null : reader.GetDateTime("end_time"),
                                Duration = reader.IsDBNull("duration") ? null : reader.GetInt32("duration"),
                                Notes = reader.IsDBNull("notes") ? null : reader.GetString("notes"),
                                CreatedAt = reader.GetDateTime("created_at")
                            });
                        }
                    }
                }
            }
            return records;
        }

        public TimeRecord GetActiveTimer()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                var sql = @"
                    SELECT tr.*, t.task_name 
                    FROM time_records tr
                    JOIN tasks t ON tr.task_id = t.task_id
                    WHERE tr.end_time IS NULL
                    LIMIT 1";

                using (var command = new SQLiteCommand(sql, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new TimeRecord
                            {
                                RecordId = reader.GetInt32("record_id"),
                                TaskId = reader.GetInt32("task_id"),
                                StartTime = reader.GetDateTime("start_time"),
                                EndTime = null,
                                Duration = null,
                                Notes = reader.IsDBNull("notes") ? null : reader.GetString("notes"),
                                CreatedAt = reader.GetDateTime("created_at"),
                                TaskName = reader.GetString("task_name")
                            };
                        }
                    }
                }
            }
            return null;
        }

        public void UpdateRecordNotes(int recordId, string notes)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                var sql = "UPDATE time_records SET notes = @notes WHERE record_id = @recordId";
                using (var command = new SQLiteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@notes", notes);
                    command.Parameters.AddWithValue("@recordId", recordId);
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
