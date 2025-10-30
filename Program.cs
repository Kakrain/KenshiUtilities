namespace KenshiUtilities;

static class Program
{
    [STAThread]
    static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            if (ex != null)
            {
                File.AppendAllText("global_errors.log", $"[Unhandled {DateTime.Now}] {ex}\n");
            }
        };

        Application.ThreadException += (sender, e) =>
        {
            File.AppendAllText("global_errors.log", $"[ThreadException {DateTime.Now}] {e.Exception}\n");
        };

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }    
}