﻿using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    [SugarTable]
    public class Relationship
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        public long GroupID { get; set; }

        public long QQ { get; set; }

        [SugarColumn(IsNullable = true)]
        public string NickName { get; set; }

        [SugarColumn(IsNullable = true)]
        public string Card { get; set; }

        public double Favorability { get; set; }

        public DateTime UpdateTime { get; set; }

        public static Relationship Create(long qq, string nickname)
        {
            return new Relationship
            {
                NickName = nickname,
                QQ = qq,
            };
        }

        public static Relationship Create(long groupId, long qq, string nickname, string? card)
        {
            return new Relationship
            {
                NickName = nickname,
                QQ = qq,
                Card = card,
                GroupID = groupId,
                UpdateTime = DateTime.Now
            };
        }

        public void Update()
        {
            using var db = SQLHelper.GetInstance();
            UpdateTime = DateTime.Now;
            db.Updateable(this).ExecuteCommand();
        }

        public void Delete()
        {
            using var db = SQLHelper.GetInstance();
            db.Deleteable(this).ExecuteCommand();
        }

        public static List<long> GetRelationShipGroups()
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<Relationship>().GroupBy(x => x.GroupID).Select(x => x.GroupID).ToList();
        }

        public static List<Relationship> GetRelationShipByGroup(long groupId)
        {
            using var db = SQLHelper.GetInstance();
            return db.Queryable<Relationship>().Where(x => x.GroupID == groupId).ToList();
        }

        public static Relationship? GetRelationShip(long groupID, long qq)
        {
            using var db = SQLHelper.GetInstance();
            var relationship = db.Queryable<Relationship>().Where(x => x.GroupID == groupID && x.QQ == qq).First();
            if (relationship == null
                || (DateTime.Now - relationship.UpdateTime).TotalDays >= AppConfig.RelationshipUpdateTime)
            {
                string nickname = null, card = null;
                if (groupID > 0)
                {
                    var info = MainSave.CQApi.GetGroupMemberInfo(groupID, qq);
                    if (info != null)
                    {
                        nickname = info.Nick;
                        card = !string.IsNullOrWhiteSpace(info.Card) ? info.Card : null;
                    }
                }
                else
                {
                    var info = MainSave.CQApi.GetFriendList().FirstOrDefault(x => x.QQ == qq);
                    if (info != null)
                    {
                        nickname = info.Nick;
                        card = !string.IsNullOrWhiteSpace(info.Postscript) ? info.Postscript : null;
                    }
                }

                if (string.IsNullOrEmpty(nickname) && string.IsNullOrEmpty(card))
                {
                    MainSave.CQLog.Warning("缓存用户昵称", "获取的昵称与卡片均为null");
                    return null;
                }
                else if (relationship == null)
                {
                    relationship = Create(groupID, qq, nickname, card);
                    db.Insertable(relationship).ExecuteCommand();
                }
                else
                {
                    relationship.Card = card;
                    relationship.NickName = nickname;
                    relationship.Update();
                }
            }

            return relationship;
        }

        public void UpdateFavourability(MoodManager.Mood mood, MoodManager.Stand stand)
        {
            double moodFavorValue = MoodManager.MoodFavorValue[mood];
            Favorability += moodFavorValue;
            Favorability = Math.Max(-1000, Math.Min(1000, Favorability));

            CommonHelper.DebugLog("更新用户关系", $"[{QQ}] 心情：{mood}，计算后新的关系值为：{Favorability}");

            using var db = SQLHelper.GetInstance();
            db.Updateable(this).ExecuteCommand();
        }

        public int GetFavorLevel() => Favorability switch
        {
            >= -1000 and < -227 => 1,
            >= -227 and < -73 => 2,
            >= -73 and < 227 => 3,
            >= 227 and < 587 => 4,
            >= 587 and < 900 => 5,
            >= 900 => 6,
            _ => 0
        };

        public override string ToString()
        {
            int level = GetFavorLevel();
            (string, string) attritude = level switch
            {
                1 => ("厌恶", "冷漠回应"),
                2 => ("冷漠", "冷淡回复"),
                3 => ("一般", "保持理性"),
                4 => ("友好", "愿意回复"),
                5 => ("喜欢", "积极回复"),
                6 => ("暧昧", "无条件支持"),
                _ => ("", "")
            };
            return string.Format("你对昵称为[{0}]的用户的态度为{1},回复态度为{2},关系等级为{3}", Card ?? NickName, attritude.Item1, attritude.Item2, level);
        }
    }
}
