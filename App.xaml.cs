using System;
using System.Threading;
using System.Windows;

namespace MeuPlayer;

public partial class App : System.Windows.Application
{
    private Mutex? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        instance = new Mutex(true, @"Local\MeuPlayer.2026", out bool first);
        if (!first)
        {
            Shutdown();
            return;
        }
        base.OnStartup(e);
        MainWindow = new PlayerWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
