using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <summary>Yerel kütüphanelerin hazırlanması ve yüklendiklerinin denetimi.</summary>
public static class NativeRuntime
{
    // Sıra önemli: msvcp140*, vcruntime140*'e bağımlıdır.
    private static readonly string[] VcFiles =
        ["vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll"];

    /// <summary>
    /// Visual C++ DLL'lerini program klasöründen önceden yükler. Herhangi bir yerel kütüphaneden önce çağrılmalı:
    /// tek dosya yayımında yerel DLL'ler geçici klasöre açıldığı için bağımlılıklar program klasöründe aranmayabilir.
    /// </summary>
    public static void PreloadVcRuntime(string baseDirectory)
    {
        foreach (var name in VcFiles)
        {
            var path = Path.Combine(baseDirectory, name);
            if (File.Exists(path)) NativeLibrary.TryLoad(path, out _);
        }
    }

    /// <summary>OpenCV ve ONNX Runtime'ı yükler; yüklenemezse istisna atar.</summary>
    public static string Describe() =>
        $"OpenCV {Cv2.GetVersionString()}, ONNX Runtime {OrtEnv.Instance().GetVersionString()}";
}
