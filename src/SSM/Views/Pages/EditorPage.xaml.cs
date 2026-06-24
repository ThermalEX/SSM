using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using SSM.Core.Helpers;
using SSM.Core.Interfaces;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace SSM.Views.Pages;

public partial class EditorPage : UserControl
{
    private ISettingsService? _settingsService;

    public event Action<string>? ThemeApplied;

    private static string MonitorThemesDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "Monitor");

    public EditorPage()
    {
        InitializeComponent();
    }

    public void Initialize(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        Loaded += (_, _) => RefreshCards();
    }

    public void Refresh() => RefreshCards();

    // ── Card building ─────────────────────────────────

    private void RefreshCards()
    {
        CardPanel.Children.Clear();
        if (!Directory.Exists(MonitorThemesDir)) return;

        string activeFull = ResolveActiveSp2();

        foreach (var dir in Directory.GetDirectories(MonitorThemesDir).OrderBy(d => d))
        {
            var sp2 = Directory.GetFiles(dir, "*.sp2").FirstOrDefault();
            if (sp2 is null) continue;

            bool isActive = string.Equals(
                Path.GetFullPath(sp2), activeFull, StringComparison.OrdinalIgnoreCase);

            CardPanel.Children.Add(BuildCard(dir, sp2, isActive));
        }
    }

    private Border BuildCard(string dir, string sp2Path, bool isActive)
    {
        var name = Path.GetFileName(dir);

        var card = new Border
        {
            Width           = 220,
            Margin          = new Thickness(8),
            CornerRadius    = new CornerRadius(10),
            BorderThickness = new Thickness(isActive ? 2 : 1),
        };
        card.SetResourceReference(Border.BackgroundProperty, "BgCard");
        card.SetResourceReference(Border.BorderBrushProperty, isActive ? "Accent" : "DividerBrush");

        // Thumbnail: try sp2 layout preview first, then static PNG fallback
        var thumb = new Border { Height = 110, CornerRadius = new CornerRadius(8, 8, 0, 0), ClipToBounds = true };
        var bmp = Sp2ThumbnailRenderer.TryRender(sp2Path, 440, 260) ?? FindThumbnail(dir);
        if (bmp is not null)
            thumb.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
        else
            thumb.SetResourceReference(Border.BackgroundProperty, "BgDeep");

        // Name
        var nameTb = new TextBlock
        {
            Text         = name,
            FontSize     = 13,
            FontWeight   = FontWeights.SemiBold,
            Margin       = new Thickness(0, 0, 0, 6),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        nameTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");

        // Apply button
        var applyBtn = new Button
        {
            Content   = isActive ? "✓ 当前使用" : "应用",
            IsEnabled = !isActive,
            Padding   = new Thickness(0, 5, 0, 5),
            Margin    = new Thickness(0, 8, 0, 4),
        };
        applyBtn.SetResourceReference(Button.StyleProperty, isActive ? "SecondaryButton" : "PrimaryButton");
        if (!isActive)
        {
            var captured = sp2Path;
            applyBtn.Click += (_, _) => ApplyTheme(captured);
        }

        // Edit button
        var openBtn = new Button
        {
            Content = "编辑",
            Padding = new Thickness(0, 5, 0, 5),
        };
        openBtn.SetResourceReference(Button.StyleProperty, "SecondaryButton");
        var capturedSp2 = sp2Path;
        openBtn.Click += (_, _) =>
        {
            var editor = new ThemeEditorWindow(capturedSp2)
            {
                Owner = Window.GetWindow(this)
            };
            editor.ShowDialog();
            RefreshCards();
        };

        // Delete button
        var delBtn = new Button
        {
            Content = "删除",
            Padding = new Thickness(0, 5, 0, 5),
            Margin  = new Thickness(0, 4, 0, 0),
        };
        delBtn.SetResourceReference(Button.StyleProperty, "SecondaryButton");
        var capturedDir = dir;
        var capturedName = name;
        delBtn.Click += (_, _) =>
        {
            var r = System.Windows.MessageBox.Show(
                $"确定删除主题「{capturedName}」？此操作不可撤销。",
                "删除主题", System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Warning);
            if (r != System.Windows.MessageBoxResult.OK) return;

            // If this is the active theme, clear the setting
            if (isActive && _settingsService is not null)
            {
                _settingsService.Settings.ActiveSp2Template = "";
                _settingsService.Save();
            }

            try { Directory.Delete(capturedDir, recursive: true); }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }
            RefreshCards();
        };

        var info = new StackPanel { Margin = new Thickness(10) };
        info.Children.Add(nameTb);
        info.Children.Add(applyBtn);
        info.Children.Add(openBtn);
        info.Children.Add(delBtn);

        var layout = new StackPanel();
        layout.Children.Add(thumb);
        layout.Children.Add(info);

        card.Child = layout;
        return card;
    }

    private static BitmapImage? FindThumbnail(string dir)
    {
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
            bmp.DecodePixelWidth = 440;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    // ── Apply ─────────────────────────────────────────

    private void ApplyTheme(string sp2FullPath)
    {
        if (_settingsService is null) return;
        var rel = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, sp2FullPath);
        _settingsService.Settings.ActiveSp2Template = rel;
        _settingsService.Save();
        ThemeApplied?.Invoke(sp2FullPath);
        RefreshCards();
    }

    // ── Import ────────────────────────────────────────

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title  = "选择主题文件",
            Filter = "主题文件 (*.sensorpanel;*.spzip;*.sp2)|*.sensorpanel;*.spzip;*.sp2|所有文件 (*.*)|*.*",
        };
        if (picker.ShowDialog() != true) return;

        try
        {
            var sp2 = ThemeImporter.Import(picker.FileName, MonitorThemesDir);
            ApplyTheme(sp2);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"导入失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // ── Helpers ───────────────────────────────────────

    private string ResolveActiveSp2()
    {
        if (_settingsService is null) return "";
        var rel  = _settingsService.Settings.ActiveSp2Template;
        var full = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rel));
        return File.Exists(full) ? full : "";
    }
}
