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
                    model.Tools.Add(new MCPToolModel
                    {
                        ConvertedName = clientBase.ToolNameConverters.TryGetValue(tool?.Name, out string converted) ? converted : null,
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

        public string ConvertedName { get; set; }

        public string Name => string.IsNullOrEmpty(ConvertedName) ? Tool?.Name : $"{Tool?.Name}({ConvertedName})";
    }
}
