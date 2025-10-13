using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.UI.Model
{
    [AddINotifyPropertyChangedInterface]
    public class MemoryNode
    {
        public string Id { get; set; }

        public string Message { get; set; }

        public double Score { get; set; }

        public DateTime Time { get; set; }
    }
}
