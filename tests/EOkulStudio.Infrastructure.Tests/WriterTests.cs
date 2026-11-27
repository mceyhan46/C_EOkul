namespace EOkulStudio.Infrastructure.Tests;

public sealed class SafeFileWriterTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private string[] Files() => Directory.GetFiles(_temp.Path).Select(p => Path.GetFileName(p)).Order().ToArray()!;

    [Fact]
    public void Writes_the_file_and_leaves_no_temporary_file()
    {
        var target = _temp.Combine("a.jpg");

        SafeFileWriter.Write(target, [1, 2, 3], overwrite: false);

        File.ReadAllBytes(target).Should().Equal(1, 2, 3);
        Files().Should().Equal("a.jpg");
    }

    [Fact]
    public void Existing_file_is_kept_unless_overwriting()
    {
        var target = _temp.File("a.jpg", "eski");

        var act = () => SafeFileWriter.Write(target, [1], overwrite: false);

        act.Should().Throw<IOException>();
        File.ReadAllText(target).Should().Be("eski");
        Files().Should().Equal("a.jpg");
    }

    [Fact]
    public void Existing_file_is_replaced_when_overwriting()
    {
        var target = _temp.File("a.jpg", "eski");

        SafeFileWriter.Write(target, [1], overwrite: true);

        File.ReadAllBytes(target).Should().Equal(1);
        Files().Should().Equal("a.jpg");
    }

    [Fact]
    public void Failed_verification_does_not_touch_the_target()
    {
        var target = _temp.File("a.jpg", "eski");

        var act = () => SafeFileWriter.Write(target, [1], overwrite: true, verify: _ => false);

        act.Should().Throw<InvalidDataException>();
        File.ReadAllText(target).Should().Be("eski");
        Files().Should().Equal("a.jpg");
    }

    [Fact]
    public void Error_during_verification_leaves_nothing_behind()
    {
        var act = () => SafeFileWriter.Write(
            _temp.Combine("a.jpg"), [1], overwrite: true, verify: _ => throw new FormatException());

        act.Should().Throw<FormatException>();
        Files().Should().BeEmpty();
    }
}

public sealed class OutputWriterTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly string _source;

    public OutputWriterTests() => _source = _temp.File("9B/Şule.jpg", "asıl");

    public void Dispose() => _temp.Dispose();

    private string OutputFolder => _temp.Combine("9B", "E_Okul");

    [Fact]
    public void Writes_into_E_Okul_and_leaves_the_source_alone()
    {
        var time = File.GetLastWriteTimeUtc(_source);

        var written = OutputWriter.WritePhoto(_source, "Şule.jpg", [1, 2], overwrite: false);

        written.Should().Be(Path.Combine(OutputFolder, "Şule.jpg"));
        File.ReadAllBytes(written).Should().Equal(1, 2);
        File.ReadAllText(_source).Should().Be("asıl");
        File.GetLastWriteTimeUtc(_source).Should().Be(time);
        Directory.GetFiles(OutputFolder).Should().ContainSingle();
    }

    [Fact]
    public void Failed_write_does_not_leave_an_empty_E_Okul()
    {
        var act = () => OutputWriter.WritePhoto(_source, "Şule.jpg", [1], overwrite: false, verify: _ => false);

        act.Should().Throw<InvalidDataException>();
        Directory.Exists(OutputFolder).Should().BeFalse();
    }

    [Fact]
    public void Failed_write_keeps_an_existing_E_Okul()
    {
        var other = _temp.File("9B/E_Okul/diger.jpg");

        var act = () => OutputWriter.WritePhoto(_source, "Şule.jpg", [1], overwrite: false, verify: _ => false);

        act.Should().Throw<InvalidDataException>();
        File.Exists(other).Should().BeTrue();
    }

    [Fact]
    public void Existing_output_is_not_reported_as_unwritable_folder()
    {
        var existing = _temp.File("9B/E_Okul/Şule.jpg", "eski");

        var act = () => OutputWriter.WritePhoto(_source, "Şule.jpg", [1], overwrite: false);

        act.Should().Throw<IOException>().Which.Should().NotBeOfType<OutputNotWritableException>();
        File.ReadAllText(existing).Should().Be("eski");
    }

    [Fact]
    public void Blocked_output_folder_is_reported()
    {
        _temp.File("9B/E_Okul", "bu bir dosya");

        var act = () => OutputWriter.WritePhoto(_source, "Şule.jpg", [1], overwrite: false);

        act.Should().Throw<OutputNotWritableException>().Which.Directory.Should().Be(OutputFolder);
    }

    [Theory]
    [InlineData("../a.jpg")]
    [InlineData("alt/a.jpg")]
    [InlineData("a.png")]
    [InlineData(".a.jpg")]
    public void Invalid_output_names_are_rejected(string name)
    {
        var act = () => OutputWriter.WritePhoto(_source, name, [1], overwrite: true);

        act.Should().Throw<ArgumentException>();
        Directory.Exists(OutputFolder).Should().BeFalse();
    }

    [Fact]
    public void Output_cannot_be_used_as_a_source()
    {
        var output = _temp.File("9B/E_Okul/a.jpg");

        var act = () => OutputWriter.WritePhoto(output, "a.jpg", [1], overwrite: true);

        act.Should().Throw<ArgumentException>();
        Directory.Exists(Path.Combine(OutputFolder, "E_Okul")).Should().BeFalse();
    }

    [Fact]
    public void Long_paths_work()
    {
        var segment = new string('k', 60);
        var source = _temp.File($"{segment}/{segment}/{segment}/{segment}/{segment}/ö.jpg");

        var written = OutputWriter.WritePhoto(source, "ö.jpg", [7], overwrite: false);

        written.Length.Should().BeGreaterThan(260);
        File.ReadAllBytes(written).Should().Equal(7);
    }
}
