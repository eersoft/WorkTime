using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace WorkTimeWPF.Models
{
    public class AppConfig
    {
        public string Theme { get; set; } = "Light";
        public string Language { get; set; } = "zh-CN";
        public bool AutoStart { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
    }

    public class ConfigManager
    {
        private static readonly string ConfigFileName = "config.json";
        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "WorkTime");
        private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, ConfigFileName);

        private static AppConfig _config;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public static AppConfig GetConfig()
        {
            if (_config == null)
            {
                lock (_lockObject)
                {
                    if (_config == null)
                    {
                        _config = LoadConfig();
                    }
                }
            }
            return _config;
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string jsonContent = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<AppConfig>(jsonContent);
                    
                    // 验证主题是否有效
                    if (config != null && IsValidTheme(config.Theme))
                    {
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果配置文件损坏，记录错误并使用默认配置
                System.Diagnostics.Debug.WriteLine($"加载配置文件失败: {ex.Message}");
            }

            // 返回默认配置
            return new AppConfig();
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public static void SaveConfig(AppConfig config)
        {
            try
            {
                // 确保配置目录存在
                if (!Directory.Exists(ConfigDirectory))
                {
                    Directory.CreateDirectory(ConfigDirectory);
                }

                // 序列化配置为JSON
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                
                string jsonContent = JsonSerializer.Serialize(config, options);
                
                // 写入文件
                File.WriteAllText(ConfigFilePath, jsonContent);
                
                // 更新内存中的配置
                _config = config;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新主题设置
        /// </summary>
        public static void UpdateTheme(string themeName)
        {
            if (!IsValidTheme(themeName))
            {
                throw new ArgumentException($"无效的主题名称: {themeName}");
            }

            var config = GetConfig();
            config.Theme = themeName;
            SaveConfig(config);
        }

        /// <summary>
        /// 验证主题名称是否有效
        /// </summary>
        private static bool IsValidTheme(string themeName)
        {
            string[] validThemes = { "Light", "Blue", "Green", "Purple" };
            return Array.IndexOf(validThemes, themeName) >= 0;
        }

        /// <summary>
        /// 获取配置文件路径（用于调试）
        /// </summary>
        public static string GetConfigFilePath()
        {
            return ConfigFilePath;
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public static void ResetToDefault()
        {
            var defaultConfig = new AppConfig();
            SaveConfig(defaultConfig);
        }
    }
}
