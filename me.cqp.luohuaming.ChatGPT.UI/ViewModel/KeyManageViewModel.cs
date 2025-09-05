using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.UI.ViewModel
{
    public class KeyManageViewModel : INotifyPropertyChanged
    {
        public KeyManageViewModel()
        {
            LoadKeys();
        }

        public event PropertyChangedEventHandler PropertyChanged;
       
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<APIKeys> APIKeys { get; set; } = [];

        public void LoadKeys()
        {
            APIKeys.Clear();
            foreach (var key in PublicInfos.DB.APIKeys.GetAllKeys())
            {
                APIKeys.Add(key);
            }
        }
    }
}
