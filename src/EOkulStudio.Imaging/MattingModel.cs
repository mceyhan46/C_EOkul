using EOkulStudio.Core.Imaging;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace EOkulStudio.Imaging;

/// <summary>Arka plan ayırma modeli. Başka bir model bu arayüzle takılabilir.</summary>
public interface IMattingModel : IDisposable
{
    string Id { get; }

    /// <returns>
    /// 0..1 arası kişi maskesi (CV_32FC1), modelin kendi çözünürlüğünde. Aynı anda birçok iş parçacığından çağrılabilir.
    /// </returns>
    Mat Predict(Mat bgr, CancellationToken cancellationToken = default);
}

/// <summary>MODNet (Apache-2.0), ONNX Runtime ile işlemcide.</summary>
public sealed class ModnetModel : IMattingModel
{
    private readonly InferenceSession _session;

    /// <param name="threads">Bir model çalıştırmasının kullanacağı iş parçacığı sayısı.</param>
    public ModnetModel(LoadedModel model, int threads)
    {
        ArgumentNullException.ThrowIfNull(model);
        using var options = new SessionOptions
        {
            IntraOpNumThreads = Math.Max(1, threads),
            InterOpNumThreads = 1,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
        _session = new InferenceSession(model.Content, options);
        Id = model.Id;
    }

    public string Id { get; }

    public Mat Predict(Mat bgr, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bgr);
        var (width, height) = ModelInput.MattingSize(bgr.Width, bgr.Height);

        using var resized = new Mat();
        Cv2.Resize(bgr, resized, new Size(width, height), 0, 0,
            bgr.Width > width ? InterpolationFlags.Area : InterpolationFlags.Cubic);
        resized.GetArray(out Vec3b[] pixels);

        // NCHW, RGB, (x - 127.5) / 127.5
        var plane = width * height;
        var input = new float[3 * plane];
        for (var i = 0; i < plane; i++)
        {
            input[i] = (pixels[i].Item2 - 127.5f) / 127.5f;
            input[plane + i] = (pixels[i].Item1 - 127.5f) / 127.5f;
            input[2 * plane + i] = (pixels[i].Item0 - 127.5f) / 127.5f;
        }

        using var tensor = OrtValue.CreateTensorValueFromMemory(input, [1L, 3, height, width]);
        using var run = new RunOptions();
        using var registration = cancellationToken.Register(() => run.Terminate = true);
        try
        {
            var inputs = new Dictionary<string, OrtValue> { [_session.InputNames[0]] = tensor };
            using var outputs = _session.Run(run, inputs, _session.OutputNames);
            var values = outputs[0].GetTensorDataAsSpan<float>();
            if (values.Length != plane)
                throw new InvalidDataException("Modelin çıktı boyutu beklenenden farklı.");

            var mask = new Mat(height, width, MatType.CV_32FC1);
            mask.SetArray(values.ToArray());
            return mask;
        }
        catch (OnnxRuntimeException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    public void Dispose() => _session.Dispose();
}
