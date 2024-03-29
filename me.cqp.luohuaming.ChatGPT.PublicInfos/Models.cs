using System.Collections.Generic;
using System.Text;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos
{
    public interface IOrderModel
    {
        bool ImplementFlag { get; set; }
        string GetOrderStr();
        bool Judge(string destStr);
        int Priority { get; set; }
        FunctionResult Progress(CQGroupMessageEventArgs e);
        FunctionResult Progress(CQPrivateMessageEventArgs e);
    }
}
