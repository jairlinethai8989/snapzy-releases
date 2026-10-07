using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.ApplicationServices;
using System.IO.Pipes;
using Microsoft.Win32.SafeHandles;

[assembly: Guid(SnapCraft.AppInfo.InstanceId)]

namespace SnapCraft;

internal static class Program
{
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppInfo.AppUserModelId));
            GrantForegroundToRunningInstance();
            new SnapzyApplication(args.Contains("--tray")).Run(args);
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, AppInfo.ProductName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void GrantForegroundToRunningInstance()
    {
        // The framework's pipe carries activation but does not transfer the shell's foreground permission.
        var version = typeof(Program).Assembly.GetName().Version!;
        using var pipe = new NamedPipeClientStream(".", $"{AppInfo.InstanceId}{version.Major}.{version.Minor}", PipeDirection.Out, PipeOptions.CurrentUserOnly);
        try
        {
            pipe.Connect(50);
            if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var processId)) AllowSetForegroundWindow(processId);
        }
        catch (Exception error) when (error is TimeoutException or IOException or UnauthorizedAccessException) { }
    }

    private sealed class SnapzyApplication : WindowsFormsApplicationBase
    {
        private readonly bool startInTray;
        public SnapzyApplication(bool startInTray)
        {
            this.startInTray = startInTray;
            IsSingleInstance = true;
            EnableVisualStyles = true;
            HighDpiMode = HighDpiMode.PerMonitorV2;
            ShutdownStyle = ShutdownMode.AfterMainFormCloses;
        }

        protected override void OnCreateMainForm()
        {
            MainForm = new MainForm(startInTray);
        }

        protected override void OnStartupNextInstance(StartupNextInstanceEventArgs eventArgs)
        {
            eventArgs.BringToForeground = false;
            base.OnStartupNextInstance(eventArgs);
            if (!eventArgs.CommandLine.Contains("--tray") && MainForm is MainForm launcher) launcher.ShowLauncher();
        }
    }
}
