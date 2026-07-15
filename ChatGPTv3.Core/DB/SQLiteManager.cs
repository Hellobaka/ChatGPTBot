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
    }
}