using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace EOkulSpike;

public partial class MainWindow : Window
{
    private readonly string _outDir;

    public MainWindow(string outDir)
    {
        _outDir = outDir;
        InitializeComponent();
        Loaded += async (_, _) => await RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            var result = await Task.Run(() => SelfTest.Run(AppContext.BaseDirectory, _outDir));
            Status.Text = result.Success ? "BAŞARILI: tüm zorunlu denemeler geçti." : "BAŞARISIZ: aşağıdaki HATA satırlarına bakın.";
            Report.Text = result.Report + Environment.NewLine + "Çıktı klasörü: " + _outDir;
            Before.Source = Load(Path.Combine(AppContext.BaseDirectory, "astronaut.png"))
                            ?? Load(Path.Combine(AppContext.BaseDirectory, "samples", "astronaut.png"));
            Alpha.Source = Load(Path.Combine(_outDir, "alpha.png"));
            After.Source = Load(Path.Combine(_outDir, "beyaz.png"));
        }
        catch (Exception ex)
        {
            Status.Text = "BAŞARISIZ";
            Report.Text = ex.ToString();
        }
    }

    private static BitmapImage? Load(string path)
    {
        if (!File.Exists(path)) return null;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.StreamSource = new MemoryStream(File.ReadAllBytes(path));
        image.EndInit();
        image.Freeze();
        return image;
    }
}
