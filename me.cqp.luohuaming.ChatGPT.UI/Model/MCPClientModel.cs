using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.UI.Model
{
    public class MCPClientModel
    {
        public bool IsSelected { get; set; }

        public MCPClientBase MCPClientBase { get; set; }

        public string Name => MCPClientBase?.Name;

        public bool IsExpanded { get; set; }

        public bool IsHide { get; set; }

        public ObservableCollection<MCPToolModel> Tools { get; set; } = [];

        public static MCPClientModel BuildFromMCPClientBase(MCPClientBase clientBase, IEnumerable<AIFunction> tools)
        {
            var model = new MCPClientModel
            {
                MCPClientBase = clientBase
            };
            if (tools != null)
            {
                foreach (var tool in tools.OrderBy(x => x.Name))
                {
                    var name = clientBase.ToolNameConverters.FirstOrDefault(x => x.Value == tool.Name).Key;
                    model.Tools.Add(new MCPToolModel
                    {
                        OriginalName = string.IsNullOrEmpty(name) ? tool.Name : name,
                        Tool = tool,
                        Parent = model
                    });
                }
            }
            return model;
        }
    }

    public class MCPToolModel
    {
        public bool IsSelected { get; set; }

        public AIFunction Tool { get; set; }

        public MCPClientModel Parent { get; set; }

        public string OriginalName { get; set; }

        public string Name => OriginalName.Equals(Tool?.Name) ? Tool?.Name : $"{OriginalName}({Tool?.Name})";
    }
}
