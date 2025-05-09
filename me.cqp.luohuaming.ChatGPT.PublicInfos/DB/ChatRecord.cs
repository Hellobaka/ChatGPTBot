using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Enum;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
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

        [SugarColumn(IsIgnore = true)]
        public Relationship Relationship { get; set; }

        [SugarColumn(IsIgnore = true)]
        public string Message_NoAppendInfo { get; set; }

        public string ParsedMessage { get; set; }

        public DateTime Time { get; set; }

        public bool IsImage { get; set; }

        public bool IsEmpty { get; set; }

        [SugarColumn(IsIgnore = true)]
        public bool IsMentioned { get; set; }

        public static ChatRecord Create(long qq, string message, int messageID)
        {
            var record = new ChatRecord
            {
                GroupID = -1,
                QQ = qq,
                RawMessage = message,
                Time = DateTime.Now,
                MessageID = messageID,
            };
            record.ParsedMessage = record.ParseMessage();
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
                MessageID = messageID,
                IsMentioned = CheckAt(message, false)
            };
            record.ParsedMessage = record.ParseMessage();
            return record;
        }

        public static void InsertRecord(ChatRecord chatRecord)
        {
            using var db = SQLHelper.GetInstance();
            chatRecord.Id = db.Insertable(chatRecord).ExecuteReturnIdentity();
        }

        public static List<ChatRecord> GetGroupChatRecord(long groupId, long qq = 0, int count = 15)
        {
            using var db = SQLHelper.GetInstance();
            List<ChatRecord> results = qq > 0
                ? db.Queryable<ChatRecord>().Where(x => x.GroupID == groupId && x.QQ == qq).OrderByDescending(x => x.Time).Take(count).ToList()
                : db.Queryable<ChatRecord>().Where(x => x.GroupID == groupId).OrderByDescending(x => x.Time).Take(count).ToList();
            return results;
        }

        public static List<ChatRecord> GetPrivateChatRecord(long qq, int count = 15)
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<ChatRecord>().Where(x => x.GroupID == -1 && x.QQ == qq).OrderByDescending(x => x.Time).Take(count).ToList();
        }

        public static List<ChatRecord> GetChatRecordByIds(int[] ids)
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<ChatRecord>().Where(x => ids.Contains(x.Id)).OrderByDescending(x => x.Time).ToList();
        }

        public static ChatRecord? GetChatRecordById(SqlSugarClient db, int id)
        {
            return db.Queryable<ChatRecord>().First(x => id == x.Id);
        }

        public string ParseMessage()
        {
            string message = (string)RawMessage.Clone();

            Relationship = Relationship.GetRelationShip(GroupID, QQ);
            StringBuilder stringBuilder = new();

            string info = $"[{Time:G}][MessageID={MessageID}]";
            if (QQ == MainSave.CurrentQQ)
            {
                info += $"[你自己] [昵称：{AppConfig.BotName}]: ";
            }
            else if (Relationship != null)
            {
                info += $" [昵称：{Relationship.Card ?? Relationship.NickName}]: ";
            }
            else
            {
                info += $" [昵称：{QQ}]: ";
            }

            var split = message.Replace("\n", "").SplitV2("\\[CQ:.*?\\]");
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
                        case CQFunction.Reply:
                            if (int.TryParse(cqcode.Items["id"], out int id))
                            {
                                var msg = GetRecordByMessageId(id);
                                if (msg != null)
                                {
                                    var img = CQCode.Parse(msg.RawMessage).FirstOrDefault(x => x.IsImageCQCode);
                                    var quoteMessage = img != null
                                        ? HandleImage(img)
                                        : msg.ParsedMessage;
                                    stringBuilder.Append($"[QuoteMessage@{cqcode.Items["id"]}:{quoteMessage}]");
                                }
                            }
                            break;

                        case CQFunction.At:
                            long target = long.Parse(cqcode.Items["qq"]);
                            if (target == MainSave.CurrentQQ)
                            {
                                stringBuilder.Append($"[@{AppConfig.BotName}]");
                            }
                            else
                            {
                                var r = Relationship.GetRelationShip(GroupID, target);
                                stringBuilder.Append($"[@{r.Card ?? r.NickName}]");
                            }
                            break;

                        case CQFunction.Face:
                            image++;
                            var face = (CQFace)int.Parse(cqcode.Items["id"]);
                            stringBuilder.Append($"[表情:{face}]");
                            break;

                        case CQFunction.Image:
                            image++;
                            string reply = HandleImage(cqcode);
                            if (!string.IsNullOrWhiteSpace(reply))
                            {
                                stringBuilder.Append(reply);
                            }
                            else
                            {
                                stringBuilder.Append("[图片]");
                            }
                            break;
                    }
                }
            }
            IsImage = image >= 1 && text == 0;
            IsEmpty = image == 0 && text == 0;
            Message_NoAppendInfo = stringBuilder.ToString();

            return info + stringBuilder.ToString();
        }

        public static ChatRecord GetRecordByMessageId(int id)
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<ChatRecord>().First(x => x.MessageID == id);
        }

        public static bool CheckAt(string input, bool forceBegin)
        {
            // 要求CQ码必须在开头, 所以只检查原始文本开头是否为At CQ码即可
            if (forceBegin && input.StartsWith("[CQ:at"))
            {
                return false;
            }

            var cqcodes = CQCode.Parse(input);
            var atCode = cqcodes.Where(x => x.Function == Sdk.Cqp.Enum.CQFunction.At);
            var replyCode = cqcodes.FirstOrDefault(x => x.Function == Sdk.Cqp.Enum.CQFunction.Reply);
            if (replyCode != null && int.TryParse(replyCode.Items["id"], out int id))
            {
                var msg = ChatRecord.GetRecordByMessageId(id);
                if (msg != null && msg.QQ == MainSave.CurrentQQ)
                {
                    return true;
                }
            }
            if (atCode == null || atCode.Count() == 0)
            {
                return false;
            }
            // 强制开头检查 取第一个CQ码 检查QQ是否为本机QQ
            if (forceBegin)
            {
                return atCode.FirstOrDefault()?.Items["qq"] == MainSave.CurrentQQ.ToString();
            }

            // 非强制开头检查 检查第一个QQ为本机QQ是否存在
            return atCode.Any(x => x.Items["qq"] == MainSave.CurrentQQ.ToString());
        }

        private bool ShouldProcessImage(bool isMentioned, bool isEmoji)
        {
            if (AppConfig.EnableVisionWhenMentioned && isMentioned)
            {
                return true;
            }

            if (!AppConfig.EnableVision)
            {
                return false;
            }

            return !(AppConfig.IgnoreNotEmoji && !isEmoji);
        }

        private string? HandleImage(CQCode cqcode)
        {
            if (!cqcode.IsImageCQCode)
            {
                return null;
            }
            bool emoji = cqcode.Items.TryGetValue("sub_type", out string subType) && subType == "1";
            if (!ShouldProcessImage(IsMentioned, emoji))
            {
                return "[图片]";
            }

            // 获取图片
            (Picture? picture, string? cachePath, string? hash) = Picture.TryGetImageHash(cqcode);

            // 图片无效
            if (string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath) || string.IsNullOrEmpty(hash))
            {
                PictureDescriber.DeleteImage(cachePath);
                return "[图片]";
            }

            // 已有描述
            if (picture != null && !string.IsNullOrEmpty(picture.Description))
            {
                return $"[这是一张图片，这是它的描述：{picture.Description}]";
            }

            // 生成描述
            string description = emoji ? PictureDescriber.DescribeEmoji(cachePath) : PictureDescriber.DescribePicture(cachePath);
            if (description == Chat.ErrorMessage)
            {
                MainSave.CQLog.Error("图片描述", "描述失败，接口返回错误");
                PictureDescriber.DeleteImage(cachePath);
                return null;
            }

            Picture.InsertImageDescription(cachePath, hash, emoji, description);

            // 非表情包且只保存表情包图片
            if (!emoji && AppConfig.OnlySaveEmojiPicture)
            {
                PictureDescriber.DeleteImage(cachePath);
            }

            return $"[这是一张图片，这是它的描述：{description}]";
        }
    }
}
