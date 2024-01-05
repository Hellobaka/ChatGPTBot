using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.PublicInfos;
using System.IO;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp;

namespace me.cqp.luohuaming.ChatGPT.Code.OrderFunctions
{
    public class ImageGeneration : IOrderModel
    {
        public bool ImplementFlag { get; set; } = true;

        public int Priority { get; set; } = 100;

        public string GetOrderStr() => AppConfig.ImageGenerationOrder;

        public bool Judge(string destStr) => destStr.Replace("＃", "#").StartsWith(GetOrderStr());

        public FunctionResult Progress(CQGroupMessageEventArgs e)
        {
            if (AppConfig.GroupList.Contains(e.FromGroup) is false)
            {
                return new FunctionResult();
            }
            FunctionResult result = new FunctionResult
            {
                Result = true,
                SendFlag = true,
            };
            SendText sendText = new SendText
            {
                SendID = e.FromGroup,
            };
            result.SendObject.Add(sendText);

            sendText.MsgToSend.Add(GenerateImage(e.Message.Text));
            return result;
        }

        public FunctionResult Progress(CQPrivateMessageEventArgs e)
        {
            if (AppConfig.PersonList.Contains(e.FromQQ) is false)
            {
                return new FunctionResult();
            }
            FunctionResult result = new FunctionResult
            {
                Result = true,
                SendFlag = true,
            };
            SendText sendText = new SendText
            {
                SendID = e.FromQQ,
            };
            result.SendObject.Add(sendText);

            sendText.MsgToSend.Add(GenerateImage(e.Message.Text));
            return result;
        }

        private string GenerateImage(string prompt)
        {
            try
            {
                var imgTask = PublicInfos.API.ImageGeneration.GetImageGenerationAsync(prompt);
                imgTask.Wait();
                string imgPath = Path.Combine(MainSave.ImageDirectory, "OpenAI_Image");
                Directory.CreateDirectory(imgPath);

                string fileName = $"{DateTime.Now:yyyyMMddHHmmssfff}.png";
                var downloadTask = CommonHelper.DownloadFile(imgTask.Result, fileName, imgPath);
                downloadTask.Wait();
                if (downloadTask.Result is false)
                {
                    return "下载图片失败";
                }
                else
                {
                    return CQApi.CQCode_Image(Path.Combine("OpenAI_Image", fileName)).ToString();
                }
            }
            catch (Exception e)
            {
                MainSave.CQLog.Error("图片生成", e.Message + e.StackTrace);
                if (e.InnerException != null)
                {
                    MainSave.CQLog.Error("图片生成", e.InnerException.Message + e.InnerException.StackTrace);
                }
                return "连接发生问题，查看日志排查问题";
            }
        }
    }
}
