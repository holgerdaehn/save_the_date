using System.IO;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SaveTheDate;

public partial class MainWindow : Window
{
    readonly List<string> sources = new();
    ObservableCollection<Item> items = new();
    ListCollectionView view;
    Dictionary<Item, Plan> plans = new();
    List<Item> work = new();
    Settings settings = new();
    Dictionary<string, (DateTime C, DateTime M)> undo = new();
    CancellationTokenSource? scanCts;
    (DatePicker Dp, TextBox Tm)[] pairs = [];
    bool ready, busy, restoring;
    double? lat, lon;
    List<Op> lastOps = new();
    string? lastRoot;

    public MainWindow()
    {
        InitializeComponent();
        view = new ListCollectionView(items);
        view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
        Table.ItemsSource = view;
        pairs = [(DpC, TmC), (DpM, TmM), (DpT, TmT)];
        SetAll(DateTime.Now);
        MapView.Picked += (la, lo) => SetCoord(la, lo, center: false);
        ready = true;
        UpdateModeUi();
        Recompute();
    }

    // ---------- Quellen & Scan ----------

    void OpenFiles(object? s, RoutedEventArgs? e)
    {
        var d = new Microsoft.Win32.OpenFileDialog { Multiselect = true, Title = "Dateien auswählen" };
        if (d.ShowDialog(this) == true) AddSources(d.FileNames);
    }

