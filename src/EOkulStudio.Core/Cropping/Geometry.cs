namespace EOkulStudio.Core.Cropping;

/// <summary>Görüntü koordinatı: x sağa, y aşağı, birim piksel.</summary>
public readonly record struct PointD(double X, double Y)
{
    /// <summary>Verilen nokta çevresinde döndürür. Pozitif açı ekranda saat yönündedir.</summary>
    public PointD RotateAround(PointD pivot, double degrees)
    {
        var (sin, cos) = Math.SinCos(degrees * Math.PI / 180.0);
        double dx = X - pivot.X, dy = Y - pivot.Y;
        return new PointD(pivot.X + dx * cos - dy * sin, pivot.Y + dx * sin + dy * cos);
    }
}

public readonly record struct RectD(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
    public PointD Center => new(Left + Width / 2, Top + Height / 2);
}
