using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SoundConsole.Core.Settings;

namespace SoundConsole;

public partial class App : Application
{
    private const string MutexName = @"Local\SoundConsole.App.8E5F1C1B";
    private Mutex? _instanceMutex;
    private bool _ownsMutex;
    private bool _handlingException;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            MessageBox.Show(
                "声卡控制台已经在运行。\n请切换到已打开的窗口。",
                "声卡控制台",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);
        AppPaths.EnsureCreated();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLog.Error("未处理的应用程序异常。", ex);
            }
        };

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instanceMutex is not null)
        {
            if (_ownsMutex)
            {
                try
                {
                    _instanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore shutdown races.
                }
            }

            _instanceMutex.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.Error("UI 线程发生未处理异常。", e.Exception);
        if (_handlingException)
        {
            return;
        }

        _handlingException = true;
        e.Handled = true;
        MessageBox.Show(
            "程序遇到一个未处理的错误，操作可能没有完成。\n\n" +
            e.Exception.Message +
            "\n\n详细信息已写入日志目录。",
            "声卡控制台",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        _handlingException = false;
    }
}
