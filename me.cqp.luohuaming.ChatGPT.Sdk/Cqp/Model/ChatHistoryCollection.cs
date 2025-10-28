using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Expand;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model
{
    public class ChatHistoryCollection : BasisStreamModel, IReadOnlyCollection<ChatHistory>
    {
        private List<ChatHistory> _list;

        public ChatHistoryCollection(CQApi api, string cipherText)
            : base(api, cipherText)
        {
        }

        public int Count
        {
            get { return this._list.Count; }
        }

        public IEnumerator<ChatHistory> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected override void Initialize(BinaryReader reader)
        {
            if (this._list == null)
            {
                this._list = new List<ChatHistory>();
            }

            int count = reader.ReadInt32_Ex();
            for (int i = 0; i < count; i++)
            {
                if (reader.Length() <= 0)
                {
                    throw new EndOfStreamException("无法读取数据, 因为已经读取到数据流末尾");
                }

                this._list.Add(new ChatHistory(this.CQApi, reader.ReadToken_Ex()));
            }
        }

        public override string ToSendString()
        {
            return this.ToString();
        }
    }
}
