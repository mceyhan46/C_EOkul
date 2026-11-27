using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.XImgProc;

namespace EOkulSpike;

public sealed record SelfTestResult(bool Success, string Report);

/// <summary>Aşama 0 denemeleri. Her deneme ayrı yakalanır; biri düşse de diğerleri çalışır.</summary>
public static class SelfTest
{
    private const string Modnet = "modnet_photographic.onnx";
    private const string YuNet = "face_detection_yunet_2023mar.onnx";

    public static SelfTestResult Run(string baseDir, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var report = new StringBuilder();
        var success = true;
        var reportPath = Path.Combine(outDir, "rapor.txt");

        void Log(string line)
        {
            report.AppendLine(line);
            // Yerel bir DLL süreci düşürürse nereye kadar geldiği görülsün.
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
        }

        void Check(string name, Action action, bool required = true)
        {
            try
            {
                action();
                Log($"[GEÇTİ] {name}");
            }
            catch (Exception ex)
            {
                if (required) success = false;
                Log($"[{(required ? "HATA" : "BULGU")}] {name}: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException is { } inner) Log($"        iç hata: {inner.GetType().Name}: {inner.Message}");
            }
        }

        Log($"İşletim sistemi : {RuntimeInformation.OSDescription}");
        Log($"Mimari          : {RuntimeInformation.ProcessArchitecture}");
        Log($".NET            : {RuntimeInformation.FrameworkDescription}");
        Log($"İşlemci         : {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")} ({Environment.ProcessorCount} mantıksal)");
        Log($"Program klasörü : {baseDir}");
        Log($"ANSI kod sayfası: {GetACP()}");
        Log("");

        var modelDir = Path.Combine(baseDir, "models");
        var samplePath = Path.Combine(baseDir, "samples", "astronaut.png");
        if (!File.Exists(samplePath)) samplePath = Path.Combine(baseDir, "astronaut.png");

        Check("Model dosyaları SHA-256", () => VerifyModels(modelDir, Log));

        byte[]? alphaFirstRun = null;
        Mat? image = null;
        Mat? alpha = null;
        try
        {
            Check("OpenCvSharp yerel kütüphanesi yükleniyor", () =>
            {
                Log($"        OpenCV {Cv2.GetVersionString()}");
                image = Cv2.ImDecode(File.ReadAllBytes(samplePath), ImreadModes.Color);
                Require(!image.Empty(), "örnek görsel okunamadı");
            });

            Check("ONNX Runtime + MODNet çıkarımı", () =>
            {
                Require(image is not null, "görsel yok");
                Log($"        ONNX Runtime {OrtEnv.Instance().GetVersionString()}");
                using var options = new SessionOptions();
                var load = Stopwatch.StartNew();
                // Bayt dizisinden yükleme: Türkçe karakterli kurulum yolundan etkilenmez.
                using var session = new InferenceSession(File.ReadAllBytes(Path.Combine(modelDir, Modnet)), options);
                Log($"        model yükleme: {load.ElapsedMilliseconds} ms");

                var times = new List<double>();
                for (var i = 0; i < 6; i++)
                {
                    var watch = Stopwatch.StartNew();
                    var matte = RunModnet(session, image!);
                    watch.Stop();
                    if (i == 0)
                    {
                        alpha = matte;
                        alphaFirstRun = ToBytes(matte);
                    }
                    else
                    {
                        times.Add(watch.Elapsed.TotalMilliseconds);
                        Require(ToBytes(matte).AsSpan().SequenceEqual(alphaFirstRun), "aynı girdi farklı çıktı verdi");
                        matte.Dispose();
                    }
                }
                times.Sort();
                Log($"        512x512 çıkarım (ön/son işlem dahil) ortanca: {times[times.Count / 2]:F0} ms; 6 çalıştırmada çıktı birebir aynı");

                Cv2.MinMaxLoc(alpha!, out double min, out double max);
                var mean = Cv2.Mean(alpha!).Val0;
                Log($"        alfa min={min:F3} maks={max:F3} ortalama={mean:F3}");
                Require(min < 0.05 && max > 0.95 && mean is > 0.2 and < 0.8, "alfa beklenen aralıkta değil");
            });

            // Zorunlu değil: sistemde Redistributable varsa başka bir bileşen sistem kopyasını önce yüklemiş olabilir.
            Check("Visual C++ dosyaları program klasöründen", () => CheckVcRuntime(baseDir, Log), required: false);

            Check("ONNX Runtime + YuNet çıkarımı", () =>
            {
                Require(image is not null, "görsel yok");
                using var session = new InferenceSession(File.ReadAllBytes(Path.Combine(modelDir, YuNet)));
                using var resized = new Mat();
                Cv2.Resize(image!, resized, new Size(640, 640));
                var input = ToTensor(resized, rgb: false, mean: 0f, scale: 1f);
                using var outputs = session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), input)]);
                var best = 0f;
                foreach (var stride in new[] { 8, 16, 32 })
                {
                    var cls = outputs.First(o => o.Name == $"cls_{stride}").AsEnumerable<float>().ToArray();
                    var obj = outputs.First(o => o.Name == $"obj_{stride}").AsEnumerable<float>().ToArray();
                    for (var i = 0; i < cls.Length; i++) best = Math.Max(best, MathF.Sqrt(cls[i] * obj[i]));
                }
                Log($"        en yüksek yüz skoru: {best:F3}");
                Require(best > 0.8f, "yüz skoru düşük");
            });

            Check("OpenCV FaceDetectorYN + YuNet", () =>
            {
                Require(image is not null, "görsel yok");
                DetectFace(Path.Combine(modelDir, YuNet), image!, Log);
            });

            Check("FaceDetectorYN: Türkçe karakterli model yolu", () =>
            {
                Require(image is not null, "görsel yok");
                var dir = Path.Combine(outDir, "Türkçe yol çğıöşü İŞĞ");
                Directory.CreateDirectory(dir);
                var copy = Path.Combine(dir, YuNet);
                File.Copy(Path.Combine(modelDir, YuNet), copy, overwrite: true);
                DetectFace(copy, image!, Log);
            });

            Check("XImgProc.GuidedFilter", () =>
            {
                Require(image is not null && alpha is not null, "görsel veya alfa yok");
                using var guide = new Mat();
                image!.ConvertTo(guide, MatType.CV_32FC3, 1.0 / 255);
                using var refined = new Mat();
                CvXImgProc.GuidedFilter(guide, alpha!, refined, 8, 1e-4);
                Require(!refined.Empty() && refined.Size() == alpha!.Size(), "çıktı boyutu yanlış");
                using var difference = new Mat();
                Cv2.Absdiff(refined, alpha!, difference);
                var change = Cv2.Mean(difference).Val0;
                Log($"        ortalama değişim: {change:F5}");
                Require(change is > 0 and < 0.1, "filtre sonucu beklenmedik");

                using var alpha8 = new Mat();
                refined.ConvertTo(alpha8, MatType.CV_8UC1, 255);
                WritePng(Path.Combine(outDir, "alpha.png"), alpha8);
                // Yalnız görsel kontrol: ön plan rengi arındırma Aşama 2'de.
                using var white = CompositeOnWhite(image, alpha8);
                WritePng(Path.Combine(outDir, "beyaz.png"), white);
            });

            Check("Türkçe karakterli yola JPEG yazma/okuma", () =>
            {
                Require(image is not null, "görsel yok");
                var dir = Path.Combine(outDir, "Öğrenci Fotoğrafları ışık", "E_Okul");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "12345 Çağrı Şüheda İnöğ.jpg");
                using var small = new Mat();
                Cv2.Resize(image!, small, new Size(133, 171), 0, 0, InterpolationFlags.Area);
                Cv2.ImEncode(".jpg", small, out var bytes, new ImageEncodingParam(ImwriteFlags.JpegQuality, 92));
                File.WriteAllBytes(path, bytes);
                using var back = Cv2.ImDecode(File.ReadAllBytes(path), ImreadModes.Color);
                Require(back.Width == 133 && back.Height == 171, "geri okunan boyut yanlış");
            });
        }
        finally
        {
            image?.Dispose();
            alpha?.Dispose();
        }

        Log("");
        Log(success ? "SONUÇ: BAŞARILI" : "SONUÇ: BAŞARISIZ");
        return new SelfTestResult(success, report.ToString());
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void CheckVcRuntime(string baseDir, Action<string> log)
    {
        var expected = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDir));
        using var process = Process.GetCurrentProcess();
        var loaded = process.Modules.Cast<ProcessModule>()
            .Where(m => VcRuntime.Files.Contains(m.ModuleName, StringComparer.OrdinalIgnoreCase))
            .ToList();
        foreach (var module in loaded) log($"        {module.ModuleName} <- {module.FileName}");
        foreach (var name in VcRuntime.Files)
        {
            var matches = loaded.Where(m => m.ModuleName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
            Require(matches.Count > 0, $"{name} yüklü değil");
            Require(matches.All(m => string.Equals(Path.GetDirectoryName(m.FileName), expected, StringComparison.OrdinalIgnoreCase)),
                $"{name} program klasörü dışından yüklendi");
        }
    }

    private static void VerifyModels(string modelDir, Action<string> log)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(modelDir, "models.json")));
        foreach (var item in manifest.RootElement.EnumerateArray())
        {
            var name = item.GetProperty("file").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(modelDir, name));
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Require(hash == item.GetProperty("sha256").GetString(), $"SHA-256 uyuşmuyor: {name}");
            log($"        {name}: {bytes.Length:N0} bayt");
        }
    }

    /// <summary>Spike: kare 512x512 giriş. Gerçek boru hattında en-boy oranı korunacak.</summary>
    private static Mat RunModnet(InferenceSession session, Mat bgr)
    {
        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new Size(512, 512), 0, 0, InterpolationFlags.Area);
        var input = ToTensor(resized, rgb: true, mean: 127.5f, scale: 1f / 127.5f);
        using var outputs = session.Run([NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), input)]);
        var values = outputs.First().AsEnumerable<float>().ToArray();
        using var small = new Mat(512, 512, MatType.CV_32FC1);
        small.SetArray(values);
        var full = new Mat();
        Cv2.Resize(small, full, bgr.Size(), 0, 0, InterpolationFlags.Cubic);
        Cv2.Max(full, 0.0, full);
        Cv2.Min(full, 1.0, full);
        return full;
    }

    private static DenseTensor<float> ToTensor(Mat bgr, bool rgb, float mean, float scale)
    {
        var tensor = new DenseTensor<float>([1, 3, bgr.Height, bgr.Width]);
        bgr.GetArray(out Vec3b[] pixels);
        int height = bgr.Height, width = bgr.Width;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var p = pixels[y * width + x];
            tensor[0, 0, y, x] = ((rgb ? p.Item2 : p.Item0) - mean) * scale;
            tensor[0, 1, y, x] = (p.Item1 - mean) * scale;
            tensor[0, 2, y, x] = ((rgb ? p.Item0 : p.Item2) - mean) * scale;
        }
        return tensor;
    }

    private static void DetectFace(string modelPath, Mat image, Action<string> log)
    {
        using var detector = FaceDetectorYN.Create(modelPath, "", image.Size());
        using var faces = new Mat();
        detector.Detect(image, faces);
        Require(faces.Rows == 1, $"1 yüz beklenirken {faces.Rows} bulundu");
        float F(int column) => faces.At<float>(0, column);
        var roll = Math.Atan2(F(7) - F(5), F(6) - F(4)) * 180 / Math.PI;
        log($"        yüz kutusu x={F(0):F0} y={F(1):F0} g={F(2):F0} y={F(3):F0}, skor={F(14):F3}, baş eğimi={roll:F1}°");
    }

    private static Mat CompositeOnWhite(Mat bgr, Mat alpha8)
    {
        using var a = new Mat();
        Cv2.CvtColor(alpha8, a, ColorConversionCodes.GRAY2BGR);
        using var inverse = new Mat();
        Cv2.BitwiseNot(a, inverse);
        using var foreground = new Mat();
        Cv2.Multiply(bgr, a, foreground, 1.0 / 255);
        var result = new Mat();
        Cv2.Add(foreground, inverse, result);
        return result;
    }

    private static byte[] ToBytes(Mat floatMat)
    {
        floatMat.GetArray(out float[] values);
        return MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
    }

    private static void WritePng(string path, Mat mat)
    {
        Cv2.ImEncode(".png", mat, out var bytes);
        File.WriteAllBytes(path, bytes);
    }
}
