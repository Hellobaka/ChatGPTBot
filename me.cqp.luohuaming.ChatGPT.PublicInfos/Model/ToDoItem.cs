using me.cqp.luohuaming.ChatGPT.PublicInfos.Model.CustomTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class ToDoItem
    {
        public static int NextId { get; set; } = 1;

        public int Id { get; set; }

        public bool IsGlobalTodo { get; set; }

        public CustomToolContext Context { get; set; }

        public string ToDo { get; set; }

        public DateTime CreateTime { get; set; }

        public bool Completed { get; set; }

        public DateTime CompletedTime { get; set; }

        public override string ToString()
        {
            return $"- [{(Completed ? "x" : " ")}] ID={Id};内容：{ToDo};添加时间：{CreateTime:G}"
                + (Completed ? $";完成时间：{CompletedTime:G}" : "");
        }
    }
}
