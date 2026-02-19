using HGERSaveEditor.Forms;

namespace HGERSaveEditor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += OnThreadException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string? initialFile = args.Length > 0 && File.Exists(args[0]) ? args[0] : null;
        Application.Run(new MainForm(initialFile));
    }

    private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
    {
        MessageBox.Show(
            $"오류가 발생하여 프로그램을 종료합니다.\n\n{e.Exception.Message}",
            "오류",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        Environment.Exit(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        string message = e.ExceptionObject is Exception ex ? ex.Message : e.ExceptionObject?.ToString() ?? "알 수 없는 오류";
        MessageBox.Show(
            $"오류가 발생하여 프로그램을 종료합니다.\n\n{message}",
            "오류",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        Environment.Exit(1);
    }
}
