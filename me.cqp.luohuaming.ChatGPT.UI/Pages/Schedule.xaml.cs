using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.PublicInfos.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace me.cqp.luohuaming.ChatGPT.UI.Pages
{
    /// <summary>
    /// Schedule.xaml 的交互逻辑
    /// </summary>
    public partial class Schedule : Page
    {
        public Schedule()
        {
            InitializeComponent();
        }

        public List<(DateTime time, string action)> Schedules { get; private set; } = [];

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SchedulerManager.Instance.LastUpdateTime = DateTime.Now;
            SchedulerManager.Instance.Schedules.Clear();
            foreach (var item in Schedules)
            {
                SchedulerManager.Instance.Schedules.Add(item);
            }

            string path = Path.Combine(MainSave.AppDirectory, "schedules.json");
            File.WriteAllText(path, new
            {
                LastUpdateTime = DateTime.Now,
                Schedules = Schedules.Select(x => new { Time = x.time, Action = x.action }).ToArray()
            }.ToJson(true));

            ReloadScheduleList();
            MainWindow.ShowInfo("保存成功");
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedIndex < 0)
            {
                MainWindow.ShowInfo("请选择要删除的日程");
                return;
            }
            else
            {
                Schedules.RemoveAt(ScheduleList.SelectedIndex);
                ScheduleList.Items.RemoveAt(ScheduleList.SelectedIndex);
            }
        }

        private void SelectSaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ScheduleList.SelectedIndex < 0)
            {
                MainWindow.ShowInfo("请选择要修改的日程");
                return;
            }
            var input = SelectItemTime.Text.Replace("：", ":").Split(':');
            if (input.Length < 2 || input.Length > 3)
            {
                MainWindow.ShowInfo("时间格式不正确");
                return;
            }
            if (!int.TryParse(input[0], out int hour) || !int.TryParse(input[1], out int minute))
            {
                MainWindow.ShowInfo("时间格式不正确");
                return;
            }
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
            {
                MainWindow.ShowInfo("时间格式不正确");
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectItemSchedule.Text))
            {
                MainWindow.ShowInfo("请输入日程内容");
                return;
            }
            var c = Schedules[ScheduleList.SelectedIndex];
            c.time = new DateTime(2000, 1, 1, hour, minute, 0);
            c.action = SelectItemSchedule.Text;
            Schedules[ScheduleList.SelectedIndex] = c;
            ReloadScheduleList();
        }

        private void ReloadScheduleList()
        {
            ScheduleList.Items.Clear();
            foreach (var (time, action) in Schedules)
            {
                ScheduleList.Items.Add($"{time.ToShortTimeString()} - {action}");
            }
            ScheduleEmptyHint.Visibility = Schedules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateTime.Text = $"更新时间：{SchedulerManager.Instance.LastUpdateTime:G}";
        }

        private void SelectAddButton_Click(object sender, RoutedEventArgs e)
        {
            var input = SelectItemTime.Text.Replace("：", ":").Split(':');
            if (input.Length < 2 || input.Length > 3)
            {
                MainWindow.ShowInfo("时间格式不正确");
                return;
            }
            if (!int.TryParse(input[0], out int hour) || !int.TryParse(input[1], out int minute))
            {
                MainWindow.ShowInfo("时间格式不正确");
                return;
            }
            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
            {
                MainWindow.ShowInfo("时间格式不正确");
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectItemSchedule.Text))
            {
                MainWindow.ShowInfo("请输入日程内容");
                return;
            }
            Schedules.Add((new DateTime(2000, 1, 1, hour, minute, 0), SelectItemSchedule.Text));
            Schedules = Schedules.OrderBy(x => x.time).ToList();
            ReloadScheduleList();
        }

        private void PromptRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            string prompt = string.Format(SchedulerManager.Prompt, AppConfig.BotName, AppConfig.SchedulePrompt, DateTime.Now.ToLongDateString(), DateTime.Now.ToString("dddd"));
            SchedulePrompt.Text = prompt;
        }

        private async void PromptRegenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SchedulePrompt.Text))
            {
                MainWindow.ShowError("请先生成日程Prompt");
                return;
            }
            string prompt = SchedulePrompt.Text;
            ToggleButtonEnableStatus(false);
            var results = await Task.Run(() => SchedulerManager.GetSchedule(prompt));
            ToggleButtonEnableStatus(true);
            if (results.Count == 0)
            {
                MainWindow.ShowError("生成日程失败");
                return;
            }
            Schedules.Clear();
            foreach (var (time, action) in results)
            {
                Schedules.Add((time, action));
            }
            ReloadScheduleList();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (SchedulerManager.Instance.Schedules.Count > 0)
            {
                Schedules.Clear();
                foreach (var item in SchedulerManager.Instance.Schedules)
                {
                    Schedules.Add(item);
                }
                ReloadScheduleList();
                UpdateTime.Text = $"更新时间：{SchedulerManager.Instance.LastUpdateTime:G}";
            }
            if (string.IsNullOrWhiteSpace(SchedulePrompt.Text))
            {
                PromptRefreshButton_Click(sender, e);
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (SchedulerManager.Instance.Schedules.Count > 0)
            {
                Schedules.Clear();
                foreach (var item in SchedulerManager.Instance.Schedules)
                {
                    Schedules.Add(item);
                }
                ReloadScheduleList();
            }
            else
            {
                MainWindow.ShowError("没有日程可供刷新");
            }
        }

        private void ScheduleList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ScheduleList.SelectedIndex < 0)
            {
                return;
            }
            var c = Schedules[ScheduleList.SelectedIndex];
            SelectItemTime.Text = $"{c.time.Hour:00}:{c.time.Minute:00}";
            SelectItemSchedule.Text = c.action;
        }

        private void ToggleButtonEnableStatus(bool enable)
        {
            ReloadButton.IsEnabled = enable;
            DeleteButton.IsEnabled = enable;
            SaveButton.IsEnabled = enable;
            PromptRefreshButton.IsEnabled = enable;
            PromptRegenerateButton.IsEnabled = enable;
            SelectAddButton.IsEnabled = enable;
            SelectSaveButton.IsEnabled = enable;

            SchedulePrompt.IsEnabled = enable;
            SelectItemTime.IsEnabled = enable;
            SelectItemSchedule.IsEnabled = enable;
            ScheduleList.IsEnabled = enable;

            Regenerating.Visibility = enable ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
