using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Enum;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    [SugarTable]
    public class ChatRecord
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public int MessageID { get; set; }

        public long GroupID { get; set; }

        public long QQ { get; set; }

        public string RawMessage { get; set; }

        public string ParsedMessage { get; set; }

        public DateTime Time { get; set; }

        public bool IsImage { get; set; }

        public static ChatRecord Create(long qq, string message, int messageID)
        {
            var record = new ChatRecord
            {
                GroupID = -1,
                QQ = qq,
                RawMessage = message,
                Time = DateTime.Now,
                MessageID = messageID
            };
            record.ParsedMessage = record.ParseMessage(message);
            return record;
        }

        public static ChatRecord Create(long groupID, long qq, string message, int messageID)
        {
            var record = new ChatRecord
            {
                GroupID = groupID,
                QQ = qq,
                RawMessage = message,
                Time = DateTime.Now,
                MessageID = messageID
            };
            record.ParsedMessage = record.ParseMessage(message);
            return record;
        }

        public static void InsertRecord(ChatRecord chatRecord)
        {
            var db = SQLHelper.GetInstance();
            db.Insertable(chatRecord).ExecuteCommand();
        }

        public static List<ChatRecord> GetGroupChatRecord(long groupId, long qq = 0, int count = 15)
        {
            var db = SQLHelper.GetInstance();
            List<ChatRecord> results;
            if (qq > 0)
            {
                results = db.Queryable<ChatRecord>().Where(x => x.GroupID == groupId && x.QQ == qq).Take(count).ToList();
            }
            else
            {
                results = db.Queryable<ChatRecord>().Where(x => x.GroupID == groupId).Take(count).ToList();
            }

            return results;
        }

        public static List<ChatRecord> GetPrivateChatRecord(long qq, int count = 15)
        {
            var db = SQLHelper.GetInstance();
            return db.Queryable<ChatRecord>().Where(x => x.GroupID == -1 && x.QQ == qq).Take(count).ToList();
        }

        private string ParseMessage(string message)
        {
            var relationship = Relationship.GetRelationShip(GroupID, QQ);
            StringBuilder stringBuilder = new();
            if (relationship != null)
            {
                stringBuilder.Append($"[{Time:G}] {relationship.Card ?? relationship.NickName}: ");
            }
            else
            {
                stringBuilder.Append($"[{Time:G}] {QQ}: ");
            }

            var split = message.SplitV2("\\[CQ:.*?\\]");
            int image = 0, text = 0;
            foreach (var item in split)
            {
                if (!item.StartsWith("[CQ:"))
                {
                    text++;
                    stringBuilder.Append(item);
                    continue;
                }
                else
                {
                    var cqcode = CQCode.Parse(item).FirstOrDefault();
                    if (cqcode == null)
                    {
                        continue;
                    }
                    switch (cqcode.Function)
                    {
                        case CQFunction.Face:
                            image++;
                            var face = (CQFace)int.Parse(cqcode.Items["id"]);
                            stringBuilder.Append($"[表情:{face}]");
                            break;
                        case CQFunction.Image:
                            image++;
                            bool emoji = cqcode.Items.TryGetValue("sub_type", out string subType) && subType == "1";
                            if (AppConfig.EnableVision is false)
                            {
                                continue;
                            }
                            MainSave.CQLog.Debug("图片描述", $"开始对图片 {cqcode.Items["file"]} 进行描述生成");
                            string description = emoji ? PictureDescriber.DescribeEmoji(cqcode) : PictureDescriber.DescribePicture(cqcode);
                            Picture.InsertImageDescription(cqcode, emoji, description);
                            MainSave.CQLog.Debug("图片描述", $"图片 {cqcode.Items["file"]} 的描述为：{description}");


                            if (!string.IsNullOrEmpty(description))
                            {
                                stringBuilder.Append($"[这是一张图片，这是它的描述：{description}]");
                            }
                            break;
                    }
                }
            }
            IsImage = image >= 1 && text == 0;
            return stringBuilder.ToString();
        }
    }
}
