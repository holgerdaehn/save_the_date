using System.Windows;
using System.Windows.Controls;

namespace SaveTheDate;

public partial class RenameWindow : Window
{
    readonly IReadOnlyList<Item> targets;
    List<RenameMove> plan = new();
    bool ready;

    public List<RenameMove> Result => plan;

    public RenameWindow(IReadOnlyList<Item> targets, bool selection, bool filtered)
    {
        InitializeComponent();
        this.targets = targets;
        Intro.Text = $"Benennt Dateien nach ihrem Datum um, z. B. 2024-07-13_14-22-05.jpg. Wirkt auf {(selection ? "die ausgewählten" : filtered ? "die angezeigten" : "alle")} Dateien der Liste.";
        foreach (var p in Renamer.Presets) CmbPreset.Items.Add(p.Label);
        TxTemplate.Text = Renamer.DefaultTemplate;
        ready = true;
        Refresh();
    }

    void OnPreset(object s, RoutedEventArgs e)
    {
        if (ready && CmbPreset.SelectedIndex >= 0) TxTemplate.Text = Renamer.Presets[CmbPreset.SelectedIndex].Template;
    }

    void OnOpt(object s, RoutedEventArgs e) => Refresh();

    void Refresh()
    {
        if (!ready) return;
        plan = Renamer.Plan(targets, TxTemplate.Text, (DateSource)Math.Max(CmbSource.SelectedIndex, 0), CkLower.IsChecked == true, CkMedia.IsChecked == true);
        Table.ItemsSource = plan;
        var todo = plan.Count(m => m.Skip == null);
        Summary.Text = $"{todo} Dateien werden umbenannt · {plan.Count - todo} bleiben unverändert";
        BtnGo.IsEnabled = todo > 0;
    }

    void OnGo(object s, RoutedEventArgs e)
    {
        var todo = plan.Count(m => m.Skip == null);
        if (MessageBox.Show(this, $"{todo} Dateien umbenennen?\n\nDie Aktion lässt sich über das Menü „Werkzeuge“ rückgängig machen.",
                "Nach Datum umbenennen", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) DialogResult = true;
    }
}
