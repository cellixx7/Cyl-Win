using System.Windows;

namespace CylWin;

public partial class App : Application
{
    private Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Só uma instância do Cyl Win por vez.
        _single = new Mutex(true, "CylWin.SingleInstance", out bool first);
        if (!first)
        {
            Shutdown();
            return;
        }

        new TopBar().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _single?.Dispose();
        base.OnExit(e);
    }
}
