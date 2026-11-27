using System.IO;
using System.Runtime.InteropServices;

namespace EOkulSpike;

/// <summary>
/// Visual C++ DLL'lerini program klasöründen önceden yükler. Tek dosya yayımında yerel kütüphaneler
/// geçici klasöre açıldığı için Windows bağımlılıkları program klasöründe aramayabilir;
/// önceden yüklenen modül yeniden kullanılır.
/// </summary>
public static class VcRuntime
{
    // Sıra önemli: msvcp140*, vcruntime140*'e bağımlıdır.
    public static readonly string[] Files =
        ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll"];

    public static void Preload(string baseDir)
    {
        foreach (var name in Files)
        {
            var path = Path.Combine(baseDir, name);
            // Yüklenemezse sistem kopyası denenir; sorun öz denemede raporlanır.
            if (File.Exists(path)) NativeLibrary.TryLoad(path, out _);
        }
    }
}
