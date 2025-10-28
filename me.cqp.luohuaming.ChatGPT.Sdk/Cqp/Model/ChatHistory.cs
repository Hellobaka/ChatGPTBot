using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Expand;
using System;
using System.IO;

namespace me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model
{
    public class ChatHistory : BasisStreamModel
    {
        public ChatHistory(CQApi api, byte[] cipherBytes)
            : base(api, cipherBytes)
        {
        }

        public ChatHistory(CQApi api, string cipherText)
            : base(api, cipherText)
        {
        }

        public DateTime Time { get; set; } = DateTime.Now;

        /// <summary>
        /// 群号或QQ
        /// </summary>
        public long ParentID { get; set; }

        public long SenderID { get; set; }

        public string Message { get; set; }

        public int MsgId { get; set; }

        public bool Recalled { get; set; }

        protected override void Initialize(BinaryReader binaryReader)
        {
            Time = DateTime.ParseExact(binaryReader.ReadString_Ex(), "G", null);
            ParentID = binaryReader.ReadInt64_Ex();
            SenderID = binaryReader.ReadInt64_Ex();
            Message = binaryReader.ReadString_Ex();
            MsgId = binaryReader.ReadInt32_Ex();
            Recalled = binaryReader.ReadInt32_Ex() == 1;
        }

        public override string ToSendString()
        {
            return this.ToString();
        }
    }
}
