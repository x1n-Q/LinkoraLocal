using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace Linkora.Local;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private Forms.NotifyIcon? _trayIcon;
    public bool IsQuitting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "Linkora.Local.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show(
                "Linkora Local is already running in the notification area.",
                "Linkora Local",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        if (Environment.GetEnvironmentVariable("LINKORA_SOFTWARE_RENDERING") == "1")
        {
            RenderOptions.ProcessRenderMode =
                System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        var window = new MainWindow();
        MainWindow = window;
        CreateTrayIcon(window);
        window.Show();
    }

    private void CreateTrayIcon(MainWindow window)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Linkora Local", null, (_, _) => ShowWindow(window));
        menu.Items.Add("Stop publishing", null, async (_, _) => await window.StopPublishingAsync());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, async (_, _) => await QuitAsync(window));

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Linkora Local",
            Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
                   ?? SystemIcons.Shield,
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => ShowWindow(window);
    }

    private static void ShowWindow(MainWindow window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }
        window.Activate();
    }

    public async Task QuitAsync(MainWindow window)
    {
        IsQuitting = true;
        await window.StopPublishingAsync();
        _trayIcon?.Dispose();
        _trayIcon = null;
        window.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
