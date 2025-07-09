using System.Configuration;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Windows;

namespace ManutMap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var culture = new CultureInfo("pt-BR");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            var updateWindow = new UpdateWindow();
            updateWindow.ShowDialog();

            if (!updateWindow.UpdateInitiated)
            {
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }

}
