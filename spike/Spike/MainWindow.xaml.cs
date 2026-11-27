using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace EOkulSpike;

public partial class MainWindow : Window
{
    private readonly string _outDir;

    /// <param name="finished">Verilirse deneme bitince çağrılır (otomatik pencere testi).</param>
    public MainWindow(string outDir, Action<bool>? finished = null)
    {
        _outDir = outDir;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var ok = await RunAsync();
            finished?.Invoke(ok);
        };
    }

    private async Task<bool> RunAsync()
    {
        try
        {
            var result = await Task.Run(() => SelfTest.Run(AppContext.BaseDirectory, _outDir));
            Status.Text = result.Success ? "BAŞARILI: tüm zorunlu denemeler geçti." : "BAŞARISIZ: aşağıdaki HATA satırlarına bakın.";
            Report.Text = result.Report + Environment.NewLine + "Çıktı klasörü: " + _outDir;
            return ShowImages() && result.Success;
        }
        catch (Exception ex)
        {
            Status.Text = "BAŞARISIZ";
            Report.AppendText(Environment.NewLine + ex);
            File.WriteAllText(Path.Combine(_outDir, "pencere-hatasi.txt"), ex.ToString());
            return false;
        }
    }

    /// <summary>Görsel gösterme hatası deneme raporunu silmez; rapora eklenir.</summary>
    private bool ShowImages()
    {
        try
        {
            Before.Source = Load(Path.Combine(AppContext.BaseDirectory, "samples", "astronaut.png"));
            Alpha.Source = Load(Path.Combine(_outDir, "alpha.png"));
            After.Source = Load(Path.Combine(_outDir, "beyaz.png"));
            return Before.Source is not null && Alpha.Source is not null && After.Source is not null;
        }
        catch (Exception ex)
        {
            var message = "Görseller gösterilemedi: " + ex;
            Report.AppendText(Environment.NewLine + message);
            File.WriteAllText(Path.Combine(_outDir, "pencere-hatasi.txt"), message);
            return false;
        }
    }

    private static BitmapImage? Load(string path)
    {
        if (!File.Exists(path)) return null;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        // IgnoreImageCache KULLANMA: akıştan yüklemede (Uri yok) WPF ArgumentNullException atar.
        image.StreamSource = new MemoryStream(File.ReadAllBytes(path));
        image.EndInit();
        image.Freeze();
        return image;
    }
}
