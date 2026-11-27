using System.IO;
using System.Windows;
using System.Windows.Threading;
using EOkulStudio.Desktop.ViewModels;
using EOkulStudio.Desktop.Views;
using EOkulStudio.Imaging;
using EOkulStudio.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Wpf.Ui.Appearance;

namespace EOkulStudio.Desktop;

/// <remarks>
/// Komut satırı: <c>--smoke</c> pencereyi açıp kapanır (otomatik test); <c>--data &lt;klasör&gt;</c> ayar ve günlük klasörü.
/// </remarks>
public partial class App : Application
{
    private IHost? _host;
    private AppPaths _paths = AppPaths.Default();
    private bool _smoke;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Herhangi bir yerel kütüphane yüklenmeden önce.
        NativeRuntime.PreloadVcRuntime(AppContext.BaseDirectory);

        _smoke = e.Args.Contains("--smoke");
        var dataIndex = Array.IndexOf(e.Args, "--data");
        if (dataIndex >= 0 && dataIndex + 1 < e.Args.Length) _paths = new AppPaths(Path.GetFullPath(e.Args[dataIndex + 1]));

        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "İşlenmeyen hata");
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Gözlenmeyen görev hatası");
            args.SetObserved();
        };

        try
        {
            Start();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Program başlatılamadı");
            Report("Program başlatılamadı.");
            Quit(1);
        }
    }

    private void Start()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(_paths.LogDirectory, "eokul-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Services.AddSerilog(Log.Logger);
        builder.Services.AddSingleton(_paths);
        builder.Services.AddSingleton(new SettingsStore(_paths.SettingsFile));
        builder.Services.AddSingleton<PhotoScanner>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();
        _host = builder.Build();
        _host.Start();

        var settings = _host.Services.GetRequiredService<SettingsStore>();
        settings.Load();
        if (settings.LastLoadFailed) Log.Warning("Ayar dosyası okunamadı; varsayılanlar kullanılıyor");
        Log.Information("Başlatıldı: {Version}", typeof(App).Assembly.GetName().Version);

        ApplicationThemeManager.ApplySystemTheme();
        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        if (_smoke)
        {
            // Pakette yerel kütüphanelerin yüklendiğini de doğrular.
            Log.Information("Yerel kütüphaneler: {Libraries}", NativeRuntime.Describe());
            window.ContentRendered += (_, _) =>
            {
                Log.Information("Duman testi: pencere çizildi");
                Quit(0);
            };
        }
        window.Show();
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Arayüz hatası");
        e.Handled = true;
        if (_smoke) Quit(1);
        else Report("Beklenmeyen bir hata oluştu.");
    }

    private void Report(string message)
    {
        if (_smoke) return;
        System.Windows.MessageBox.Show(
            $"{message}\n\nProgram günlüğü: {_paths.LogDirectory}",
            "E-Okul Foto Studio",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void Quit(int code)
    {
        Environment.ExitCode = code;
        Shutdown(code);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
