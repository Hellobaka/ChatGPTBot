using me.cqp.luohuaming.ChatGPT.PublicInfos;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.EventArgs;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Interface;
using System;
using System.Threading;
using System.Windows;

namespace me.cqp.luohuaming.ChatGPT.UI
{
    public class Event_MenuCall : IMenuCall
    {
        private App App { get; set; }

        public void MenuCall(object sender, CQMenuCallEventArgs e)
        {
            try
            {
                if (App == null)
                {
                    MainWindow.OnWindowClosing += MainWindow_OnWindowClosing;
                    Thread thread = new(() =>
                    {
                        App = new();
                        App.ShutdownMode = ShutdownMode.OnMainWindowClose;
                        App.InitializeComponent();
                        App.Run();
                    });
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                }
                else
                {
                    MainWindow.Instance.Dispatcher.BeginInvoke(new Action(MainWindow.Instance.Show));
                }
            }
            catch (Exception exc)
            {
                MainSave.CQLog.Info("Error", exc.Message, exc.StackTrace);
            }
        }

        private void MainWindow_OnWindowClosing()
        {
            MainWindow.Instance.Dispatcher.BeginInvoke(new Action(MainWindow.Instance.Hide));
        }
    }
}
