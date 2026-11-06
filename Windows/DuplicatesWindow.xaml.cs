using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SaveTheDate;

public sealed class DupRow : INotifyPropertyChanged
{
    public required Item Item { get; init; }
    public required int GroupNo { get; init; }
    public required string GroupText { get; init; }
    public required List<DupRow> Peers { get; init; }
    public Action? Changed;
    bool del;

    public bool Delete
    {
        get => del;
        set
        {
            // Pro Gruppe muss mindestens eine Datei übrig bleiben.
            if (value && Peers.Count(r => !r.del) <= 1)
            {
                System.Media.SystemSounds.Beep.Play();
                Application.Current.Dispatcher.BeginInvoke(() => Raise(nameof(Delete)));
                return;
            }
            Set(value);
            Changed?.Invoke();
        }
    }

    public string Verdict => del ? "Löschen" : "Behalten";

    public void Set(bool v)
    {
        del = v;
        Raise(nameof(Delete));
        Raise(nameof(Verdict));
    }

    void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class DuplicatesWindow : Window
{
    readonly List<Item> files;
    CancellationTokenSource cts = new();
    List<List<Item>> groups = new();
    List<DupRow> rows = new();
    bool busy, ready, similarMode;

    /// <summary>true, wenn Dateien gelöscht wurden (Liste im Hauptfenster neu einlesen).</summary>
    public bool Changed { get; private set; }

    public DuplicatesWindow(IEnumerable<Item> items)
    {
        InitializeComponent();
        files = items.ToList();
        ready = true;
        Loaded += async (_, _) => await ScanAsync();
        Closed += (_, _) => cts.Cancel();
    }

    async Task ScanAsync()
    {
        cts.Cancel();
        cts = new CancellationTokenSource();
        var ct = cts.Token;
        var similar = similarMode = CmbMode.SelectedIndex == 1;
        var dist = CmbSens.SelectedIndex switch { 0 => (int)Sensitivity.Strict, 2 => (int)Sensitivity.Loose, _ => (int)Sensitivity.Normal };
        Wait.Visibility = Visibility.Visible;
        Table.Visibility = NoneFound.Visibility = Visibility.Collapsed;
        Prog.Value = 0;
        BtnTrash.IsEnabled = false;
        Summary.Text = "";
        void Report(double p) => Dispatcher.BeginInvoke(() => Prog.Value = p * 100);
        List<List<Item>> found;
        try
        {
            found = await Task.Run(() => similar ? Similar.Groups(files, ImageHash.Compute, dist, Report, ct) : Dupes.Find(files, Report, ct), ct);
        }
        catch (OperationCanceledException) { return; }
        if (ct.IsCancellationRequested) return;
        groups = found;
        Wait.Visibility = Visibility.Collapsed;
        Build();
    }

    void OnModeChanged(object s, RoutedEventArgs e)
    {
        if (!ready) return;
        var sim = CmbMode.SelectedIndex == 1;
        CmbSens.Visibility = sim ? Visibility.Visible : Visibility.Collapsed;
        Intro.Text = sim
            ? "Ähnliche Fotos – auch skaliert, neu komprimiert oder leicht bearbeitet. Bitte vor dem Löschen prüfen; gelöscht wird nur in den Papierkorb."
            : "Gleicher Inhalt (SHA-256) – egal, wie die Dateien heißen. Gelöscht wird nur in den Papierkorb.";
        rows = new();
        CmbKeep.SelectedIndex = sim ? 2 : 0;
        _ = ScanAsync();
    }

    void OnSensChanged(object s, RoutedEventArgs e)
    {
        if (ready) _ = ScanAsync();
    }

    void Build()
    {
        rows = new List<DupRow>();
        for (var i = 0; i < groups.Count; i++)
        {
            var peers = new List<DupRow>();
            foreach (var it in groups[i])
            {
                var r = new DupRow
                {
                    Item = it, GroupNo = i,
                    GroupText = similarMode ? $"Gruppe {i + 1} · {groups[i].Count} ähnliche Bilder" : $"Gruppe {i + 1} · {groups[i].Count} × {FormatSize(it.Size)}",
                    Peers = peers, Changed = UpdateSummary,
                };
                peers.Add(r);
                rows.Add(r);
            }
        }
        var view = new ListCollectionView(rows);
        view.SortDescriptions.Add(new SortDescription("GroupNo", ListSortDirection.Ascending));
        view.GroupDescriptions.Add(new PropertyGroupDescription("GroupText"));
        Table.ItemsSource = view;
        Table.Visibility = rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoneFound.Visibility = rows.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        AutoMark();
    }

    void AutoMark()
    {
        var rule = (Keep)Math.Max(CmbKeep.SelectedIndex, 0);
        foreach (var g in rows.GroupBy(r => r.GroupNo))
        {
            var keep = Dupes.Keeper(groups[g.Key], rule);
            foreach (var r in g) r.Set(r.Item != keep);
        }
        UpdateSummary();
    }

    void UpdateSummary()
    {
        var marked = rows.Where(r => r.Delete).ToList();
        Summary.Text = $"{groups.Count} Gruppen · {marked.Count} zum Löschen · {FormatSize(marked.Sum(r => r.Item.Size))}";
        BtnTrash.IsEnabled = marked.Count > 0 && !busy;
    }

    void OnKeepChanged(object s, RoutedEventArgs e)
    {
        if (rows.Count > 0) AutoMark();
    }

    async void OnTrash(object s, RoutedEventArgs e)
    {
        var marked = rows.Where(r => r.Delete && r.Peers.Any(p => !p.Delete)).ToList();
        var size = marked.Sum(r => r.Item.Size);
        if (MessageBox.Show(this, $"{marked.Count} Dateien in den Papierkorb legen?\n\nEs werden {FormatSize(size)} frei. Pro Gruppe bleibt mindestens eine Datei erhalten.",
                "Duplikate löschen", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        busy = true;
        BtnTrash.IsEnabled = false;
        var errors = await Task.Run(() =>
        {
            var errs = new List<string>();
            foreach (var r in marked)
            {
                try { Recycle.Delete(r.Item.FullPath, false); }
                catch (Exception ex) { if (File.Exists(r.Item.FullPath)) errs.Add($"{r.Item.Name}: {ex.Message}"); }
            }
            return errs;
        });
        busy = false;
        Changed = true;
        groups = groups.Select(g => g.Where(i => File.Exists(i.FullPath)).ToList()).Where(g => g.Count > 1).ToList();
        Build();
        MessageBox.Show(this, $"{marked.Count - errors.Count} Dateien in den Papierkorb gelegt."
            + (errors.Count > 0 ? "\n\nFehler:\n" + string.Join("\n", errors.Take(10)) : ""), "Fertig");
    }

    internal static string FormatSize(long n) => Fmt.Size(n);
}
