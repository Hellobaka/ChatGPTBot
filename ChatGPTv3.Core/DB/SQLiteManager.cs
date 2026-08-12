using SqlSugar;

namespace ChatGPTv3.Core.DB;

/// <summary>
/// SQLite database manager using SqlSugar ORM.
/// </summary>
public static class SQLiteManager
{
    public static string AppDirectory { get; set; } = string.Empty;

    public static SqlSugarClient GetInstance()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"data source={Path.Combine(AppDirectory, "core.db")}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute
        });
    }

    /// <summary>
    /// Creates DB and initializes tables via CodeFirst (v2-compatible names).
    /// Only creates tables that don't already exist.
    /// </summary>
    public static void CreateDB()
    {
        string dbPath = Path.Combine(AppDirectory, "core.db");
        using var db = GetInstance();
        db.DbMaintenance.CreateDatabase(dbPath);

        // Init tables — CodeFirst will create if not exists
        db.CodeFirst.InitTables(
            typeof(ChatRecord),
            typeof(Picture),
            typeof(Relationship),
            typeof(TokenUsage),
            typeof(APIKey),
            typeof(LLMModelConfig),
            typeof(ScheduledTask),
            typeof(OverrideConfig),
            typeof(ContextSummary),
            typeof(PurposeBinding)
        );

        // v3 schema migration: CodeFirst only creates missing tables, so add the
        // ApiFormat column explicitly for databases created before this feature.
        var apiKeyColumns = db.DbMaintenance.GetColumnInfosByTableName("APIKeys", false);
        if (apiKeyColumns.All(c => !string.Equals(c.DbColumnName, "ApiFormat", StringComparison.OrdinalIgnoreCase)))
        {
            db.DbMaintenance.AddColumn("APIKeys", new DbColumnInfo
            {
                DbColumnName = "ApiFormat",
                DataType = "INTEGER",
                IsNullable = true,
                DefaultValue = "0"
            });
        }

        if (apiKeyColumns.All(c => !string.Equals(c.DbColumnName, "EnableWebSearch", StringComparison.OrdinalIgnoreCase)))
        {
            db.DbMaintenance.AddColumn("APIKeys", new DbColumnInfo
            {
                DbColumnName = "EnableWebSearch",
                DataType = "INTEGER",
                IsNullable = true,
                DefaultValue = "1"
            });
        }
    }
}
