namespace EOkulStudio.Core.Results;

public enum PhotoStatus
{
    Success,
    NeedsReview,
    Failed,
}

public static class StatusRules
{
    public static PhotoStatus Evaluate(IEnumerable<IssueCode> issues)
    {
        var status = PhotoStatus.Success;
        foreach (var issue in issues)
        {
            if (IssueCatalog.Get(issue).BlocksOutput) return PhotoStatus.Failed;
            status = PhotoStatus.NeedsReview;
        }
        return status;
    }
}
