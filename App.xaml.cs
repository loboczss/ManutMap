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
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var culture = new CultureInfo("pt-BR");
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            bool showUpdate = false;
            try
            {
                var svc = new Services.AtualizadorService();
                var (localVer, remoteVer) = await svc.GetVersionsAsync();
                showUpdate = remoteVer > localVer;
            }
            catch
            {
                // Falha ao verificar; assume sem atualização
            }

            if (showUpdate)
            {
                var updateWindow = new UpdateWindow();
                updateWindow.ShowDialog();

                if (updateWindow.UpdateInitiated)
                {
                    Shutdown();
                    return;
                }
            }

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
    }

}
