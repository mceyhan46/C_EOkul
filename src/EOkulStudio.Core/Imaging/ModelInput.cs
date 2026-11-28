namespace EOkulStudio.Core.Imaging;

public static class ModelInput
{
    /// <summary>MODNet girişi: kısa kenar <paramref name="shortSide"/>, iki kenar da 32'nin katı.</summary>
    public static (int Width, int Height) MattingSize(int width, int height, int shortSide = 512)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int w, h;
        if (width >= height)
        {
            h = shortSide;
            w = (int)((double)width / height * shortSide);
        }
        else
        {
            w = shortSide;
            h = (int)((double)height / width * shortSide);
        }
        return (Math.Max(32, w - w % 32), Math.Max(32, h - h % 32));
    }
}
