using LiveChartsCore.Kernel;
using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.UI.Model
{
    public class TimeGroupData 
    {
        public string GroupName { get; set; }

        public IGrouping<string, Usage> Group { get; set; }

        public string Format { get; set; }
    }
}
