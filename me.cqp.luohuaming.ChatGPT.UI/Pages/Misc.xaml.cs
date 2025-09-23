using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// Misc.xaml 的交互逻辑
    /// </summary>
    public partial class Misc : Page
    {
        public Misc()
        {
            InitializeComponent();
        }

        private Dictionary<string, object> PageCache { get; set; } = new();

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = e.AddedItems.Count > 0 ? e.AddedItems[0] : null;
            if (selectedItem != null && selectedItem is TabItem tabItem)
            {
                string selectedItemTag = (string)tabItem.Tag;
                if (PageCache.TryGetValue(selectedItemTag, out object? page))
                {
                    MiscFrame.Navigate(page);
                }
                else
                {
                    Type? pageType = typeof(MainWindow).Assembly.GetType("me.cqp.luohuaming.ChatGPT.UI.Pages." + selectedItemTag);
                    if (pageType == null)
                    {
                        return;
                    }
                    var obj = Activator.CreateInstance(pageType);
                    if (obj == null)
                    {
                        return;
                    }
                    PageCache.Add(selectedItemTag, obj);

                    MiscFrame.Navigate(obj);
                }
            }
        }
    }
}
