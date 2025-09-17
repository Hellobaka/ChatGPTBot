using PropertyChanged;
using System.ComponentModel;
using System.Text.Json;

namespace me.cqp.luohuaming.ChatGPT.UI.Model
{
    public class ToolArgumentItem : INotifyPropertyChanged
    {
        public string ArgumentName { get; set; }

        public JsonValueKind ArgumentType { get; set; }

        public bool Required { get; set; }

        public string Description { get; set; }

        public string DefaultValue { get; set; }

        public string Value { get; set; }


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
