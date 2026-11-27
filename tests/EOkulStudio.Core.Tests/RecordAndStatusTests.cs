using EOkulStudio.Core.Files;
using EOkulStudio.Core.Records;
using EOkulStudio.Core.Results;

namespace EOkulStudio.Core.Tests;

public class OutputDecisionTests
{
    private static RecordEntry Entry(PhotoStatus status, string hash = "abc") =>
        new() { SourceName = "a.jpg", SourceSha256 = hash, OutputName = "a.jpg", Status = status };

    [Theory]
    [InlineData(ExistingOutputPolicy.Skip)]
    [InlineData(ExistingOutputPolicy.Overwrite)]
    [InlineData(ExistingOutputPolicy.AddSuffix)]
    public void Missing_output_is_always_written(ExistingOutputPolicy policy) =>
        OutputDecision.Decide(false, Entry(PhotoStatus.Success), "abc", policy, resuming: true)
            .Should().Be(OutputAction.Write);

    [Theory]
    [InlineData(ExistingOutputPolicy.Skip, OutputAction.SkipExisting)]
    [InlineData(ExistingOutputPolicy.Overwrite, OutputAction.Write)]
    [InlineData(ExistingOutputPolicy.AddSuffix, OutputAction.WriteWithSuffix)]
    public void Unknown_existing_file_follows_the_setting(ExistingOutputPolicy policy, OutputAction expected) =>
        OutputDecision.Decide(true, null, "abc", policy, resuming: false).Should().Be(expected);

    [Theory]
    [InlineData(ExistingOutputPolicy.Skip, OutputAction.SkipExisting)]
    [InlineData(ExistingOutputPolicy.Overwrite, OutputAction.Write)]
    [InlineData(ExistingOutputPolicy.AddSuffix, OutputAction.WriteWithSuffix)]
    public void Unchanged_photo_follows_the_setting(ExistingOutputPolicy policy, OutputAction expected) =>
        OutputDecision.Decide(true, Entry(PhotoStatus.NeedsReview), "ABC", policy, resuming: false)
            .Should().Be(expected);

    [Theory]
    [InlineData(ExistingOutputPolicy.Skip)]
    [InlineData(ExistingOutputPolicy.Overwrite)]
    public void Finished_photo_is_skipped_when_resuming(ExistingOutputPolicy policy) =>
        OutputDecision.Decide(true, Entry(PhotoStatus.Success), "abc", policy, resuming: true)
            .Should().Be(OutputAction.SkipAlreadyProcessed);

    [Fact]
    public void Changed_photo_replaces_its_own_output() =>
        OutputDecision.Decide(true, Entry(PhotoStatus.Success), "different", ExistingOutputPolicy.Skip, resuming: false)
            .Should().Be(OutputAction.Write);

    [Theory]
    [InlineData("yeni", false, OutputAction.Write)] // fotoğraf yine farklı: yeniden dene
    [InlineData("yeni", true, OutputAction.Write)]
    [InlineData("abc", true, OutputAction.SkipExisting)] // eski fotoğraf geri konmuş: eski çıktı geçerli
    public void Failed_rerun_does_not_lose_the_earlier_output(string hash, bool resuming, OutputAction expected)
    {
        var record = new ProcessingRecord()
            .With(Entry(PhotoStatus.Success))
            .With(Entry(PhotoStatus.Failed, "yeni"))
            .With(Entry(PhotoStatus.Failed, "yeni"));

        OutputDecision.Decide(true, record.Find("a.jpg"), hash, ExistingOutputPolicy.Skip, resuming)
            .Should().Be(expected);
    }

    [Fact]
    public void Earlier_output_under_another_name_is_not_carried_over()
    {
        var record = new ProcessingRecord()
            .With(Entry(PhotoStatus.Success))
            .With(Entry(PhotoStatus.Failed, "yeni") with { OutputName = "a_2.jpg" });

        record.Find("a.jpg")!.OutputSha256.Should().BeNull();
    }

    [Fact]
    public void Failed_entry_does_not_own_an_existing_file() =>
        OutputDecision.Decide(true, Entry(PhotoStatus.Failed), "different", ExistingOutputPolicy.Skip, resuming: true)
            .Should().Be(OutputAction.SkipExisting);
}

public class IssueTests
{
    private static readonly IssueCode[] Blocking =
    [
        IssueCode.FileNotFound, IssueCode.CorruptFile, IssueCode.UnsupportedFormat, IssueCode.InsideOutputFolder,
        IssueCode.NoFace, IssueCode.MultipleFaces, IssueCode.OutputNotWritable, IssueCode.ProcessingError,
    ];

    [Fact]
    public void Every_issue_has_a_message_and_an_action()
    {
        foreach (var code in Enum.GetValues<IssueCode>())
        {
            var info = IssueCatalog.Get(code);
            info.Message.Should().NotBeNullOrWhiteSpace();
            info.Action.Should().NotBeNullOrWhiteSpace();
            info.BlocksOutput.Should().Be(Blocking.Contains(code), $"{code}");
        }
    }

    [Fact]
    public void Unknown_issue_code_is_an_error()
    {
        var act = () => IssueCatalog.Get((IssueCode)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Status_follows_the_most_serious_issue()
    {
        StatusRules.Evaluate([]).Should().Be(PhotoStatus.Success);
        StatusRules.Evaluate([IssueCode.Blurry]).Should().Be(PhotoStatus.NeedsReview);
        StatusRules.Evaluate([IssueCode.Blurry, IssueCode.NoFace]).Should().Be(PhotoStatus.Failed);
    }
}

public class ProcessingRecordTests
{
    private static RecordEntry Entry(string name, PhotoStatus status = PhotoStatus.Success) =>
        new() { SourceName = name, SourceSha256 = "abc", OutputName = name, Status = status };

    [Fact]
    public void Adding_an_entry_replaces_the_same_source_and_keeps_order()
    {
        var record = new ProcessingRecord()
            .With(Entry("b.jpg"))
            .With(Entry("a.jpg"))
            .With(Entry("B.JPG", PhotoStatus.Failed));

        record.Entries.Select(e => e.SourceName).Should().Equal("a.jpg", "B.JPG");
        record.Find("b.jpg")!.Status.Should().Be(PhotoStatus.Failed);
        record.Find("yok.jpg").Should().BeNull();
        record.OutputNames()["A.jpg"].Should().Be("a.jpg");
    }

    [Theory]
    [InlineData(PhotoStatus.Success, true)]
    [InlineData(PhotoStatus.NeedsReview, true)]
    [InlineData(PhotoStatus.Failed, false)]
    public void Only_successful_or_review_entries_have_output(PhotoStatus status, bool expected) =>
        Entry("a.jpg", status).HasOutput.Should().Be(expected);
}
