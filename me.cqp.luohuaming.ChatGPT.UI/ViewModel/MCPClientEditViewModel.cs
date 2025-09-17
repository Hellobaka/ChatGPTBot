using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using me.cqp.luohuaming.ChatGPT.UI.Model;
using ModelContextProtocol.Client;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.UI.ViewModel
{
    public class MCPClientEditViewModel : INotifyPropertyChanged
    {
        public MCPClientEditViewModel(MCPClientModel clientModel)
        {
            MCPClientModel = clientModel;

            Name = clientModel.MCPClientBase.Name;
            Enabled = clientModel.MCPClientBase.Enabled;
            GroupEnabled = clientModel.MCPClientBase.GroupEnabled;
            PersonEnabled = clientModel.MCPClientBase.PersonEnabled;
            IsGroupBlackList = clientModel.MCPClientBase.IsGroupBlackList;
            IsPersonBlackList = clientModel.MCPClientBase.IsPersonBlackList;
            Groups = new ObservableCollection<long>(clientModel.MCPClientBase.Groups);
            Persons = new ObservableCollection<long>(clientModel.MCPClientBase.Persons);
            ToolType = clientModel.MCPClientBase.ToolType.ToString();
            if (MCPClientModel.MCPClientBase is MCPStdioClient stdioClient)
            {
                Command = stdioClient.Command;
                Arguments = new ObservableCollection<string>(stdioClient.Arguments);
                EnvironmentVariables = new Dictionary<string, string>(stdioClient.EnvironmentVariables);
                WorkingDirectory = stdioClient.WorkingDirectory;
            }
            else if (MCPClientModel.MCPClientBase is MCPHttpClient httpClient)
            {
                Endpoint = httpClient.Endpoint;
                Headers = new Dictionary<string, string>(httpClient.Headers);
                ConnectionTimeout = (int)httpClient.ConnectionTimeout.TotalSeconds;
                TransportType = httpClient.TransportType.ToString();
            }
        }

        public string Name { get; set; }

        public bool Enabled { get; set; }

        public bool GroupEnabled { get; set; }

        public bool PersonEnabled { get; set; }

        public bool IsGroupBlackList { get; set; }

        public ObservableCollection<long> Groups { get; set; } = [];

        public bool IsPersonBlackList { get; set; }

        public ObservableCollection<long> Persons { get; set; } = [];

        public string ToolType { get; set; }

        public string Command { get; set; }

        public ObservableCollection<string> Arguments { get; set; } = [];

        public Dictionary<string, string> EnvironmentVariables { get; set; } = [];

        public string TransportType { get; set; }

        public string WorkingDirectory { get; set; } = string.Empty;

        public string Endpoint { get; set; } = string.Empty;

        public Dictionary<string, string> Headers { get; set; } = [];

        public int ConnectionTimeout { get; set; }

        private MCPClientModel MCPClientModel { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MCPClientModel Build()
        {
            return new MCPClientModel
            {
                MCPClientBase = ToolType switch
                {
                    "STDIO" => new MCPStdioClient
                    {
                        Name = Name,
                        Enabled = Enabled,
                        GroupEnabled = GroupEnabled,
                        PersonEnabled = PersonEnabled,
                        IsGroupBlackList = IsGroupBlackList,
                        Groups = Groups.ToArray(),
                        IsPersonBlackList = IsPersonBlackList,
                        Persons = Persons.ToArray(),
                        Command = Command,
                        Arguments = Arguments.ToList(),
                        EnvironmentVariables = new Dictionary<string, string>(EnvironmentVariables),
                        WorkingDirectory = WorkingDirectory
                    },
                    "Http" => new MCPHttpClient
                    {
                        Name = Name,
                        Enabled = Enabled,
                        GroupEnabled = GroupEnabled,
                        PersonEnabled = PersonEnabled,
                        IsGroupBlackList = IsGroupBlackList,
                        Groups = Groups.ToArray(),
                        IsPersonBlackList = IsPersonBlackList,
                        Persons = Persons.ToArray(),
                        Endpoint = Endpoint,
                        Headers = new Dictionary<string, string>(Headers),
                        ConnectionTimeout = TimeSpan.FromSeconds(ConnectionTimeout),
                        TransportType = Enum.TryParse<HttpTransportMode>(TransportType, out var transportType) ? transportType : HttpTransportMode.AutoDetect
                    },
                    _ => throw new NotSupportedException($"Unsupported ToolType: {ToolType}")
                },
                Tools = new ObservableCollection<MCPToolModel>(MCPClientModel.Tools)
            };
        }
    }
}
