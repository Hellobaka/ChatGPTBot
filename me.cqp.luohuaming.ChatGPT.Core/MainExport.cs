using System.Collections.Generic;
using System.Linq;
using System.Threading;
using me.cqp.luohuaming.ChatGPT.Code;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Interface;
using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.Code.OrderFunctions;
using System;

namespace me.cqp.luohuaming.ChatGPT.Core
{
    public class MainExport : IGroupMessage, IPrivateMessage
    {
        public void GroupMessage(object sender, CQGroupMessageEventArgs e)
        {
            if (AppConfig.BlackList.Any(x => e.FromQQ == x))
            {
                return;
            }
            FunctionResult result = Event_GroupMessage.GroupMessage(e);
            if (result.SendFlag)
            {
                if (result.SendObject == null || result.SendObject.Count == 0)
                {
                    e.Handler = false;
                }
                foreach (var item in result.SendObject)
                {
                    foreach (var sendMsg in item.MsgToSend)
                    {
                        if (item.Reply && AppConfig.EnableGroupReply)
                        {
                            var msg = e.CQApi.SendGroupMessage(item.SendID, $"[CQ:reply,id={e.Message.Id}]" + sendMsg);
                            Record.RecordSelfMessage(item.SendID, msg);
                        }
                        else
                        {
                            var msg = e.CQApi.SendGroupMessage(item.SendID, sendMsg);
                            Record.RecordSelfMessage(item.SendID, msg);
                        }
                    }
                }
            }
            e.Handler = result.Result;
        }

        public void PrivateMessage(object sender, CQPrivateMessageEventArgs e)
        {
            if (AppConfig.BlackList.Any(x => e.FromQQ == x))
            {
                return;
            }

            FunctionResult result = Event_PrivateMessage.PrivateMessage(e);
            if (result.SendFlag)
            {
                if (result.SendObject == null || result.SendObject.Count == 0)
                {
                    e.Handler = false;
                }
                foreach (var item in result.SendObject)
                {
                    foreach (var sendMsg in item.MsgToSend)
                    {
                        var msg = e.CQApi.SendPrivateMessage(item.SendID, sendMsg);
                        Record.RecordSelfMessage(item.SendID, msg);
                    }
                }
            }
            e.Handler = result.Result;
        }
    }
}
