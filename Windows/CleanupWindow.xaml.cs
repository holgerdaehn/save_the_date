using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace SaveTheDate;

public sealed class JunkRow(Junk j, Action changed) : INotifyPropertyChanged
{
    bool marked = true;
    public Junk J => j;
    public string Name => System.IO.Path.GetFileName(j.Path);
    public string Folder => System.IO.Path.GetDirectoryName(j.Path) ?? "";
    public string Reason => j.Reason;
    public string SizeText => j.IsDir ? "" : DuplicatesWindow.FormatSize(j.Size);

    public bool Marked
    {
        get => marked;
        set { marked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Marked))); changed(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class CleanupWindow : Window
{
    readonly List<string> roots;
    List<JunkRow> rows = new();
    CancellationTokenSource? cts;

    /// <summary>true, wenn etwas gelöscht wurde (Liste im Hauptfenster neu einlesen).</summary>
    public bool Changed { get; private set; }

    public CleanupWindow(IEnumerable<string> roots)
    {
        InitializeComponent();
        this.roots = roots.ToList();
        Loaded += async (_, _) => await ScanAsync();
        Closed += (_, _) => cts?.Cancel();
    }

    void OnPatternKey(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _ = ScanAsync();
    }

    void OnRescan(object s, RoutedEventArgs e) => _ = ScanAsync();

    async Task ScanAsync()
    {
        if (!IsLoaded) return;
        cts?.Cancel();
        var c = cts = new CancellationTokenSource();
        Wait.Visibility = Visibility.Visible;
        Table.ItemsSource = null;
        var extra = Cleanup.Patterns(TxPatterns.Text);
        var empty = CkEmpty.IsChecked == true;
        List<Junk> found;
        try { found = await Task.Run(() => Cleanup.Scan(roots, extra, empty, c.Token), c.Token); }
        catch (OperationCanceledException) { return; }
        rows = found.Select(j => new JunkRow(j, Update)).ToList();
        Table.ItemsSource = rows;
        Wait.Visibility = Visibility.Collapsed;
        NoneFound.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Update();
    }

    void Update()
    {
        var m = rows.Where(r => r.Marked).ToList();
        Summary.Text = $"{m.Count} von {rows.Count} ausgewählt · {DuplicatesWindow.FormatSize(m.Sum(r => r.J.Size))}";
        BtnTrash.IsEnabled = m.Count > 0;
    }

    void OnAll(object s, RoutedEventArgs e) { foreach (var r in rows) r.Marked = true; }

    void OnNone(object s, RoutedEventArgs e) { foreach (var r in rows) r.Marked = false; }

    async void OnTrash(object s, RoutedEventArgs e)
    {
        var marked = rows.Where(r => r.Marked).Select(r => r.J).ToList();
        if (MessageBox.Show(this, $"{marked.Count} Elemente in den Papierkorb legen?", "Aufräumen",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        BtnTrash.IsEnabled = false;
        var errors = await Task.Run(() =>
        {
            var errs = new List<string>();
            foreach (var j in marked)    // Reihenfolge: tiefste Einträge zuerst
            {
                try { Recycle.Delete(j.Path, j.IsDir); }
                catch (Exception ex) { if (File.Exists(j.Path) || Directory.Exists(j.Path)) errs.Add($"{System.IO.Path.GetFileName(j.Path)}: {ex.Message}"); }
            }
            return errs;
        });
        Changed = true;
        MessageBox.Show(this, $"{marked.Count - errors.Count} Elemente in den Papierkorb gelegt."
            + (errors.Count > 0 ? "\n\nFehler:\n" + string.Join("\n", errors.Take(10)) : ""), "Fertig");
        await ScanAsync();
    }
}
