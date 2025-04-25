using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using PropertyChanged;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// Mood.xaml 的交互逻辑
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public partial class Mood : Page
    {
        public Mood()
        {
            InitializeComponent();
            DataContext = this;
        }
        private bool Dragging { get; set; }

        private Point DragOffset { get; set; } = new();

        private Point OriginPoint { get; set; } = new();

        public double Valence { get; set; }

        public double Arousal { get; set; }

        public string CurrentMood { get; set; }

        public bool PageLoaded { get; set; }

        private void MoodPoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Dragging = true;
            DragOffset = e.GetPosition(MoodPoint);
            OriginPoint = e.GetPosition(MoodCanvas);
            MoodPoint.CaptureMouse();
            UpdateTooltip(OriginPoint);
            MoodDisplay.IsOpen = true;
        }

        private void MoodPoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Dragging = false;
            MoodPoint.ReleaseMouseCapture();
            MoodDisplay.IsOpen = false;
        }

        private void MoodCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (Dragging)
            {
                var currentPoint = e.GetPosition(MoodCanvas);
                double targetLeft = Math.Max(0, Math.Min(currentPoint.X - DragOffset.X, 400 - 5));
                double targetTop = Math.Max(0, Math.Min(currentPoint.Y - DragOffset.Y, 400 - 5));
                Canvas.SetLeft(MoodPoint, targetLeft);
                Canvas.SetTop(MoodPoint, targetTop);
                UpdateTooltip(currentPoint);
            }
        }

        private void UpdateTooltip(Point position)
        {
            double left = Canvas.GetLeft(MoodPoint) + 5;
            double top = Canvas.GetTop(MoodPoint) + 5;

            Valence = (left - 200) / 200.0;
            Arousal = (200 - top) / 200.0;

            MoodManager.Instance.Valence = Valence;
            MoodManager.Instance.Arousal = Arousal;

            CurrentMood = MoodManager.GetCurrentMoodText(Valence, Arousal);
            MoodDisplay.Content = $"({Valence:F2}, {Arousal:F2})\n{CurrentMood}";

            MoodDisplay.HorizontalOffset = position.X - OriginPoint.X;
            MoodDisplay.VerticalOffset = position.Y - OriginPoint.Y;
        }

        private void SetPoint(double valence, double arousal)
        {
            double left = 200 + valence * 200;
            double top = 200 - arousal * 200;
            Dispatcher.BeginInvoke(() =>
            {
                Canvas.SetLeft(MoodPoint, left - 5);
                Canvas.SetTop(MoodPoint, top - 5);
                UpdateTooltip(OriginPoint);
            });
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (PageLoaded)
            {
                return;
            }
            PageLoaded = true;
            SetPoint(MoodManager.Instance.Valence, MoodManager.Instance.Arousal);

            // 添加原点按钮
            var origin = new Button()
            {
                Margin = new Thickness(0, 5, 5, 5),
                Content = "原点",
                Tag = (0.0, 0.0)
            };
            origin.Click += MoodChange_Click;
            MoodControl1Panel.Children.Add(origin);

            foreach (var item in MoodManager.MoodMap)
            {
                var button = new Button()
                {
                    Margin = new Thickness(0,5,5,5),
                    Content = item.Key,
                    Tag = item.Value
                };
                button.Click += MoodChange_Click;
                MoodControl1Panel.Children.Add(button);
            }

            foreach(var item in MoodManager.MoodValues)
            {
                var button = new Button()
                {
                    Margin = new Thickness(0,5,5,5),
                    Content = item.Key.ToString(),
                    Tag = item.Key
                };
                button.Click += MoodDiff_Click;
                MoodControl2Panel.Children.Add(button);
            }

            MoodManager.Instance.MoodChanged += Instance_MoodChanged;
        }

        private void Instance_MoodChanged()
        {
            SetPoint(MoodManager.Instance.Valence, MoodManager.Instance.Arousal);
        }

        private void MoodChange_Click(object sender, RoutedEventArgs e)
        {
            var (valence, arousal) = ((double valence, double arousal))(sender as Button).Tag;
            MoodManager.Instance.Valence = valence;
            MoodManager.Instance.Arousal = arousal;

            SetPoint(MoodManager.Instance.Valence, MoodManager.Instance.Arousal);
        }

        private void MoodDiff_Click(object sender, RoutedEventArgs e)
        {
            MoodManager.Mood mood = (MoodManager.Mood)(sender as Button).Tag;
            MoodManager.Instance.UpdateMood(mood);

            SetPoint(MoodManager.Instance.Valence, MoodManager.Instance.Arousal);
        }
    }
}
