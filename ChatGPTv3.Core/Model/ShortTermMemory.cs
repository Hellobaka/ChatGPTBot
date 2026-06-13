namespace ChatGPTv3.Core.Model;

/// <summary>
/// Short-term memory item — stored in-memory, persisted as JSON.
/// Has a limited use count; auto-expires when exceeded.
/// </summary>
public class ShortTermMemory
{
    public static int NextId { get; set; } = 1;
    public int Id { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.Now;
    public string Memory { get; set; } = string.Empty;
    public int UsedCount { get; set; }
    public long GroupId { get; set; }
    public long QQ { get; set; }

    public override string ToString()
        => $"ID={Id};记忆内容:{Memory};创建时间:{CreateTime:G};已使用次数:{UsedCount};";
}

/// <summary>
/// To-do item — stored in-memory, persisted as JSON.
/// </summary>
public class ToDoItem
{
    public static int NextId { get; set; } = 1;
    public int Id { get; set; }
    public string ToDo { get; set; } = string.Empty;
    public bool IsGlobal { get; set; }
    public long GroupId { get; set; }
    public long QQ { get; set; }
    public DateTime CreateTime { get; set; } = DateTime.Now;
    public bool Completed { get; set; }
    public DateTime? CompletedTime { get; set; }

    public override string ToString()
        => $"ID={Id};代办:{ToDo};是否全局:{IsGlobal};创建时间:{CreateTime:G};已完成:{Completed};";
}
