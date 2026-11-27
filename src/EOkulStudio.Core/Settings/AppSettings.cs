using EOkulStudio.Core.Cropping;
using EOkulStudio.Core.Files;

namespace EOkulStudio.Core.Settings;

public sealed record WindowPlacement(double Left, double Top, double Width, double Height, bool Maximized);

/// <summary>Kullanıcı ayarları. Boyut ve kalite varsayılanları Android sürümünden alınmıştır.</summary>
public sealed record AppSettings
{
    public const int MaxRecentFolders = 8;
    public const string DefaultMattingModel = "modnet";

    public bool AutoStart { get; init; } = true;
    public bool IncludeSubfolders { get; init; } = true;
    public ExistingOutputPolicy ExistingOutput { get; init; } = ExistingOutputPolicy.Skip;
    public int JpegQuality { get; init; } = 100;
    public int MinFileSizeKb { get; init; } = 20;
    public int MaxFileSizeKb { get; init; } = 150;
    public CropRules Crop { get; init; } = new();
    public string MattingModel { get; init; } = DefaultMattingModel;
    public IReadOnlyList<string> RecentFolders { get; init; } = [];
    public WindowPlacement? Window { get; init; }

    /// <summary>Eksik veya aralık dışı değerleri düzeltir.</summary>
    public AppSettings Normalized()
    {
        var maxKb = Math.Clamp(MaxFileSizeKb, 5, 5000);
        return this with
        {
            ExistingOutput = Enum.IsDefined(ExistingOutput) ? ExistingOutput : ExistingOutputPolicy.Skip,
            JpegQuality = Math.Clamp(JpegQuality, 70, 100),
            MaxFileSizeKb = maxKb,
            MinFileSizeKb = Math.Clamp(MinFileSizeKb, 0, Math.Min(500, maxKb)),
            Crop = (Crop ?? new CropRules()).Normalized(),
            MattingModel = string.IsNullOrWhiteSpace(MattingModel) ? DefaultMattingModel : MattingModel,
            RecentFolders = (RecentFolders ?? [])
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxRecentFolders)
                .ToList(),
        };
    }

    public AppSettings WithRecentFolder(string folder) =>
        (this with { RecentFolders = [folder, .. RecentFolders] }).Normalized();
}
