using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class ChatRecords
    {
        public int MessageId { get; set; }

        public long GroupID { get; set; }

        public long QQ { get; set; }

        public string Message { get; set; }

        public DateTime ReceiveTime { get; set; }

        public bool Used { get; set; }
    }
}
