using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Interface;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace me.cqp.luohuaming.ChatGPT.Code
{
    public class Event_StartUp : ICQStartup
    {
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
            AppConfig.Init();
            if (new string[] { AppConfig.ChatAPIKey, AppConfig.ChatBaseURL, AppConfig.ChatModelName }.Any(string.IsNullOrEmpty))
            {
                MainSave.CQLog.Error("初始化", "关键 Chat 配置无效，插件无法使用");
                return;
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

            SQLHelper.CreateDB();
            TTSHelper.CheckTTS();
            if (AppConfig.EnableMemory)
            {
                var qdrant = new Qdrant(AppConfig.QdrantHost, AppConfig.QdrantPort);
                if (qdrant.CheckConnection() is false)
                {
                    MainSave.CQLog.Error("初始化", "向量数据库连接失败，请检查配置，记忆模块已禁用");
                    AppConfig.EnableMemory = false;
                }
                else
                {
                    qdrant.CreateCollection();
                }
            }
            if (AppConfig.EnableVision)
            {
                if (new string[] { AppConfig.ImageDescriberUrl, AppConfig.ImageDescriberApiKey, AppConfig.ImageDescriberModelName,
                    AppConfig.EmbeddingUrl,  AppConfig.EmbeddingModelName}.Any(string.IsNullOrEmpty))
                {
                    MainSave.CQLog.Error("初始化", "图像描述API配置无效，图像描述模块已禁用");
                    AppConfig.EnableVision = false;
                }
            }
            if (AppConfig.EnableRerank)
            {
                if (new string[] { AppConfig.RerankUrl,  AppConfig.RerankModelName }.Any(string.IsNullOrEmpty))
                {
                    MainSave.CQLog.Error("初始化", "重排序API配置无效，重排序模块已禁用");
                    AppConfig.EnableRerank = false;
                }
            }
            if (AppConfig.EnableSpliter && !AppConfig.SpliterRegexFirst)
            {
                if (new string[] { AppConfig.SpliterApiKey, AppConfig.SpliterUrl, AppConfig.SpliterPrompt, AppConfig.SpliterModelName }.Any(string.IsNullOrEmpty))
                {
                    MainSave.CQLog.Error("初始化", "分段API配置无效，已切换至强制正则分段");
                    AppConfig.SpliterRegexFirst = true;
                }
            }
            if (AppConfig.EnableTencentSign)
            {
                if (new string[] { AppConfig.TencentSecretId, AppConfig.TencentSecretKey }.Any(string.IsNullOrEmpty))
                {
                    MainSave.CQLog.Error("初始化", "腾讯云签名配置无效，已禁用");
                    AppConfig.EnableTencentSign = false;
                }
            }

            _ = new MoodManager();
            _ = new SchedulerManager();

            MainSave.CQLog.Info("初始化", "ChatGPT插件初始化完成");
        }
    }
}