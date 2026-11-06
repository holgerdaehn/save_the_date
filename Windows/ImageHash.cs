using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SaveTheDate;

/// <summary>Bild-Hash für „Ähnliche Bilder“ (64-Bit-Differenz-Hash auf 9×8 Graustufen) und Vorschaubilder.</summary>
public static class ImageHash
{
    public static ulong? Compute(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = fs;
            bi.DecodePixelWidth = 90; bi.DecodePixelHeight = 80;       // 10×10 Pixel je Zelle des 9×8-Rasters
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bi.EndInit();
            bi.Freeze();
            var g = new FormatConvertedBitmap(bi, PixelFormats.Gray8, null, 0);
            var px = new byte[90 * 80];
            g.CopyPixels(px, 90, 0);
            var cell = new double[9 * 8];
            for (var y = 0; y < 80; y++)
                for (var x = 0; x < 90; x++) cell[y / 10 * 9 + x / 10] += px[y * 90 + x];
            ulong h = 0;
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++) h = h << 1 | (cell[y * 9 + x] > cell[y * 9 + x + 1] ? 1UL : 0UL);
            return h;
        }
        catch { return null; }
    }
}

/// <summary>Pfad → Vorschaubild (null bei Dateien, die WPF nicht darstellen kann). Läuft asynchron per IsAsync-Binding.</summary>
public sealed class ThumbConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path) return null;
        try
        {
            using var fs = File.OpenRead(path);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = fs;
            bi.DecodePixelWidth = 96;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
