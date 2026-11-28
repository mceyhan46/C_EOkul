using System.Security.Cryptography;
using EOkulStudio.Core.Files;
using EOkulStudio.Core.Imaging;
using EOkulStudio.Core.Results;
using EOkulStudio.Infrastructure;

namespace EOkulStudio.Imaging.Tests;

public sealed class ModelStoreTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("eokul-model-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static string Shipped => Path.Combine(AppContext.BaseDirectory, "models");

    [Fact]
    public void Shipped_models_pass_verification()
    {
        var store = new ModelStore(Shipped);

        store.Load(ModelStore.MattingFile).Id.Should().Be("modnet_photographic:5069a5e3");
        store.Load(ModelStore.FaceFile).Id.Should().Be("face_detection_yunet_2023mar:8f2383e4");
    }

    [Fact]
    public void Changed_missing_or_unlisted_models_are_rejected()
    {
        var store = new ModelStore(_folder);
        var missing = () => store.Load(ModelStore.FaceFile);
        missing.Should().Throw<FileNotFoundException>();

        File.Copy(Path.Combine(Shipped, ModelStore.ListFile), Path.Combine(_folder, ModelStore.ListFile));
        File.WriteAllBytes(Path.Combine(_folder, ModelStore.FaceFile), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_folder, "baska.onnx"), [1, 2, 3]);
        var changed = () => store.Load(ModelStore.FaceFile);
        var unlisted = () => store.Load("baska.onnx");

        changed.Should().Throw<InvalidDataException>();
        unlisted.Should().Throw<InvalidDataException>();
    }
}

/// <summary>Gerçek boru hattıyla klasör işleme: kaynaklar değişmez, Türkçe yollar çalışır.</summary>
public sealed class FolderProcessingTests(PipelineFixture fixture) : IClassFixture<PipelineFixture>, IDisposable
{
    private readonly string _base = Directory.CreateTempSubdirectory("eokul-toplu-").FullName;

    public void Dispose() => Directory.Delete(_base, recursive: true);

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [Fact]
    public async Task A_turkish_folder_is_processed_without_changing_the_sources()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_base, "Okul İşleri çğşöü", "9-A Şubesi")).FullName;
        var good = Path.Combine(folder, "Ayşe Güneş.png");
        var broken = Path.Combine(folder, "bozuk.jpg");
        File.Copy(TestImages.SamplePath, good);
        File.WriteAllText(broken, "fotoğraf değil");
        var before = new[] { Hash(good), Hash(broken) };
        var scan = new PhotoScanner().Scan([_base], includeSubfolders: true, TestContext.Current.CancellationToken);

        var results = await new BatchRunner(fixture.Pipeline).RunAsync(
            scan.Photos,
            new BatchOptions(PipelineFixture.Options, ExistingOutputPolicy.Skip, Parallelism: 2),
            cancellationToken: TestContext.Current.CancellationToken);

        new[] { Hash(good), Hash(broken) }.Should().Equal(before);
        results.Single(r => r.SourcePath == broken).Issues.Should().Equal(IssueCode.CorruptFile);
        var output = Path.Combine(folder, "E_Okul", "Ayşe Güneş.jpg");
        results.Single(r => r.SourcePath == good).OutputPath.Should().Be(output);
        JpegFile.ReadSize(File.ReadAllBytes(output)).Should().Be((133, 171));
        var record = new ProcessingRecordStore(Path.Combine(folder, "E_Okul")).Load().Record;
        record.Find("Ayşe Güneş.png")!.SourceSha256.Should().Be(before[0].ToLowerInvariant());
        record.Find("Ayşe Güneş.png")!.ModelId.Should().Be(fixture.Pipeline.ModelId);
        record.Find("bozuk.jpg")!.Status.Should().Be(PhotoStatus.Failed);
    }
}
