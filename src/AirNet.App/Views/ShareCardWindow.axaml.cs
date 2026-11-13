using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace AirNet.App.Views;

public partial class ShareCardWindow : Window
{
    private const int CardWidth = 1080;
    private const int CardHeight = 1350;

    public ShareCardWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();

    /// <summary>The window has no native title bar; the header row is the drag handle.</summary>
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private async void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.ShareCardViewModel viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "保存 AirNet 分享卡片",
            SuggestedFileName = $"airnet-{DateTime.Now:yyyyMMdd-HHmm}.png",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG 图片") { Patterns = ["*.png"] }],
        });

        if (file is null)
        {
            return;
        }

        var temp = Path.Combine(Path.GetTempPath(), $"airnet-share-{Guid.NewGuid():N}.png");
        try
        {
            ShareCardRenderer.Save(viewModel, temp);
            await using var source = File.OpenRead(temp);
            await using var target = await file.OpenWriteAsync();
            await source.CopyToAsync(target);
            Status.Text = "已保存 PNG";
        }
        catch (Exception ex)
        {
            Status.Text = $"保存失败：{ex.Message}";
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