    void OpenFolder(object? s, RoutedEventArgs? e)
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Multiselect = true, Title = "Ordner auswählen" };
        if (d.ShowDialog(this) == true) AddSources(d.FolderNames);
    }

    void OnDragOver(object s, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    void OnDrop(object s, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddSources(paths);
    }

    void AddSources(IEnumerable<string> paths)
    {
        foreach (var p in paths)
            if (!sources.Contains(p, StringComparer.OrdinalIgnoreCase)) sources.Add(p);
        _ = RescanAsync();
    }

    void OnRescan(object? s, RoutedEventArgs? e) => _ = RescanAsync();

    void OnClear(object s, RoutedEventArgs e)
    {
        scanCts?.Cancel();
        sources.Clear();
        _ = RescanAsync();
    }

    void OnScanOption(object s, RoutedEventArgs e) => _ = RescanAsync();

    async Task RescanAsync()
    {
        scanCts?.Cancel();
        var cts = scanCts = new CancellationTokenSource();
        var src = sources.ToArray();
        var opt = new ScanOptions(MnuRecursive.IsChecked, MnuDirs.IsChecked, MnuHidden.IsChecked);
        var keep = Table.SelectedItems.Cast<Item>().Select(i => i.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<Item> list = new();
        if (src.Length > 0)
        {
            SetBusy("Einlesen …", indeterminate: true);
            try { list = await Task.Run(() => Scanner.Run(src, opt, cts.Token), cts.Token); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { SetBusy(null); MessageBox.Show(this, ex.Message, "Fehler beim Einlesen", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            if (cts.IsCancellationRequested) return;
        }

        var sort = view.SortDescriptions.ToList();
        items = new ObservableCollection<Item>(list);
        view = new ListCollectionView(items);
        foreach (var d in sort) view.SortDescriptions.Add(d);
        ApplyFilter();
        Table.ItemsSource = view;
        restoring = true;
        foreach (var it in items.Where(i => keep.Contains(i.FullPath))) Table.SelectedItems.Add(it);
        restoring = false;
        EmptyHint.Visibility = src.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        SetBusy(null);
        Recompute();
    }

    void OnSearch(object s, RoutedEventArgs e) => ApplyFilter();

    void ApplyFilter()
    {
        var q = Search.Text.Trim();
        view.Filter = q.Length == 0 ? null : o => ((Item)o).Name.Contains(q, StringComparison.CurrentCultureIgnoreCase);
    }

    void OnSelectAll(object s, RoutedEventArgs e) => Table.SelectAll();

    void OnRemove(object? s, RoutedEventArgs? e)
    {
        foreach (var it in Table.SelectedItems.Cast<Item>().ToList()) items.Remove(it);
        Recompute();
    }

    void OnColumn(object s, RoutedEventArgs e)
    {
        var mi = (MenuItem)s;
        var first = int.Parse((string)mi.Tag);
        var vis = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        foreach (var i in first == 6 ? new[] { 6, 7, 8 } : new[] { first }) Table.Columns[i].Visibility = vis;
    }

    void OnSelectionChanged(object s, RoutedEventArgs e) { if (!restoring) Recompute(); }

    void OnExit(object s, RoutedEventArgs e) => Close();

    void OnAbout(object s, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();

    HelpWindow? help;

    void OnHelp(object? s, RoutedEventArgs? e) => ShowHelp(null);

    void OnKeys(object s, RoutedEventArgs e) => ShowHelp("keys");

    /// <summary>Öffnet die Anleitung (ein einziges, nicht-modales Fenster) beim gewünschten Thema.</summary>
    public void ShowHelp(string? topic)
    {
        if (help == null)
        {
            help = new HelpWindow { Owner = this };
            help.Closed += (_, _) => help = null;
            help.Show();
        }
        help.Select(topic);
    }

    void OnKey(object s, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl && e.Key == Key.O)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) OpenFolder(null, null); else OpenFiles(null, null);
            e.Handled = true;
        }
        else if (ctrl && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && e.Key is Key.D or Key.S or Key.K)
        {
            if (e.Key == Key.D) OnDupes(null, null); else if (e.Key == Key.S) OnOrganize(null, null); else OnCleanup(null, null);
            e.Handled = true;
        }
        else if (e.Key == Key.F1) { ShowHelp(null); e.Handled = true; }
        else if (e.Key == Key.F5) { _ = RescanAsync(); e.Handled = true; }
        else if (ctrl && e.Key == Key.Enter) { OnApply(null, null); e.Handled = true; }
        else if (e.Key == Key.Delete && Table.IsKeyboardFocusWithin && e.OriginalSource is not TextBoxBase)
        {
            OnRemove(null, null);
            e.Handled = true;
        }
    }

    // ---------- Einstellungen ----------

    void OnModeChanged(object s, RoutedEventArgs e)
    {
        if (!ready) return;
        UpdateModeUi();
        Recompute();
    }

    void UpdateModeUi()
    {
        var fixedMode = RbFixed.IsChecked == true;
        var exif = RbExif.IsChecked == true;
        foreach (var p in new UIElement[] { PkC, PkM, PkT, PnFixed })
            p.Visibility = fixedMode ? Visibility.Visible : Visibility.Collapsed;
        PnShift.Visibility = RbShift.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PnExif.Visibility = exif ? Visibility.Visible : Visibility.Collapsed;
        RowT.Visibility = exif ? Visibility.Collapsed : Visibility.Visible;
    }

    void OnSettingChanged(object s, RoutedEventArgs e) => Recompute();

    void OnGeoModeChanged(object s, RoutedEventArgs e)
    {
        if (!ready) return;
        PnGeo.Visibility = RbGeoSet.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        Recompute();
    }

    void OnLinkChanged(object s, RoutedEventArgs e)
    {
        if (CkLink.IsChecked == true) CopyPair(0, 1, 2);
        Recompute();
    }

    void OnDateEdited(object s, RoutedEventArgs e)
    {
        if (!ready || busy) return;
        if (CkLink.IsChecked == true)
        {
            var i = Array.FindIndex(pairs, p => p.Dp == s || p.Tm == s);
            if (i >= 0) CopyPair(i, Enumerable.Range(0, 3).Where(j => j != i).ToArray());
        }
        Recompute();
    }

    void CopyPair(int from, params int[] to)
    {
        busy = true;
        foreach (var j in to)
        {
            pairs[j].Dp.SelectedDate = pairs[from].Dp.SelectedDate;
            pairs[j].Tm.Text = pairs[from].Tm.Text;
        }
        busy = false;
    }

    void SetAll(DateTime dt)
    {
        busy = true;
        foreach (var p in pairs) { p.Dp.SelectedDate = dt.Date; p.Tm.Text = dt.ToString("HH:mm:ss"); }
        busy = false;
    }

    void OnNow(object s, RoutedEventArgs e)
    {
        var i = "CMT".IndexOf((string)((FrameworkElement)s).Tag);
        var now = DateTime.Now;
        busy = true;
        pairs[i].Dp.SelectedDate = now.Date;
        pairs[i].Tm.Text = now.ToString("HH:mm:ss");
        busy = false;
        if (CkLink.IsChecked == true) CopyPair(i, Enumerable.Range(0, 3).Where(j => j != i).ToArray());
        Recompute();
    }

    DateTime? Combine(DatePicker dp, TextBox tm)
    {
        var ok = dp.SelectedDate != null
                 && TimeSpan.TryParse(tm.Text, CultureInfo.InvariantCulture, out var ts)
                 && ts >= TimeSpan.Zero && ts < TimeSpan.FromDays(1);
        if (ok) tm.ClearValue(Control.ForegroundProperty); else tm.Foreground = Brushes.IndianRed;
        return ok ? dp.SelectedDate!.Value.Date + TimeSpan.Parse(tm.Text, CultureInfo.InvariantCulture) : null;
    }

    static int Int(TextBox t) => int.TryParse(t.Text, out var v) ? Math.Clamp(v, 0, 100000) : 0;

    Settings ReadSettings()
    {
        var s = new Settings
        {
            Mode = RbShift.IsChecked == true ? Mode.Shift : RbExif.IsChecked == true ? Mode.Exif : Mode.Fixed,
            SetCreated = CkC.IsChecked == true, SetModified = CkM.IsChecked == true, SetTaken = CkT.IsChecked == true,
            Created = Combine(DpC, TmC), Modified = Combine(DpM, TmM), Taken = Combine(DpT, TmT),
            Interval = double.TryParse(TxInterval.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var iv) ? Math.Max(0, iv) : 0,
            Later = RbLater.IsChecked == true,
            Days = Int(TxD), Hours = Int(TxH), Minutes = Int(TxMin), Seconds = Int(TxS),
            Geo = RbGeoSet.IsChecked == true ? GeoKind.Set : RbGeoRemove.IsChecked == true ? GeoKind.Remove : GeoKind.Keep,
            Lat = lat, Lon = lon,
        };
        s.Valid = s.Mode != Mode.Fixed
                  || (!s.SetCreated || s.Created != null) && (!s.SetModified || s.Modified != null) && (!s.SetTaken || s.Taken != null);
        return s;
    }

    GeoEdit Geo(Settings s) => s.Geo switch
    {
        GeoKind.Remove => new GeoEdit(GeoKind.Remove),
        GeoKind.Set when s.Lat != null && s.Lon != null => new GeoEdit(GeoKind.Set, s.Lat.Value, s.Lon.Value),
        _ => new GeoEdit(GeoKind.Keep),
    };

    IReadOnlyList<Item> Targets() => Table.SelectedItems.Count > 0 ? Table.SelectedItems.Cast<Item>().ToList() : items;

    void Recompute()
    {
        if (!ready) return;
        settings = ReadSettings();
        var targets = Targets();
        plans = Planner.Make(targets, settings);
        foreach (var it in items) it.SetPlan(plans.TryGetValue(it, out var p) ? p : default);
        var geo = Geo(settings).Kind != GeoKind.Keep;
        work = targets.Where(i => plans.TryGetValue(i, out var p) && !p.IsEmpty || geo && i.IsJpeg).ToList();

        var n = work.Count;
        BtnApply.IsEnabled = n > 0 && !busy && settings.Valid;
        BtnApply.Content = !settings.Valid ? "Ungültiges Datum" : n == 0 ? "Keine Änderung" : $"{n} Objekte ändern";
        InfoText.Text = Table.SelectedItems.Count == 0
            ? $"Wirkt auf alle {items.Count} Objekte"
            : $"Wirkt auf {Table.SelectedItems.Count} ausgewählte Objekte";
        if (!busy)
            StatusText.Text = $"{items.Count} Objekte · {Table.SelectedItems.Count} ausgewählt · {items.Count(i => i.Lat != null)} mit Standort";
    }

    // ---------- Standort ----------

    void SetCoord(double la, double lo, bool center)
    {
        lat = la; lon = lo;
        GeoText.Text = string.Create(CultureInfo.InvariantCulture, $"{la:F6}, {lo:F6}");
        MapView.SetPin(la, lo, center);
        Recompute();
    }

    void OnZoomIn(object s, RoutedEventArgs e) => MapView.ZoomBy(1);

    void OnZoomOut(object s, RoutedEventArgs e) => MapView.ZoomBy(-1);

    async void OnGeoQueryKey(object s, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || GeoQuery.Text.Trim().Length == 0) return;
        try
        {
            var hits = await Geocoder.Search(GeoQuery.Text.Trim());
            GeoHits.ItemsSource = hits;
            GeoHits.Visibility = hits.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (hits.Count == 0) StatusText.Text = "Kein Ort gefunden.";
        }
        catch (Exception ex) { StatusText.Text = "Ortssuche fehlgeschlagen: " + ex.Message; }
    }

    void OnGeoHit(object s, RoutedEventArgs e)
    {
        if (GeoHits.SelectedItem is not Place p) return;
        SetCoord(p.Lat, p.Lon, center: true);
        GeoHits.Visibility = Visibility.Collapsed;
        GeoHits.SelectedItem = null;
    }

    void OnGeoTextKey(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ParseCoords();
    }

    void OnGeoTextLost(object s, RoutedEventArgs e) => ParseCoords();

    /// <summary>"52.52, 13.40" oder (deutsche Schreibweise) "52,52; 13,40".</summary>
    void ParseCoords()
    {
        var t = GeoText.Text.Trim();
        if (t.Length == 0) return;
        if (t.Contains(';')) t = t.Replace(',', '.').Replace(';', ',');
        var p = t.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => double.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? (double?)v : null).ToArray();
        if (p is [{ } la, { } lo] && Math.Abs(la) <= 90 && Math.Abs(lo) <= 180) SetCoord(la, lo, center: true);
        else if (lat is { } a && lon is { } b) GeoText.Text = string.Create(CultureInfo.InvariantCulture, $"{a:F6}, {b:F6}");
        else { GeoText.Clear(); System.Media.SystemSounds.Beep.Play(); }
    }

    void OnGeoFromSelection(object s, RoutedEventArgs e)
    {
        if (Targets().FirstOrDefault(i => i.Lat != null) is { Lat: { } la, Lon: { } lo }) SetCoord(la, lo, center: true);
        else StatusText.Text = "Keine Datei mit Standort in der Auswahl.";
    }

    // ---------- Anwenden ----------

    void OnApply(object? s, RoutedEventArgs? e)
    {
        if (work.Count == 0 || busy || !settings.Valid) return;
        if (MnuConfirm.IsChecked && MessageBox.Show(this, $"{work.Count} Objekte werden geändert.", "Änderungen anwenden?",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        var pl = plans;
        var geo = Geo(settings);
        undo = work.ToDictionary(i => i.FullPath, i => (i.Created, i.Modified), StringComparer.OrdinalIgnoreCase);
        BtnUndo.IsEnabled = MnuUndo.IsEnabled = true;
        _ = RunAsync(work.ToList(), "geändert", it => Writer.Apply(it, pl.GetValueOrDefault(it), geo));
    }

    /// <summary>Setzt nur Datei-Daten zurück; EXIF-Änderungen werden nicht rückgängig gemacht.</summary>
    void OnUndo(object s, RoutedEventArgs e)
    {
        var u = undo;
        BtnUndo.IsEnabled = MnuUndo.IsEnabled = false;
        _ = RunAsync(items.Where(i => u.ContainsKey(i.FullPath)).ToList(), "zurückgesetzt",
            it => Writer.Apply(it, new Plan(u[it.FullPath].C, u[it.FullPath].M, null), new GeoEdit(GeoKind.Keep)));
    }

    async Task RunAsync(List<Item> jobs, string verb, Action<Item> job)
    {
        SetBusy("Wird angewendet …", indeterminate: false);
        var errors = new ConcurrentBag<string>();
        var done = 0;
        await Task.Run(() => Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = 8 }, it =>
        {
            try { job(it); }
            catch (Exception ex) { errors.Add($"{it.Name}: {ex.Message}"); }
            var d = Interlocked.Increment(ref done);
            if (d % 8 == 0 || d == jobs.Count) Dispatcher.BeginInvoke(() => Prog.Value = d * 100.0 / jobs.Count);
        }));
        SetBusy(null);
        var msg = $"{jobs.Count - errors.Count} von {jobs.Count} Objekten {verb}.";
        if (!errors.IsEmpty)
        {
            msg += "\n\nFehler:\n" + string.Join("\n", errors.Take(10));
            if (errors.Count > 10) msg += $"\n… und {errors.Count - 10} weitere";
        }
        MessageBox.Show(this, msg, "Fertig", MessageBoxButton.OK, errors.IsEmpty ? MessageBoxImage.Information : MessageBoxImage.Warning);
        await RescanAsync();
    }

    // ---------- Duplikate, Sortieren, Aufräumen ----------

    void OnDupes(object? s, RoutedEventArgs? e)
    {
        if (items.Count == 0 || busy) return;
        var w = new DuplicatesWindow(items) { Owner = this };
        w.ShowDialog();
        if (w.Changed) _ = RescanAsync();
    }

    void OnOrganize(object? s, RoutedEventArgs? e)
    {
        if (items.Count == 0 || busy) return;
        var w = new OrganizeWindow(Targets(), sources, Table.SelectedItems.Count > 0) { Owner = this };
        if (w.ShowDialog() == true) _ = OrganizeAsync(w.Result, w.Copy, w.RemoveEmpty, w.Root);
    }

    async Task OrganizeAsync(List<Move> plan, bool copy, bool removeEmpty, string root)
    {
        var roots = sources.Where(Directory.Exists).ToList();
        SetBusy("Sortiere …", indeterminate: false);
        var r = await Task.Run(() => Organizer.Run(plan, copy, removeEmpty, roots, p => Dispatcher.BeginInvoke(() => Prog.Value = p * 100)));
        lastOps = r.Ops;
        lastRoot = root;
        MnuUndoSort.IsEnabled = r.Ops.Count > 0;
        if (!sources.Any(x => Under(root, x))) sources.Add(root);
        SetBusy(null);
        var msg = $"{r.Ops.Count} Dateien {(copy ? "kopiert" : "abgelegt")}, {r.Skipped} übersprungen"
                  + (r.RemovedDirs > 0 ? $", {r.RemovedDirs} leere Ordner entfernt" : "") + ".";
        if (r.Errors.Count > 0) msg += "\n\nFehler:\n" + string.Join("\n", r.Errors.Take(10));
        MessageBox.Show(this, msg, "Fertig", MessageBoxButton.OK, r.Errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        await RescanAsync();
    }

    async void OnUndoSort(object s, RoutedEventArgs e)
    {
        if (lastRoot is not { } root) return;
        var ops = lastOps;
        lastOps = new();
        MnuUndoSort.IsEnabled = false;
        SetBusy("Stelle wieder her …", indeterminate: true);
        var errs = await Task.Run(() => Organizer.Undo(ops, root));
        SetBusy(null);
        MessageBox.Show(this, $"{ops.Count - errs.Count} Dateien zurückgelegt." + (errs.Count > 0 ? "\n\nFehler:\n" + string.Join("\n", errs.Take(10)) : ""), "Fertig");
        await RescanAsync();
    }

    void OnCleanup(object? s, RoutedEventArgs? e)
    {
        var roots = sources.Where(Directory.Exists).ToList();
        if (roots.Count == 0)
        {
            MessageBox.Show(this, "Zum Aufräumen zuerst einen Ordner hinzufügen.", "Aufräumen", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var w = new CleanupWindow(roots) { Owner = this };
        w.ShowDialog();
        if (w.Changed) _ = RescanAsync();
    }

    static bool Under(string path, string root)
    {
        var r = Path.TrimEndingDirectorySeparator(root);
        return path.Equals(r, StringComparison.OrdinalIgnoreCase) || path.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    void SetBusy(string? text, bool indeterminate = false)
    {
        busy = text != null;
        Prog.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        Prog.IsIndeterminate = indeterminate;
        Prog.Value = 0;
        if (busy) StatusText.Text = text;
        if (!busy) Recompute();
    }
}
