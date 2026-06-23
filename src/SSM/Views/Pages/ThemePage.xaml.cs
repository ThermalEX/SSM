using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using UserControl = System.Windows.Controls.UserControl;
using SSM.Core.Interfaces;

namespace SSM.Views.Pages;

public partial class ThemePage : UserControl
{
    private ISettingsService? _settingsService;

    // Raised when user applies a theme; host (MainWindow) listens and reloads overlay
    public event Action<string>? ThemeApplied;

    // Absolute path to Themes/monitor/ next to exe
    private static string MonitorThemesDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "monitor");

    public ThemePage()
    {
        InitializeComponent();
    }

    public void Initialize(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        Loaded += (_, _) => RefreshCards();
    }

    // Called by MainWindow after a template change to keep cards in sync
    public void Refresh() => RefreshCards();

    // ── Card building ─────────────────────────────────────────

    private void RefreshCards()
    {
        CardPanel.Children.Clear();

        if (!Directory.Exists(MonitorThemesDir)) return;

        // Each subdirectory under Themes/monitor/ is a theme
        var dirs = Directory.GetDirectories(MonitorThemesDir)
            .OrderBy(d => d)
            .ToList();

        string activeFull = "";
        if (_settingsService is not null)
        {
            var rel = _settingsService.Settings.ActiveSp2Template;
            activeFull = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rel));
        }

        foreach (var dir in dirs)
        {
            var sp2 = Directory.GetFiles(dir, "*.sp2").FirstOrDefault();
            if (sp2 is null) continue;

            bool isActive = string.Equals(
                Path.GetFullPath(sp2), activeFull,
                StringComparison.OrdinalIgnoreCase);

            CardPanel.Children.Add(BuildCard(dir, sp2, isActive));
        }
    }

    private Border BuildCard(string dir, string sp2Path, bool isActive)
    {
        var name = Path.GetFileName(dir);

        // Outer card border
        var card = new Border
        {
            Width         = 220,
            Margin        = new Thickness(8),
            CornerRadius  = new CornerRadius(10),
            BorderThickness = new Thickness(isActive ? 2 : 1),
        };
        card.SetResourceReference(Border.BackgroundProperty, "BgCard");
        if (isActive)
            card.SetResourceReference(Border.BorderBrushProperty, "Accent");
        else
            card.SetResourceReference(Border.BorderBrushProperty, "DividerBrush");

        // Thumbnail (background_400.png or any first png)
        var thumb = new Border
        {
            Height       = 110,
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            ClipToBounds = true,
        };
        var thumbImg = FindThumbnail(dir);
        if (thumbImg is not null)
            thumb.Background = new ImageBrush(thumbImg) { Stretch = Stretch.UniformToFill };
        else
            thumb.SetResourceReference(Border.BackgroundProperty, "BgDeep");

        // Info area
        var nameTb = new TextBlock
        {
            Text       = name,
            FontSize   = 13,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 4),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        nameTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");

        var applyBtn = new Button
        {
            Content = isActive ? "✓ 当前使用" : "应用",
            IsEnabled = !isActive,
            Padding = new Thickness(0, 5, 0, 5),
            Margin  = new Thickness(0, 8, 0, 0),
        };
        applyBtn.SetResourceReference(Button.StyleProperty,
            isActive ? "SecondaryButton" : "PrimaryButton");

        if (!isActive)
        {
            var capturedSp2 = sp2Path;
            applyBtn.Click += (_, _) => ApplyTheme(capturedSp2);
        }

        var info = new StackPanel { Margin = new Thickness(10) };
        info.Children.Add(nameTb);
        info.Children.Add(applyBtn);

        var layout = new StackPanel();
        layout.Children.Add(thumb);
        layout.Children.Add(info);

        card.Child = layout;
        return card;
    }

    private static BitmapImage? FindThumbnail(string dir)
    {
        // prefer background_*.png, then first png
        var candidates = new[]
        {
            Directory.GetFiles(dir, "background*.png").FirstOrDefault(),
            Directory.GetFiles(dir, "*.png").FirstOrDefault()
        };

        var path = candidates.FirstOrDefault(p => p is not null);
        if (path is null) return null;

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource        = new Uri(path, UriKind.Absolute);
            bmp.CacheOption      = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 440; // 2× card width for crisp display
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    // ── Apply theme ───────────────────────────────────────────

    private void ApplyTheme(string sp2FullPath)
    {
        if (_settingsService is null) return;

        // Store as relative path
        var rel = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, sp2FullPath);
        _settingsService.Settings.ActiveSp2Template = rel;
        _settingsService.Save();

        ThemeApplied?.Invoke(sp2FullPath);
        RefreshCards();
    }

    // ── .spzip import ─────────────────────────────────────────

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "导入 SensorPanel 主题",
            Filter = "SensorPanel 主题包 (*.spzip)|*.spzip|所有文件 (*.*)|*.*",
        };

        if (dlg.ShowDialog() != true) return;

        var zipPath  = dlg.FileName;
        var themeName = Path.GetFileNameWithoutExtension(zipPath);
        var destDir   = Path.Combine(MonitorThemesDir, themeName);

        try
        {
            if (Directory.Exists(destDir))
            {
                var r = MessageBox.Show(
                    $"主题「{themeName}」已存在，是否覆盖？",
                    "导入主题", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (r != MessageBoxResult.OK) return;
                Directory.Delete(destDir, true);
            }

            ZipFile.ExtractToDirectory(zipPath, destDir);

            // Auto-apply if this is the only / first theme
            var sp2 = Directory.GetFiles(destDir, "*.sp2").FirstOrDefault();
            if (sp2 is not null)
                ApplyTheme(sp2);
            else
                RefreshCards();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导入失败：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
