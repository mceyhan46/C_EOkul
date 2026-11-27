using System.IO;
using System.Windows;

namespace EOkulSpike;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // ONNX Runtime yüklenmeden önce çağrılmalı.
        VcRuntime.Preload(AppContext.BaseDirectory);

        var selfTest = e.Args.Contains("--selftest");
        var outIndex = Array.IndexOf(e.Args, "--out");
        var outDir = outIndex >= 0 && outIndex + 1 < e.Args.Length
            ? Path.GetFullPath(e.Args[outIndex + 1])
            : Path.Combine(Path.GetTempPath(), "EOkulSpike");

        if (selfTest)
        {
            int code;
            try { code = SelfTest.Run(AppContext.BaseDirectory, outDir).Success ? 0 : 1; }
            catch (Exception ex)
            {
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "crash.txt"), ex.ToString());
                code = 2;
            }
            Environment.ExitCode = code;
            Shutdown(code);
            return;
        }

        // --uitest: pencereyi açar, deneme ve görseller bitince çıkış koduyla kapanır (CI).
        Action<bool>? finished = e.Args.Contains("--uitest")
            ? ok =>
            {
                Environment.ExitCode = ok ? 0 : 1;
                Shutdown(ok ? 0 : 1);
            }
            : null;
        new MainWindow(outDir, finished).Show();
    }
}
