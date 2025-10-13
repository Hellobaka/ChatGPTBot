using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.UI.Model;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace me.cqp.luohuaming.ChatGPT.UI.ViewModel
{
    public class MCPViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<MCPClientModel> MCPClients { get; set; } = [];

        public object SelectedMCPItem { get; set; }

        public bool Rebuilding { get; set; } = true;

        public bool CreateMode { get; set; }

        public MCPClientModel CreatedMCPItem { get; internal set; }

        public void LoadMCPClients(bool showCustom)
        {
            MCPClients.Clear();

            foreach (var client in MCPClientManager.Clients)
            {
                if (client.ToolType == MCPClientType.Custom && !showCustom)
                {
                    continue;
                }
                if (!MCPClientManager.MCPTools.TryGetValue(client, out var tools))
                {
                    tools = [];
                }
                var model = MCPClientModel.BuildFromMCPClientBase(client, tools);
                MCPClients.Add(model);
            }
        }
    }
}
