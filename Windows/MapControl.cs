using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SaveTheDate;

public sealed record Place(string Label, double Lat, double Lon);

/// <summary>Ortssuche über Nominatim (OpenStreetMap).</summary>
public static class Geocoder
{
    public static async Task<List<Place>> Search(string q)
    {
        var url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=6&q=" + Uri.EscapeDataString(q);
        using var doc = JsonDocument.Parse(await MapControl.Http.GetStringAsync(url));
        return doc.RootElement.EnumerateArray().Select(e => new Place(
            e.GetProperty("display_name").GetString() ?? "",
            double.Parse(e.GetProperty("lat").GetString()!, CultureInfo.InvariantCulture),
            double.Parse(e.GetProperty("lon").GetString()!, CultureInfo.InvariantCulture))).ToList();
    }
}

/// <summary>Schlanke Slippy-Map mit OpenStreetMap-Kacheln: ziehen = verschieben, Mausrad = zoomen, Klick = Pin setzen.</summary>
public sealed class MapControl : FrameworkElement
{
    public static readonly HttpClient Http = MakeHttp();

    static HttpClient MakeHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("SaveTheDate/1.0 (Foto-Datums-Editor)");
        return h;
    }

    readonly Dictionary<(int Z, int X, int Y), ImageSource?> tiles = new();
    int z = 5;
    double cx, cy;                       // Kartenmitte in Welt-Pixeln der aktuellen Zoomstufe
    double? pinLat, pinLon;
    Point down, last;
    bool moved;

    public event Action<double, double>? Picked;

    public MapControl()
    {
        ClipToBounds = true;
        Center(51, 10, 5);
    }

    static double Wx(double lon, int z) => (lon + 180) / 360 * 256 * (1 << z);

    static double Wy(double lat, int z)
    {
        var r = Math.Clamp(lat, -85.05, 85.05) * Math.PI / 180;
        return (1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * 256 * (1 << z);
    }

    static double ToLon(double x, int z) => x / (256.0 * (1 << z)) * 360 - 180;

    static double ToLat(double y, int z) => Math.Atan(Math.Sinh(Math.PI - 2 * Math.PI * y / (256.0 * (1 << z)))) * 180 / Math.PI;

    public void Center(double lat, double lon, int zoom)
    {
        z = Math.Clamp(zoom, 2, 18);
        cx = Wx(lon, z); cy = Wy(lat, z);
        InvalidateVisual();
    }

    public void SetPin(double lat, double lon, bool center)
    {
        pinLat = lat; pinLon = lon;
        if (center) Center(lat, lon, Math.Max(z, 13));
        else InvalidateVisual();
    }

    public void ZoomBy(int dz, Point at)
    {
        var nz = Math.Clamp(z + dz, 2, 18);
        if (nz == z) return;
        var f = Math.Pow(2, nz - z);
        var ox = at.X - ActualWidth / 2; var oy = at.Y - ActualHeight / 2;
        cx = (cx + ox) * f - ox; cy = (cy + oy) * f - oy;
        z = nz;
        InvalidateVisual();
    }

    public void ZoomBy(int dz) => ZoomBy(dz, new Point(ActualWidth / 2, ActualHeight / 2));

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xE8, 0xE6, 0xE0)), null, new Rect(0, 0, w, h));
        var n = 1 << z;
        int x0 = (int)Math.Floor((cx - w / 2) / 256), x1 = (int)Math.Floor((cx + w / 2) / 256);
        int y0 = Math.Max(0, (int)Math.Floor((cy - h / 2) / 256)), y1 = Math.Min(n - 1, (int)Math.Floor((cy + h / 2) / 256));
        for (var tx = x0; tx <= x1; tx++)
            for (var ty = y0; ty <= y1; ty++)
                if (Tile(z, (tx % n + n) % n, ty) is { } img)
                    dc.DrawImage(img, new Rect(Math.Round(tx * 256 - cx + w / 2), Math.Round(ty * 256 - cy + h / 2), 256, 256));

        if (pinLat is { } la && pinLon is { } lo)
        {
            var p = new Point(Wx(lo, z) - cx + w / 2, Wy(la, z) - cy + h / 2);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)), new Pen(Brushes.White, 2.5), p, 8, 8);
        }

        var ft = new FormattedText("© OpenStreetMap-Mitwirkende", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 10, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var pos = new Point(w - ft.Width - 6, h - ft.Height - 3);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), null, new Rect(pos.X - 3, pos.Y - 1, ft.Width + 6, ft.Height + 2));
        dc.DrawText(ft, pos);
    }

    ImageSource? Tile(int tz, int x, int y)
    {
        var k = (tz, x, y);
        if (tiles.TryGetValue(k, out var v)) return v;
        if (tiles.Count > 600) tiles.Clear();
        tiles[k] = null;
        _ = Load(k);
        return null;
    }

    async Task Load((int Z, int X, int Y) k)
    {
        try
        {
            var bytes = await Http.GetByteArrayAsync($"https://tile.openstreetmap.org/{k.Z}/{k.X}/{k.Y}.png");
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            tiles[k] = bmp;
            InvalidateVisual();
        }
        catch { /* offline: Kachel bleibt leer */ }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        down = last = e.GetPosition(this);
        moved = false;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var p = e.GetPosition(this);
        if (!moved && (p - down).Length > 4) moved = true;
        if (moved) { cx -= p.X - last.X; cy -= p.Y - last.Y; InvalidateVisual(); }
        last = p;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (moved) return;
        var p = e.GetPosition(this);
        var lat = ToLat(cy + p.Y - ActualHeight / 2, z);
        var lon = ToLon(cx + p.X - ActualWidth / 2, z);
        lon = ((lon + 180) % 360 + 360) % 360 - 180;
        SetPin(lat, lon, center: false);
        Picked?.Invoke(lat, lon);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e) => ZoomBy(e.Delta > 0 ? 1 : -1, e.GetPosition(this));
}
