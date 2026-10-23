using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SaveTheDate;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        Topics.ItemsSource = HelpContent.Topics.Select(t => new { t.Id, t.Title }).ToList();
        Select(null);
    }

    /// <summary>Wählt ein Thema (null = erstes) und bringt das Fenster nach vorn.</summary>
    public void Select(string? id)
    {
        var i = Math.Max(0, Array.FindIndex(HelpContent.Topics, t => t.Id == id));
        Topics.SelectedIndex = i;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    void OnTopic(object s, RoutedEventArgs e)
    {
        if (Topics.SelectedIndex < 0) return;
        var t = HelpContent.Topics[Topics.SelectedIndex];
        TitleText.Text = t.Title;
        Render(BodyText, t.Body);
    }

    static void Render(TextBlock tb, string body)
    {
        tb.Inlines.Clear();
        var lines = body.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) tb.Inlines.Add(new LineBreak());
            foreach (var part in Regex.Split(lines[i], @"(\*\*.+?\*\*|`.+?`)"))
            {
                if (part.Length > 4 && part.StartsWith("**") && part.EndsWith("**")) tb.Inlines.Add(new Bold(new Run(part[2..^2])));
                else if (part.Length > 2 && part.StartsWith('`') && part.EndsWith('`'))
                    tb.Inlines.Add(new Run(part[1..^1]) { FontFamily = new FontFamily("Consolas"), Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)) });
                else if (part.Length > 0) tb.Inlines.Add(new Run(part));
            }
        }
    }
}
