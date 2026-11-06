using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace SaveTheDate;

public sealed record GeoRow(Item Item, double? Lat, double? Lon, string Note)
{
    public string Where => Lat is { } la && Lon is { } lo ? string.Create(CultureInfo.InvariantCulture, $"{la:F5}, {lo:F5}") : Note;
}

public partial class GeotagWindow : Window
{
    readonly IReadOnlyList<Item> targets;
    List<GpxPoint> points = new();
    List<GeoRow> rows = new();
    bool ready;

    /// <summary>Datei → neuer Standort.</summary>
    public Dictionary<Item, (double Lat, double Lon)> Result =>
        rows.Where(r => r.Lat != null).ToDictionary(r => r.Item, r => (r.Lat!.Value, r.Lon!.Value));

    public GeotagWindow(IReadOnlyList<Item> targets)
    {
        InitializeComponent();
        this.targets = targets.Where(i => i.IsJpeg).ToList();
        ready = true;
        Refresh();
    }

    void OnChoose(object s, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "GPX-Tracklog(s) auswählen", Filter = "GPX-Tracklog (*.gpx)|*.gpx|Alle Dateien|*.*" };
        if (d.ShowDialog(this) != true) return;
        points = Gpx.Parse(d.FileNames);
        TxFiles.Text = $"{d.FileNames.Length} Datei(en) · {points.Count} Punkte";
        Refresh();
    }

    static double Num(TextBox t, double def) =>
        double.TryParse(t.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;

    void OnOpt(object s, RoutedEventArgs e) => Refresh();

    void Refresh()
    {
        if (!ready) return;
        TxZone.IsEnabled = CkZone.IsChecked == true;
        double? zone = CkZone.IsChecked == true ? Math.Clamp(Num(TxZone, 0), -14, 14) : null;
        var gap = TimeSpan.FromMinutes(Math.Max(0, Num(TxGap, 15)));
        var overwrite = CkOverwrite.IsChecked == true;
        rows = targets.Select(it =>
        {
            if (it.Taken is not { } t) return new GeoRow(it, null, null, "Kein Aufnahmedatum");
            if (it.Lat != null && !overwrite) return new GeoRow(it, null, null, "Hat schon einen Standort");
            return Gpx.Locate(points, Gpx.Instant(t, zone), gap) is { } p
                ? new GeoRow(it, p.Lat, p.Lon, "")
                : new GeoRow(it, null, null, points.Count == 0 ? "Kein Track geladen" : "Kein Trackpunkt in der Nähe");
        }).ToList();
        Table.ItemsSource = rows;
        var hits = rows.Count(r => r.Lat != null);
        Summary.Text = $"{hits} von {rows.Count} Fotos bekommen einen Standort";
        BtnGo.IsEnabled = hits > 0;
    }

    void OnGo(object s, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, $"{rows.Count(r => r.Lat != null)} Fotos mit Standort versehen?\n\nDer Standort wird verlustfrei in die EXIF-Daten geschrieben und lässt sich über „Letzte Änderung rückgängig“ zurücknehmen.",
                "Geotagging", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) DialogResult = true;
    }
}
