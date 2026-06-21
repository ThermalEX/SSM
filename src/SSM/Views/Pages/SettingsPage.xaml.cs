using System.Windows.Controls;
using System.Windows.Input;
using SSM.ViewModels;

namespace SSM.Views.Pages;

public partial class SettingsPage : System.Windows.Controls.UserControl
{
    private readonly SettingsViewModel _vm;
    private bool _capturing;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        SyncRefreshRate();
        HotkeyBox.Text = vm.HotkeyDisplay;
    }

    private void SyncRefreshRate()
    {
        RefreshRateCombo.SelectedIndex = _vm.RefreshIntervalMs switch
        {
            500  => 0,
            2000 => 2,
            _    => 1
        };
    }

    private void RefreshRateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RefreshRateCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && int.TryParse(tag, out var ms))
        {
            _vm.RefreshIntervalMs = ms;
        }
    }

    private void HotkeyBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        _capturing = true;
        HotkeyBox.Text = "按下新快捷键...";
        if (System.Windows.Application.Current.Resources["Accent"] is System.Windows.Media.Brush accent)
            HotkeyBorder.BorderBrush = accent;
    }

    private void HotkeyBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        _capturing = false;
        HotkeyBox.Text = _vm.HotkeyDisplay;
        if (System.Windows.Application.Current.Resources["DividerBrush"] is System.Windows.Media.Brush divider)
            HotkeyBorder.BorderBrush = divider;
    }

    private void HotkeyBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
               or Key.LeftAlt  or Key.RightAlt  or Key.LWin       or Key.RWin or Key.Tab)
            return;

        if (key == Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        uint modifiers = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl)  || Keyboard.IsKeyDown(Key.RightCtrl))  modifiers |= 0x0002;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) modifiers |= 0x0004;
        if (Keyboard.IsKeyDown(Key.LeftAlt)   || Keyboard.IsKeyDown(Key.RightAlt))   modifiers |= 0x0001;
        if (Keyboard.IsKeyDown(Key.LWin)      || Keyboard.IsKeyDown(Key.RWin))       modifiers |= 0x0008;

        if (modifiers == 0) return;

        var vk      = (uint)KeyInterop.VirtualKeyFromKey(key);
        var display = BuildDisplay(modifiers, key);

        _vm.UpdateHotkey(modifiers, vk, display);
        HotkeyBox.Text = display;
        _capturing = false;
        Keyboard.ClearFocus();
    }

    private static string BuildDisplay(uint mods, Key key)
    {
        var parts = new List<string>();
        if ((mods & 0x0002) != 0) parts.Add("Ctrl");
        if ((mods & 0x0004) != 0) parts.Add("Shift");
        if ((mods & 0x0001) != 0) parts.Add("Alt");
        if ((mods & 0x0008) != 0) parts.Add("Win");
        parts.Add(KeyLabel(key));
        return string.Join(" + ", parts);
    }

    private static string KeyLabel(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => $"Num{key - Key.NumPad0}",
        Key.OemPlus   => "=",
        Key.OemMinus  => "-",
        Key.OemPeriod => ".",
        Key.OemComma  => ",",
        Key.Space     => "Space",
        Key.Delete    => "Del",
        Key.Insert    => "Ins",
        Key.Prior     => "PgUp",
        Key.Next      => "PgDn",
        _             => key.ToString()
    };
}
