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
            ConfigHelper.EnableHotReload();
            ClearFlowTimer = new Timer();
            ClearFlowTimer.Elapsed += ClearFlowTimer_Elapsed;
            ClearFlowTimer.Interval = 1000;
            ClearFlowTimer.Start();

            BuildPromptList();

            TTSHelper.CheckTTS();

            MainSave.CQLog.Info("初始化", "ChatGPT插件初始化完成");
        }

        private void BuildPromptList()
        {
            string promptPath = Path.Combine(MainSave.AppDirectory, "Prompts");
            foreach (var file in Directory.GetFiles(promptPath, "*.txt"))
            {
                MainSave.Prompts.Add(Path.GetFileNameWithoutExtension(file), file);
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