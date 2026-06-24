using System.IO;
using System.IO.Compression;
using System.Windows;
using Microsoft.Win32;
using SSM.Core.Helpers;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace SSM.Views.Dialogs;

public partial class ImportDialog : Window
{
    private static string MonitorThemesDir =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes", "Monitor");

    private static readonly string[] AcceptedExts = [".spzip", ".sensorpanel", ".sp2"];

    public string? ImportedSp2Path { get; private set; }

    public ImportDialog()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (HasAcceptedFile(e))
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
        if (!HasAcceptedFile(e)) return;
        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)!;
        var file = files.FirstOrDefault(f =>
            AcceptedExts.Contains(Path.GetExtension(f).ToLowerInvariant()));
        if (file is not null)
            TryImport(file);
    }

    private void DropZone_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "选择 SensorPanel 主题包",
            Filter = "SensorPanel 主题包 (*.spzip;*.sensorpanel;*.sp2)|*.spzip;*.sensorpanel;*.sp2|所有文件 (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true)
            TryImport(dlg.FileName);
    }

    private void TryImport(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        try
        {
            string sp2;
            if (ext == ".sensorpanel")
            {
                sp2 = ImportSensorpanel(path);
            }
            else if (ext == ".spzip")
            {
                sp2 = ImportSpzip(path);
            }
            else
            {
                sp2 = ImportSp2Dir(path);
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

    private string ImportSensorpanel(string sensorpanelPath)
    {
        var themeName = Path.GetFileNameWithoutExtension(sensorpanelPath);
        var destDir   = Path.Combine(MonitorThemesDir, themeName);

        if (Directory.Exists(destDir))
        {
            var r = System.Windows.MessageBox.Show(
                $"主题「{themeName}」已存在，是否覆盖？",
                "导入主题", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) throw new OperationCanceledException();
            Directory.Delete(destDir, true);
        }

        return SensorpanelImporter.Import(sensorpanelPath, MonitorThemesDir);
    }

    private string ImportSpzip(string zipPath)
    {
        var themeName = Path.GetFileNameWithoutExtension(zipPath);
        var destDir   = Path.Combine(MonitorThemesDir, themeName);

        if (Directory.Exists(destDir))
        {
            var r = System.Windows.MessageBox.Show(
                $"主题「{themeName}」已存在，是否覆盖？",
                "导入主题", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) throw new OperationCanceledException();
            Directory.Delete(destDir, true);
        }

        Directory.CreateDirectory(MonitorThemesDir);
        ZipFile.ExtractToDirectory(zipPath, destDir);

        return Directory.GetFiles(destDir, "*.sp2").FirstOrDefault()
            ?? throw new InvalidDataException("压缩包中未找到 .sp2 文件");
    }

    private string ImportSp2Dir(string sp2Path)
    {
        var srcDir    = Path.GetDirectoryName(sp2Path)!;
        var themeName = Path.GetFileName(srcDir);
        var destDir   = Path.Combine(MonitorThemesDir, themeName);

        if (Directory.Exists(destDir))
        {
            var r = System.Windows.MessageBox.Show(
                $"主题「{themeName}」已存在，是否覆盖？",
                "导入主题", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) throw new OperationCanceledException();
            Directory.Delete(destDir, true);
        }

        CopyDirectory(srcDir, destDir);
        return Path.Combine(destDir, Path.GetFileName(sp2Path));
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
    }

    private void ShowError(string msg)
    {
        ErrorText.Text = msg;
        ErrorText.Visibility = Visibility.Visible;
    }

    private static bool HasAcceptedFile(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return false;
        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)!;
        return files.Any(f => AcceptedExts.Contains(Path.GetExtension(f).ToLowerInvariant()));
    }
}
