using System.IO;
using System.Windows;
using System.Windows.Controls;
using Button = System.Windows.Controls.Button;
using UserControl = System.Windows.Controls.UserControl;
using SSM.Core.Interfaces;

namespace SSM.Views.Pages;

public partial class ThemePage : UserControl
{
    private ISettingsService? _settingsService;
    private IHardwareMonitorService? _monitor;

    public event Action<string>? ThemeApplied;

    private static string MonitorThemesDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "monitor");

    // Ordered list of (name, sp2Path) for all discovered themes
    private readonly List<(string Name, string Sp2Path)> _themes = new();
    private int _previewIdx = -1;   // index into _themes currently shown in preview
    private string _activeSp2 = "";  // full path of currently applied theme

    public ThemePage()
    {
        InitializeComponent();
    }

    public void Initialize(ISettingsService settingsService, IHardwareMonitorService monitor)
    {
        _settingsService = settingsService;
        _monitor = monitor;
        Loaded += (_, _) => Refresh();
    }

    public void Refresh()
    {
        _activeSp2 = ResolveActiveSp2();
        ScanThemes();
        RebuildCombo();

        // Jump to active theme in preview, or first available
        int activeIdx = _themes.FindIndex(t =>
            string.Equals(t.Sp2Path, _activeSp2, StringComparison.OrdinalIgnoreCase));
        ShowPreview(activeIdx >= 0 ? activeIdx : (_themes.Count > 0 ? 0 : -1));
    }

    public void Cleanup() => Preview.Cleanup();

    // ── Navigation ────────────────────────────────────

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_themes.Count == 0) return;
        int idx = (_previewIdx - 1 + _themes.Count) % _themes.Count;
        ShowPreview(idx);
        SyncCombo();
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_themes.Count == 0) return;
        int idx = (_previewIdx + 1) % _themes.Count;
        ShowPreview(idx);
        SyncCombo();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int idx = ThemeCombo.SelectedIndex;
        if (idx < 0 || idx == _previewIdx) return;
        ShowPreview(idx);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_previewIdx < 0 || _previewIdx >= _themes.Count) return;
        var sp2 = _themes[_previewIdx].Sp2Path;

        var rel = Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, sp2);
        _settingsService!.Settings.ActiveSp2Template = rel;
        _settingsService.Save();

        _activeSp2 = sp2;
        UpdateApplyButton();
        ThemeApplied?.Invoke(sp2);
    }

    // ── Helpers ───────────────────────────────────────

    private void ShowPreview(int idx)
    {
        _previewIdx = idx;
        if (idx < 0 || idx >= _themes.Count)
        {
            PrevBtn.IsEnabled = false;
            NextBtn.IsEnabled = false;
            ApplyBtn.IsEnabled = false;
            return;
        }

        var (name, sp2) = _themes[idx];

        if (_monitor is not null)
            Preview.Initialize(_monitor, sp2);

        PrevBtn.IsEnabled = _themes.Count > 1;
        NextBtn.IsEnabled = _themes.Count > 1;
        UpdateApplyButton();
    }

    private void UpdateApplyButton()
    {
        if (_previewIdx < 0 || _previewIdx >= _themes.Count) return;
        bool isCurrent = string.Equals(
            _themes[_previewIdx].Sp2Path, _activeSp2, StringComparison.OrdinalIgnoreCase);

        ApplyBtn.Content = isCurrent ? "✓ 当前使用" : "应用此主题";
        ApplyBtn.IsEnabled = !isCurrent;
        ApplyBtn.SetResourceReference(Button.StyleProperty,
            isCurrent ? "SecondaryButton" : "PrimaryButton");

    }

    private void SyncCombo()
    {
        ThemeCombo.SelectionChanged -= ThemeCombo_SelectionChanged;
        ThemeCombo.SelectedIndex = _previewIdx;
        ThemeCombo.SelectionChanged += ThemeCombo_SelectionChanged;
    }

    private void ScanThemes()
    {
        _themes.Clear();
        if (!Directory.Exists(MonitorThemesDir)) return;

        foreach (var dir in Directory.GetDirectories(MonitorThemesDir).OrderBy(d => d))
        {
            var sp2 = Directory.GetFiles(dir, "*.sp2").FirstOrDefault();
            if (sp2 is not null)
                _themes.Add((Path.GetFileName(dir), sp2));
        }
    }

    private void RebuildCombo()
    {
        ThemeCombo.SelectionChanged -= ThemeCombo_SelectionChanged;
        ThemeCombo.Items.Clear();
        foreach (var (name, _) in _themes)
            ThemeCombo.Items.Add(name);
        ThemeCombo.SelectionChanged += ThemeCombo_SelectionChanged;
    }

    private string ResolveActiveSp2()
    {
        if (_settingsService is null) return "";
        var rel  = _settingsService.Settings.ActiveSp2Template;
        var full = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rel));
        return File.Exists(full) ? full : "";
    }

}
