using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using SSM.Core.Models;

namespace SSM.Views.Pages;

public partial class EditorPage : System.Windows.Controls.UserControl
{
    private const string ThemesDir = "Assets/Themes";

    public EditorPage()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshList();
    }

    private void RefreshList()
    {
        ThemeCardPanel.Children.Clear();
        Directory.CreateDirectory(ThemesDir);

        var files = Directory.GetFiles(ThemesDir, "*.json");
        EmptyHint.Visibility = files.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var file in files)
        {
            ThemeConfig? cfg = null;
            try
            {
                var json = File.ReadAllText(file);
                cfg = JsonSerializer.Deserialize<ThemeConfig>(json);
            }
            catch { /* 跳过损坏文件 */ }

            var name  = cfg?.Name ?? Path.GetFileNameWithoutExtension(file);
            var count = cfg?.Components.Count ?? 0;
            ThemeCardPanel.Children.Add(BuildCard(file, name, count));
        }
    }

    private System.Windows.Controls.Border BuildCard(string filePath, string name, int componentCount)
    {
        // 使用 SetResourceReference 绑定动态资源，主题切换时自动更新
        var card = new System.Windows.Controls.Border { Width = 200, Margin = new Thickness(8) };
        card.SetResourceReference(System.Windows.Controls.Border.StyleProperty, "CardBorder");

        var nameBlock = new System.Windows.Controls.TextBlock
        {
            Text       = name,
            FontSize   = 14,
            FontWeight = FontWeights.SemiBold,
        };
        nameBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextPrimary");

        var countBlock = new System.Windows.Controls.TextBlock
        {
            Text   = $"{componentCount} 个组件",
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
        };
        countBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextSecondary");

        var editBtn = new System.Windows.Controls.Button
        {
            Content = "编辑",
            Padding = new Thickness(12, 4, 12, 4),
        };
        editBtn.SetResourceReference(System.Windows.Controls.Button.StyleProperty, "SecondaryButton");
        editBtn.Click += (_, _) => OpenEditor(filePath);

        var deleteBtn = new System.Windows.Controls.Button
        {
            Content         = "删除",
            Background      = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize        = 12,
            Cursor          = System.Windows.Input.Cursors.Hand,
            Margin          = new Thickness(8, 0, 0, 0),
            Padding         = new Thickness(4),
        };
        deleteBtn.SetResourceReference(System.Windows.Controls.Button.ForegroundProperty, "DangerBrush");
        deleteBtn.Click += (_, _) => DeleteTheme(filePath);

        var btnRow = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin      = new Thickness(0, 12, 0, 0),
        };
        btnRow.Children.Add(editBtn);
        btnRow.Children.Add(deleteBtn);

        var content = new System.Windows.Controls.StackPanel();
        content.Children.Add(nameBlock);
        content.Children.Add(countBlock);
        content.Children.Add(btnRow);

        card.Child = content;
        return card;
    }

    private void OpenEditor(string filePath)
    {
        var win = new ThemeEditorWindow(filePath);
        win.ShowDialog();
        RefreshList();
    }

    private void DeleteTheme(string filePath)
    {
        var name   = Path.GetFileNameWithoutExtension(filePath);
        var result = System.Windows.MessageBox.Show(
            $"确定要删除主题「{name}」吗？此操作不可撤销。",
            "删除主题",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.OK)
        {
            File.Delete(filePath);
            RefreshList();
        }
    }

    // ── 新建主题 ──────────────────────────────────────────────

    private void NewThemeButton_Click(object sender, RoutedEventArgs e)
    {
        NewThemeNameBox.Text     = string.Empty;
        NewThemePanel.Visibility = Visibility.Visible;
        NewThemeNameBox.Focus();
    }

    private void ConfirmNewTheme_Click(object sender, RoutedEventArgs e) => TryCreateTheme();

    private void CancelNewTheme_Click(object sender, RoutedEventArgs e)
    {
        NewThemePanel.Visibility = Visibility.Collapsed;
    }

    private void NewThemeNameBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)  TryCreateTheme();
        if (e.Key == Key.Escape) NewThemePanel.Visibility = Visibility.Collapsed;
    }

    private void TryCreateTheme()
    {
        var name = NewThemeNameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        Directory.CreateDirectory(ThemesDir);
        var path = Path.Combine(ThemesDir, $"{name}.json");

        var cfg  = new ThemeConfig { Name = name };
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);

        NewThemePanel.Visibility = Visibility.Collapsed;
        OpenEditor(path);
    }
}
