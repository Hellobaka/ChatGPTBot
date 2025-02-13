using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;

namespace me.cqp.luohuaming.ChatGPT.UI
{
    public class ChatBubble : UserControl
    {
        public static event Action<string> OnCopy;
        public static event Action<ChatBubble> OnRetry;

        public string Message { get; set; }

        public bool LeftAlign { get; set; }

        public ChatBubble(string message, bool leftAlign)
        {
            Message = message;
            LeftAlign = leftAlign;

            // 创建外层容器
            Grid container = new Grid();

            // 创建圆角矩形边框
            Border bubbleBorder = new Border
            {
                Background = leftAlign ? Brushes.White : new SolidColorBrush(Color.FromRgb(149, 236, 105)), // 绿色填充 (LightGreen)
                CornerRadius = new CornerRadius(15), // 圆角
                Padding = new Thickness(10), // 内边距
                Margin = new Thickness(5), // 外边距,
                HorizontalAlignment = leftAlign ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                BorderThickness = leftAlign ? new Thickness(1) : new Thickness(0),
                BorderBrush = Brushes.Black
            };

            // 创建文本
            TextBlock bubbleText = new TextBlock
            {
                Text = message, // 设置聊天内容
                Foreground = Brushes.Black, // 白色文本
                FontSize = 14, // 字体大小
                TextWrapping = TextWrapping.Wrap, // 自动换行
            };

            // 将文本添加到边框中
            bubbleBorder.Child = bubbleText;

            // 将边框添加到容器中
            container.Children.Add(bubbleBorder);

            // 将容器设置为 UserControl 的内容
            this.Content = container;

            AddContextMenu();
        }

        private void AddContextMenu()
        {
            // 创建上下文菜单
            ContextMenu contextMenu = new ContextMenu();

            // 创建 "复制" 菜单项
            MenuItem copyMenuItem = new MenuItem
            {
                Header = "复制"
            };
            copyMenuItem.Click += (s, e) =>
            {
                OnCopy?.Invoke(Message);
            };

            // 创建 "重试" 菜单项
            MenuItem retryMenuItem = new MenuItem
            {
                Header = "重试"
            };
            retryMenuItem.Click += (s, e) =>
            {
                OnRetry?.Invoke(this);
            };

            // 将菜单项添加到上下文菜单中
            contextMenu.Items.Add(copyMenuItem);
            contextMenu.Items.Add(retryMenuItem);

            // 将上下文菜单附加到聊天气泡控件
            this.ContextMenu = contextMenu;
        }
    }
}
