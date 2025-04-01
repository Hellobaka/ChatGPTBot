using SqlSugar;
using System.IO;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.DB
{
    public static class SQLHelper
    {
        public static SqlSugarClient GetInstance()
        {
            SqlSugarClient db = new(new ConnectionConfig()
            {
                ConnectionString = $"data source={Path.Combine(MainSave.AppDirectory, "core.db")}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = false,
                InitKeyType = InitKeyType.Attribute,
            });
            return db;
        }

        public static void CreateDB()
        {
            string path = Path.Combine(MainSave.AppDirectory, "core.db");
            using var db = GetInstance();
            db.DbMaintenance.CreateDatabase(path);
            db.CodeFirst.InitTables(typeof(ChatRecord));
            db.CodeFirst.InitTables(typeof(Picture));
            db.CodeFirst.InitTables(typeof(Relationship));
            db.CodeFirst.InitTables(typeof(Usage));
        }
    }
}
