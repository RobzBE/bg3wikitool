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
        Application.AddMessageFilter(new SafeOptionWheelMessageFilter());
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
            if (arguments.Length >= 3 && arguments[0] == "--save-self-test")
            {
                AppDiagnostics.WriteSaveImportReportAsync(items, arguments[1], arguments[2]).GetAwaiter().GetResult();
                return;
            }
            if (arguments.Length >= 2 && arguments[0] == "--render-preview")
            {
                using var previewForm = new MainForm(items);
                previewForm.Show();
                var width = arguments.Length >= 4 && int.TryParse(arguments[2], out var parsedWidth) ? parsedWidth : (int?)null;
                var height = arguments.Length >= 4 && int.TryParse(arguments[3], out var parsedHeight) ? parsedHeight : (int?)null;
                previewForm.RenderPreview(arguments[1], width, height);
                previewForm.Close();
                return;
            }
            if (arguments.Length >= 2 && arguments[0] == "--render-preview-nl")
            {
                Localization.Current = UiLanguage.Dutch;
                using var previewForm = new MainForm(items);
                previewForm.Show();
                previewForm.RenderPreview(arguments[1]);
                previewForm.Close();
                return;
            }
            if (arguments.Length >= 2 && arguments[0] == "--header-self-test")
            {
                using var testForm = new MainForm(items);
                testForm.Show();
                Application.DoEvents();
                testForm.RunHeaderVisibilityTest(arguments[1]);
                testForm.Close();
                return;
            }
            if (arguments.Length >= 2 && arguments[0] == "--filter-self-test")
            {
                using var testForm = new MainForm(items);
                testForm.Show();
                Application.DoEvents();
                testForm.RunFilterStressTest(arguments[1]);
                testForm.Close();
                return;
            }
            using var mainForm = new MainForm(items);
            mainForm.EnableAlwaysMaximized();
            Application.Run(mainForm);
        }
        catch (Exception exception)
        {
            if (arguments.Length >= 2 && arguments[0].StartsWith("--", StringComparison.Ordinal))
            {
                File.WriteAllText(arguments[1] + ".error.txt", exception.ToString());
                Environment.ExitCode = 1;
                return;
            }
            var logPath = Path.Combine(AppContext.BaseDirectory, "BG3-Item-Explorer-error.log");
            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now:O}]{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch
            {
                logPath = "(log could not be written)";
            }
            MessageBox.Show(
                Localization.Format("LoadError", exception.Message, logPath),
                Localization.T("WarningTitle"),
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
