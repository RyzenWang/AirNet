using Avalonia;
using Avalonia.Media.Imaging;
using AirNet.App.ViewModels;

namespace AirNet.App.Views;

/// <summary>Renders the fixed-size share card independently of the preview window.</summary>
public static class ShareCardRenderer
{
    public const int Width = 1080;
    public const int Height = 1350;

    public static void Save(ShareCardViewModel viewModel, string path)
    {
        var card = new ShareCardView { DataContext = viewModel };

        // A detached UserControl has no inherited theme resources; host it in a hidden
        // off-screen window before measuring so DynamicResource tokens and control templates
        // are resolved exactly as they are in the preview.
        var host = new Avalonia.Controls.Window
        {
            Width = Width,
            Height = Height,
            CanResize = false,
            ShowInTaskbar = false,
            SystemDecorations = Avalonia.Controls.SystemDecorations.None,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.Manual,
            Position = new PixelPoint(-20000, -20000),
            Content = card,
        };

        host.Show();
        try
        {
            card.Measure(new Size(Width, Height));
            card.Arrange(new Rect(0, 0, Width, Height));
            card.UpdateLayout();

            using var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));
            bitmap.Render(card);
            bitmap.Save(path);
        }
        finally
        {
            host.Close();
        }
    }
}
