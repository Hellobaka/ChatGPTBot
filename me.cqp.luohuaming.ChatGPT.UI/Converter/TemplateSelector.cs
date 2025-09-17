using me.cqp.luohuaming.ChatGPT.UI.Model;
using System.Windows;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Converter
{
    public class MCPEditSelector : DataTemplateSelector
    {
        public DataTemplate MCPClientEditTemplate { get; set; }

        public DataTemplate MCPToolTestTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item is MCPClientModel ? MCPClientEditTemplate : MCPToolTestTemplate;
        }
    }
}
