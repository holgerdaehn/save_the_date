using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SaveTheDate;

public partial class OrganizeWindow : Window
{
    readonly IReadOnlyList<Item> targets;
    List<Move> plan = new();
    bool ready;

    public List<Move> Result => plan;
    public bool Copy => CkCopy.IsChecked == true;
    public bool RemoveEmpty => CkEmpty.IsChecked == true && !Copy;
    public string Root => TxRoot.Text;

    public OrganizeWindow(IReadOnlyList<Item> targets, IEnumerable<string> sources, bool selection)
    {
        InitializeComponent();
        this.targets = targets;
        Intro.Text = $"Legt Fotos und Videos in Unterordner wie JJJJ/MM ab. Wirkt auf {(selection ? "die ausgewählten" : "alle")} Dateien der Liste.";
        TxRoot.Text = sources.FirstOrDefault(Directory.Exists) ?? "";
        ready = true;
        Refresh();
    }

    void OnChoose(object s, RoutedEventArgs e)
    {
        var d = new Microsoft.Win32.OpenFolderDialog { Title = "Zielordner für die Sortierung" };
        if (d.ShowDialog(this) == true) { TxRoot.Text = d.FolderName; Refresh(); }
    }

    void OnOpt(object s, RoutedEventArgs e) => Refresh();

    void Refresh()
    {
        if (!ready) return;
        CkEmpty.IsEnabled = !Copy;
        plan = TxRoot.Text.Length == 0
            ? new List<Move>()
            : Organizer.Plan(targets, TxRoot.Text, (Pattern)CmbPattern.SelectedIndex, (DateSource)CmbSource.SelectedIndex, CkMedia.IsChecked == true);
        Table.ItemsSource = plan;
        var todo = plan.Count(m => m.Skip == null);
        Summary.Text = $"{todo} Dateien werden {(Copy ? "kopiert" : "verschoben")} · {plan.Count - todo} übersprungen";
        BtnGo.Content = Copy ? "Kopieren" : "Ablegen";
        BtnGo.IsEnabled = todo > 0;
    }

    void OnGo(object s, RoutedEventArgs e)
    {
        var todo = plan.Count(m => m.Skip == null);
        if (MessageBox.Show(this, $"{todo} Dateien {(Copy ? "kopieren" : "verschieben")}?\n\nZiel: {Root}\nDie Aktion lässt sich über das Menü „Werkzeuge“ rückgängig machen.",
                "Nach Datum sortieren", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) DialogResult = true;
    }
}
