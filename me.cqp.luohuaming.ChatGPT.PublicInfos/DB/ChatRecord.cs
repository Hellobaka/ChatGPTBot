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

        [SugarColumn(IsIgnore = true)]
        public Relationship Relationship { get; set; }

        [SugarColumn(IsIgnore = true)]
        public string Message_NoAppendInfo { get; set; }

        public string ParsedMessage { get; set; }

        public DateTime Time { get; set; }

        public bool IsImage { get; set; }

        public bool IsEmpty { get; set; }

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
            List<ChatRecord> results;
            if (qq > 0)
            {
                results = db.Queryable<ChatRecord>().Where(x => x.GroupID == groupId && x.QQ == qq).OrderByDescending(x => x.Time).Take(count).ToList();
            }
            else
            {
                results = db.Queryable<ChatRecord>().Where(x => x.GroupID == groupId).OrderByDescending(x => x.Time).Take(count).ToList();
            }

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
                            stringBuilder.Append($"[QuoteMessage:{cqcode.Items["id"]}]");
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
                            bool emoji = cqcode.Items.TryGetValue("sub_type", out string subType) && subType == "1";
                            if (AppConfig.EnableVision is false)
                            {
                                stringBuilder.Append($"[图片]");
                                break;
                            }

                            (Picture? picture, string? cachePath, string? hash) = Picture.TryGetImageHash(cqcode, emoji);
                            if (picture != null && !string.IsNullOrEmpty(picture.Description))
                            {
                                stringBuilder.Append($"[这是一张图片，这是它的描述：{picture.Description}]");
                                break;
                            }
                            // 禁用视觉或 启用视觉但开启仅表情包的配置时，图片不是表情包 或下载失败时
                            if ((!emoji && AppConfig.IgnoreNotEmoji) || string.IsNullOrEmpty(cachePath)
                                || !File.Exists(cachePath) || string.IsNullOrEmpty(hash))
                            {
                                stringBuilder.Append($"[图片]");
                                // 配置关闭或失败，需要删除本次下载结果
                                PictureDescriber.DeleteImage(cachePath);
                                continue;
                            }
                            // 需要缓存并获取图片描述
                            CommonHelper.DebugLog("图片描述", $"开始对图片 {hash} 进行描述生成");
                            string description = emoji ? PictureDescriber.DescribeEmoji(cachePath) : PictureDescriber.DescribePicture(cachePath);
                            if (description == Chat.ErrorMessage)
                            {
                                MainSave.CQLog.Error("图片描述", "描述失败，接口返回错误");
                                // 获取描述失败，删除本次下载结果
                                PictureDescriber.DeleteImage(cachePath);
                                break;
                            }
                            Picture.InsertImageDescription(cachePath, hash, emoji, description);
                            if (!emoji && AppConfig.OnlySaveEmojiPicture)
                            {
                                // 配置只保存表情包图片
                                PictureDescriber.DeleteImage(cachePath);
                            }
                            CommonHelper.DebugLog("图片描述", $"图片 {hash} 的描述为：{description}");
                            stringBuilder.Append($"[这是一张图片，这是它的描述：{description}]");

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
    }
}
