using System.Runtime.InteropServices;
using AltSnip.Platform;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace AltSnip;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);

        var info = this.FindControl<TextBlock>("Info");
        if (info != null)
            info.Text = $"{RuntimeInformation.OSDescription}\nplatform services: {PlatformServices.Current.Name}";
    }
}
