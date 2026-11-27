using CommunityToolkit.Mvvm.ComponentModel;

namespace EOkulStudio.Desktop.ViewModels;

/// <summary>Aşama 1 yer tutucusu. Ekranlar, taslak onayından sonra Aşama 3'te yapılacak.</summary>
public sealed class MainViewModel : ObservableObject
{
    public string Title => "E-Okul Foto Studio";

    public string Message => "Bu sürüm yalnızca program iskeletidir; henüz fotoğraf işlemez.";
}
