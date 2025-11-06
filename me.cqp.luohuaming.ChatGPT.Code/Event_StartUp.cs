using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Interface;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
            SQLHelper.CreateDB();
            AppConfig.Init();
            _ = new MoodManager();
            _ = new SchedulerManager();
            Picture.InitCache();
            Picture.StartScheduleDeleteNonEmoji();
            if (AppConfig.ChatAPIKeyId.Count == 0)
            {
                MainSave.CQLog.Error("初始化", "Chat API 未配置，插件无法使用");
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
            Task.Run(() =>
            {
                if (AppConfig.ReplyAPIKeyId.Count == 0 && AppConfig.EnableLLMCheckShouldResponse)
                {
                    MainSave.CQLog.Error("初始化", "回复意愿 API 无效，已切换至内置方案");
                    AppConfig.EnableLLMCheckShouldResponse = false;
                }
                if (AppConfig.MemoryAPIKeyId.Count == 0 && AppConfig.EnableQdrant)
                {
                    MainSave.CQLog.Error("初始化", "记忆提取 API 无效，相关功能无法使用");
                }
                if (AppConfig.EnableQdrant)
                {
                    var qdrant = new Qdrant(AppConfig.QdrantHost, AppConfig.QdrantPort);
                    if (qdrant.GetCollections() is false)
                    {
                        MainSave.CQLog.Error("初始化", "Qdrant 向量数据库连接失败，记忆模块已禁用");
                        AppConfig.EnableQdrant = false;
                    }
                    else
                    {
                        qdrant.CreateCollection();
                    }
                }
                if (AppConfig.EnableVision)
                {
                    if (AppConfig.ImageDescriberApiKeyId.Count == 0)
                    {
                        MainSave.CQLog.Error("初始化", "图像描述 API 配置无效，图像描述模块已禁用");
                        AppConfig.EnableVision = false;
                    }
                }
                if (AppConfig.EnableRerank)
                {
                    if (AppConfig.RerankApiKeyId.Count == 0)
                    {
                        MainSave.CQLog.Error("初始化", "重排序 API 配置无效，重排序模块已禁用");
                        AppConfig.EnableRerank = false;
                    }
                }
                if (AppConfig.EnableSplitter && !AppConfig.SplitterRegexFirst)
                {
                    if (AppConfig.SplitterApiKeyId.Count == 0)
                    {
                        MainSave.CQLog.Error("初始化", "分段 API 配置无效，已切换至强制正则分段");
                        AppConfig.SplitterRegexFirst = true;
                    }
                }
                if (AppConfig.EnableSchedules)
                {
                    SchedulerManager.Instance.EnableTimer();
                }
                if (!AppConfig.EnableMCP)
                {
                    MainSave.CQLog.Info("初始化", "MCP 已禁用");
                }
                else
                {
                    MainSave.CQLog.Info("初始化", "加载 MCP 配置");
                    MCPClientManager.Load();
                    MCPClientManager.Rebuild();
                    MainSave.CQLog.Info("初始化", $"加载了 {MCPClientManager.Clients.Count} 个客户端，{MCPClientManager.MCPTools.Sum(x => x.Value.Length)} 个工具");
                }
                Memory.LoadToDoItems();
                Memory.LoadShortTermMemories();
                MainSave.CQLog.Info("初始化", "ChatGPT插件初始化完成");
            });
        }
    }
}