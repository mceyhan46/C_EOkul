using EOkulStudio.Core.Results;

namespace EOkulStudio.Infrastructure.Tests;

public sealed class PhotoScannerTests : IDisposable
{
    private readonly TempFolder _temp = new();
    private readonly PhotoScanner _scanner = new();

    public void Dispose() => _temp.Dispose();

    private (string A, string B, string D) Populate()
    {
        var a = _temp.File("a.jpg");
        var b = _temp.File("b.PNG");
        _temp.File("notlar.txt");
        _temp.File("c.heic");
        _temp.File(".gizli.jpg");
        var d = _temp.File("9A Sınıfı/d.jpg");
        _temp.File("E_Okul/e.jpg");
        _temp.File("9A Sınıfı/e_okul/f.jpg");
        return (a, b, d);
    }

    [Fact]
    public void Finds_photos_in_subfolders_and_skips_output_folders()
    {
        var (a, b, d) = Populate();

        var result = _scanner.Scan([_temp.Path], includeSubfolders: true, TestContext.Current.CancellationToken);

        result.Photos.Should().BeEquivalentTo(new[] { a, b, d });
        result.Rejected.Should().ContainSingle()
            .Which.Should().Be(new RejectedFile(_temp.Combine("c.heic"), IssueCode.UnsupportedFormat));
        result.UnreadableFolders.Should().BeEmpty();
    }

    [Fact]
    public void Subfolders_can_be_left_out()
    {
        var (a, b, _) = Populate();

        var result = _scanner.Scan([_temp.Path], includeSubfolders: false, TestContext.Current.CancellationToken);

        result.Photos.Should().Equal(a, b);
    }

    [Fact]
    public void Scanning_changes_nothing()
    {
        Populate();
        var before = _temp.Snapshot();

        _scanner.Scan([_temp.Path], includeSubfolders: true, TestContext.Current.CancellationToken);

        _temp.Snapshot().Should().Equal(before);
    }

    [Fact]
    public void Output_folders_and_files_are_refused_with_a_reason()
    {
        Populate();
        var folder = _temp.Combine("E_Okul");
        var file = _temp.Combine("9A Sınıfı", "e_okul", "f.jpg");

        var result = _scanner.Scan([folder, file], includeSubfolders: true, TestContext.Current.CancellationToken);

        result.Photos.Should().BeEmpty();
        result.Rejected.Should().HaveCount(2)
            .And.OnlyContain(r => r.Reason == IssueCode.InsideOutputFolder);
    }

    [Fact]
    public void Selected_files_are_checked_one_by_one()
    {
        var (a, _, _) = Populate();
        var text = _temp.Combine("notlar.txt");
        var missing = _temp.Combine("yok.jpg");

        var result = _scanner.Scan([a, text, missing], includeSubfolders: true, TestContext.Current.CancellationToken);

        result.Photos.Should().Equal(a);
        result.Rejected.Should().BeEquivalentTo(new[]
        {
            new RejectedFile(text, IssueCode.UnsupportedFormat),
            new RejectedFile(missing, IssueCode.FileNotFound),
        });
    }

    [Fact]
    public void Same_photo_selected_twice_is_listed_once()
    {
        var (a, _, _) = Populate();

        var result = _scanner.Scan(
            [_temp.Path + Path.DirectorySeparatorChar, a, _temp.Path],
            includeSubfolders: false,
            TestContext.Current.CancellationToken);

        result.Photos.Count(p => p == a).Should().Be(1);
        result.Photos.Should().HaveCount(2);
    }

    [Fact]
    public void Long_paths_work()
    {
        var segment = new string('k', 60);
        var photo = _temp.File($"{segment}/{segment}/{segment}/{segment}/{segment}/ö.jpg");
        photo.Length.Should().BeGreaterThan(260);

        var result = _scanner.Scan([_temp.Path], includeSubfolders: true, TestContext.Current.CancellationToken);

        result.Photos.Should().Equal(photo);
    }

    [Fact]
    public void Scan_can_be_cancelled()
    {
        Populate();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = () => _scanner.Scan([_temp.Path], includeSubfolders: true, source.Token);

        act.Should().Throw<OperationCanceledException>();
    }
}
