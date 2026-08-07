namespace BG3ItemExplorer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        FontManager.Initialize();
        if (arguments.Contains("--lang=nl", StringComparer.OrdinalIgnoreCase))
            Localization.Current = UiLanguage.Dutch;

        try
        {
            var items = ItemRepository.LoadEmbedded();
            if (arguments.Length >= 2 && arguments[0] == "--self-test")
            {
                AppDiagnostics.WriteSelfTestReport(items, arguments[1]);
                return;
            }
            if (arguments.Length >= 2 && arguments[0] == "--render-preview")
            {
                using var previewForm = new MainForm(items);
                previewForm.Show();
                Application.DoEvents();
                previewForm.RenderPreview(arguments[1]);
                previewForm.Close();
                return;
            }
            if (arguments.Length >= 2 && arguments[0] == "--render-preview-nl")
            {
                Localization.Current = UiLanguage.Dutch;
                using var previewForm = new MainForm(items);
                previewForm.Show();
                Application.DoEvents();
                previewForm.RenderPreview(arguments[1]);
                previewForm.Close();
                return;
            }
            Application.Run(new MainForm(items));
        }
        catch (Exception exception)
        {
            if (arguments.Length >= 2 && arguments[0].StartsWith("--", StringComparison.Ordinal))
            {
                File.WriteAllText(arguments[1] + ".error.txt", exception.ToString());
                Environment.ExitCode = 1;
                return;
            }
            MessageBox.Show(
                Localization.Format("LoadError", exception.Message),
                Localization.T("WarningTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
