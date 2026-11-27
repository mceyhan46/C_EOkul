namespace EOkulStudio.Infrastructure;

/// <summary>Programın kendi verileri (ayar ve günlük). Fotoğraf klasörlerine yazılmaz.</summary>
public sealed record AppPaths(string DataDirectory)
{
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public static AppPaths Default() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EOkulFotoStudio"));
}
