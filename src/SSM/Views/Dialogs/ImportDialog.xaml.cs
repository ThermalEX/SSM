using System.IO;
using System.IO.Compression;
using System.Windows;
using Microsoft.Win32;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace SSM.Views.Dialogs;

public partial class ImportDialog : Window
{
    private static string MonitorThemesDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "monitor");

    public string? ImportedSp2Path { get; private set; }

    public ImportDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (IsSpzipDrop(e))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
        }
    }

    private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!IsSpzipDrop(e)) return;
        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)!;
        var spzip = files.FirstOrDefault(f =>
            string.Equals(Path.GetExtension(f), ".spzip", StringComparison.OrdinalIgnoreCase));
        if (spzip is not null)
            TryImport(spzip);
    }

    private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "选择 SensorPanel 主题包",
            Filter = "SensorPanel 主题包 (*.spzip)|*.spzip|所有文件 (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            TryImport(dlg.FileName);
    }

    private void TryImport(string zipPath)
    {
        var themeName = Path.GetFileNameWithoutExtension(zipPath);
        var destDir   = Path.Combine(MonitorThemesDir, themeName);

        try
        {
            if (Directory.Exists(destDir))
            {
                var r = System.Windows.MessageBox.Show(
                    $"主题「{themeName}」已存在，是否覆盖？",
                    "导入主题", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (r != MessageBoxResult.OK) return;
                Directory.Delete(destDir, true);
            }

            Directory.CreateDirectory(MonitorThemesDir);
            ZipFile.ExtractToDirectory(zipPath, destDir);

            var sp2 = Directory.GetFiles(destDir, "*.sp2").FirstOrDefault();
            if (sp2 is null)
            {
                ShowError("压缩包中未找到 .sp2 文件");
                return;
            }

            ImportedSp2Path = sp2;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"导入失败：{ex.Message}");
        }
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static bool IsSpzipDrop(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return false;
        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)!;
        return files.Any(f =>
            string.Equals(Path.GetExtension(f), ".spzip", StringComparison.OrdinalIgnoreCase));
    }
}
