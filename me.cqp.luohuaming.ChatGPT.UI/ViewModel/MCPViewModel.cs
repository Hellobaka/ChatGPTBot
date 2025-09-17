using me.cqp.luohuaming.ChatGPT.UI.Model;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public ObservableCollection<object> SelectedMCPItem { get; set; }

        public bool Rebuilding { get; set; }
    }
}
