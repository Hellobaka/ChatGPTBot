using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Interface;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Timers;

namespace me.cqp.luohuaming.ChatGPT.Code
{
    public class Event_StartUp : ICQStartup
    {
        public Timer ClearFlowTimer { get; private set; }

        public void CQStartup(object sender, CQStartupEventArgs e)
        {
            MainSave.AppDirectory = e.CQApi.AppDirectory;
            MainSave.CQApi = e.CQApi;
            MainSave.CQLog = e.CQLog;
            MainSave.ImageDirectory = CommonHelper.GetAppImageDirectory();
            MainSave.RecordDirectory = CommonHelper.GetAppRecordDirectory();
            MainSave.CurrentQQ = e.CQApi.GetLoginQQ();
            Directory.CreateDirectory(Path.Combine(MainSave.AppDirectory, "Prompts"));
            ConfigHelper.ConfigFileName = Path.Combine(MainSave.AppDirectory, "Config.json");
            if (ConfigHelper.Load() is false)
            {
                MainSave.CQLog.Warning("加载配置文件", "内容格式不正确，无法加载");
            }
            foreach (var item in Assembly.GetAssembly(typeof(Event_GroupMessage)).GetTypes())
            {
                if (item.IsInterface)
                {
                    continue;
                }

                foreach (var instance in item.GetInterfaces())
                {
                    if (instance == typeof(IOrderModel))
                    {
                        IOrderModel obj = (IOrderModel)Activator.CreateInstance(item);
                        if (obj.ImplementFlag == false)
                        {
                            break;
                        }

                        MainSave.Instances.Add(obj);
                    }
                }
            }
            AppConfig.Init();
            ClearFlowTimer = new Timer();
            ClearFlowTimer.Elapsed += ClearFlowTimer_Elapsed;
            ClearFlowTimer.Interval = 1000;
            ClearFlowTimer.Start();

            FileSystemWatcher configChangeWatcher = new FileSystemWatcher();
            configChangeWatcher.Path = Path.GetDirectoryName(ConfigHelper.ConfigFileName);
            configChangeWatcher.Filter = Path.GetFileName(ConfigHelper.ConfigFileName);
            configChangeWatcher.NotifyFilter = NotifyFilters.LastWrite;
            configChangeWatcher.Changed += ConfigChangeWatcher_Changed;
            configChangeWatcher.EnableRaisingEvents = true;

            BuildPromptList();

            CheckTTS();

            MainSave.CQLog.Info("初始化", "ChatGPT插件初始化完成");
        }

        private void CheckTTS()
        {
            if (AppConfig.EnableTTS is false)
            {
                return;
            }

            ProcessStartInfo startInfo = new()
            {
                FileName = "cmd.exe",
                Arguments = "/c chcp 65001 && python --version",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using Process process = Process.Start(startInfo);
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            bool success = output.Contains("Python 3.") || error.Contains("Python 3.");

            if (!success)
            {
                MainSave.CQLog.Debug("TTS_Output", output);
                MainSave.CQLog.Debug("TTS_Error", error);
                MainSave.CQLog.Error("TTS", "未检测到python环境");
            }

            TTSHelper.Enabled = success;
        }

        private void BuildPromptList()
        {
            string promptPath = Path.Combine(MainSave.AppDirectory, "Prompts");
            foreach (var file in Directory.GetFiles(promptPath, "*.txt"))
            {
                MainSave.Prompts.Add(Path.GetFileNameWithoutExtension(file), file);
            }
        }

        private void ConfigChangeWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Changed && ConfigHelper.Load())
            {
                AppConfig.Init();
            }
        }

        private void ClearFlowTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < Chat.ChatFlows.Count; i++)
            {
                var item = Chat.ChatFlows[i];
                item.RemoveTimeout++;
                if (item.RemoveTimeout >= AppConfig.ChatTimeout)
                {
                    item.RemoveFromFlows();
                    i--;
                }
            }
        }
    }
}