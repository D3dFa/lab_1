namespace OptimizationLabApp;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (TryExportScreenshot(args))
        {
            return;
        }

        Application.Run(new Form1());
    }

    private static bool TryExportScreenshot(string[] args)
    {
        if (args.Length != 2 || !string.Equals(args[0], "--export-ui-screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        using var form = new Form1
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000)
        };

        form.Show();
        Application.DoEvents();
        form.PrepareDemoState();
        Application.DoEvents();
        form.ExportScreenshot(args[1]);
        form.Close();
        return true;
    }
}
