using System.ComponentModel;

namespace me.cqp.luohuaming.ChatGPT.UI.Model
{
    public class CheckableItem : INotifyPropertyChanged
    {
        private string name;

        public string Name
        {
            get { return name; }
            set
            {
                if (value != name)
                {
                    name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        private bool check;

        public bool Checked
        {
            get { return check; }
            set
            {
                if (value != check)
                {
                    check = value;
                    OnPropertyChanged(nameof(Checked));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
