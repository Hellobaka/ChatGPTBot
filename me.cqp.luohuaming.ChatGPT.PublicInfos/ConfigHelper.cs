using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos
{
    /// <summary>
    /// 配置读取帮助类
    /// </summary>
    public static class ConfigHelper
    {
        /// <summary>
        /// 配置文件路径
        /// </summary>
        public static string ConfigFileName { get; set; } = @"conf/Config.json";

        public static object ReadLock { get; set; } = new object();

        public static object WriteLock { get; set; } = new object();

        public static JObject CurrentJObject { get; set; }

        private static FileSystemWatcher ConfigChangeWatcher { get; set; }

        /// <summary>
        /// 读取配置
        /// </summary>
        /// <param name="sectionName">需要读取的配置键名</param>
        /// <typeparam name="T">类型</typeparam>
        /// <returns>目标类型的配置</returns>
        public static T GetConfig<T>(string sectionName, T defaultValue = default)
        {
            lock (ReadLock)
            {
                if (CurrentJObject != null && CurrentJObject.ContainsKey(sectionName))
                {
                    return CurrentJObject[sectionName].ToObject<T>();
                }

                if (CurrentJObject == null && defaultValue != null)
                {
                    return defaultValue;
                }

                if (defaultValue != null)
                {
                    SetConfig<T>(sectionName, defaultValue);
                    return defaultValue;
                }

                if (typeof(T) == typeof(string))
                {
                    return (T)(object)"";
                }

                if (typeof(T) == typeof(int))
                {
                    return (T)(object)0;
                }

                if (typeof(T) == typeof(long))
                {
                    return default;
                }

                if (typeof(T) == typeof(bool))
                {
                    return (T)(object)false;
                }

                if (typeof(T) == typeof(object))
                {
                    return (T)(object)new { };
                }

                return typeof(T) == typeof(List<long>) ? (T)(object)new List<long>() : throw new Exception("无法默认返回");
            }
        }

        public static void SetConfig<T>(string sectionName, T value)
        {
            lock (WriteLock)
            {
                DisableHotReload();
                if (CurrentJObject == null)
                {
                    CurrentJObject = new JObject();
                }
                if (CurrentJObject.ContainsKey(sectionName))
                {
                    CurrentJObject[sectionName] = JToken.FromObject(value);
                }
                else
                {
                    CurrentJObject.Add(sectionName, JToken.FromObject(value));
                }

                File.WriteAllText(ConfigFileName, CurrentJObject.ToString(Newtonsoft.Json.Formatting.Indented));
                EnableHotReload();
            }
        }

        public static bool Load()
        {
            try
            {
                if (File.Exists(ConfigFileName) is false)
                {
                    File.WriteAllText(ConfigFileName, "{}");
                }
                CurrentJObject = JObject.Parse(File.ReadAllText(ConfigFileName));
                CommonHelper.DebugLog("配置热重载", "OK");
                return true;
            }
            catch (Exception e)
            {
                CommonHelper.DebugLog("配置热重载", $"LoadFail: {e.Message}");
                return false;
            }
        }

        public static void EnableHotReload()
        {
            if (ConfigChangeWatcher == null)
            {
                ConfigChangeWatcher = new FileSystemWatcher();
                ConfigChangeWatcher.Path = Path.GetDirectoryName(ConfigFileName);
                ConfigChangeWatcher.Filter = Path.GetFileName(ConfigFileName);
                ConfigChangeWatcher.NotifyFilter = NotifyFilters.LastWrite;
                ConfigChangeWatcher.Changed += ConfigChangeWatcher_Changed;
            }
            if (ConfigChangeWatcher.EnableRaisingEvents)
            {
                return;
            }
            ConfigChangeWatcher.EnableRaisingEvents = true;
        }

        public static void DisableHotReload()
        {
            if (ConfigChangeWatcher != null)
            {
                ConfigChangeWatcher.EnableRaisingEvents = false;
            }
        }

        private static void ConfigChangeWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && Load())
            {
                AppConfig.Init();
            }
        }
    }
}
