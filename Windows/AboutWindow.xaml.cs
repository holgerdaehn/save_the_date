using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Navigation;

namespace SaveTheDate;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var v = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
        TxVersion.Text = $"Version {v}";
        TxVersion2.Text = v;
        TxSystem.Text = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture}) · {RuntimeInformation.FrameworkDescription}";
    }

    void OnLink(object s, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    void OnHelp(object s, RoutedEventArgs e)
    {
        var main = Owner as MainWindow;
        Close();
        main?.ShowHelp(null);
    }
}
